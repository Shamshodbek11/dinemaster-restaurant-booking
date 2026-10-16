using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using DineMaster.Application.DTOs.Auth;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineMaster.Application.Services;

public class JwtOptions
{
    public string Issuer { get; set; } = "DineMaster";
    public string Audience { get; set; } = "DineMasterClient";
    public string Secret { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 60 * 24;
}

public class AuthService : IAuthService
{
    private static readonly string DummyPasswordHash = BCrypt.Net.BCrypt.HashPassword("dummy-password-for-timing");

    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly JwtOptions _jwtOptions;
    private readonly ILogger<AuthService> _logger;
    private readonly EmailCodeService _codes;

    public AuthService(
        IAppDbContext db,
        ITokenService tokenService,
        IOptions<JwtOptions> jwtOptions,
        ILogger<AuthService> logger,
        EmailCodeService codes)
    {
        _db = db;
        _tokenService = tokenService;
        _jwtOptions = jwtOptions.Value;
        _logger = logger;
        _codes = codes;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        if (!request.AcceptTerms)
            throw new BusinessRuleException("Ro'yxatdan o'tish uchun foydalanish shartlari va maxfiylik siyosatiga rozilik bering.");
        var (user, profile) = await CreateUserAsync(request, emailConfirmed: false, ct);

        // Tasdiqlash kodi. Email yuborilmasa ham ro'yxatdan o'tish bekor qilinmaydi —
        // foydalanuvchi kodni keyinroq qayta so'rashi mumkin.
        try
        {
            await _codes.SendAsync(user, profile.FullName, EmailCode.PurposeVerifyEmail, ct);
        }
        catch (Exception ex) when (ex is ServiceUnavailableException or BusinessRuleException)
        {
            _logger.LogWarning(ex, "Ro'yxatdan o'tishda tasdiqlash kodi yuborilmadi: {UserId}", user.Id);
        }

        return BuildAuthResponse(user, profile, new[] { Roles.User });
    }

    private async Task<(AppUser User, Profile Profile)> CreateUserAsync(
        RegisterRequest request, bool emailConfirmed, CancellationToken ct)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail, ct))
            throw new ConflictException("Bu email allaqachon ro'yxatdan o'tgan.");

        var user = new AppUser
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            EmailConfirmed = emailConfirmed
        };

        var profile = new Profile
        {
            Id = user.Id,
            Email = normalizedEmail,
            FullName = request.FullName.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : PhoneNumber.Normalize(request.Phone),
            PreferredLanguage = request.PreferredLanguage,
            YumsBalance = 0,
            TermsAcceptedAt = request.AcceptTerms ? DateTime.UtcNow : null,
            ReferralCode = await ReferralCodes.NewUniqueAsync(_db, ct)
        };

        // Kim taklif qilgan (bonus — do'st birinchi tashrifini yakunlaganda, BookingService'da)
        if (!string.IsNullOrWhiteSpace(request.ReferralCode))
        {
            var code = request.ReferralCode.Trim().ToUpperInvariant();
            profile.ReferredBy = await _db.Profiles.Where(p => p.ReferralCode == code && !p.IsBanned)
                .Select(p => (Guid?)p.Id).FirstOrDefaultAsync(ct);
        }

        var role = new UserRoleEntity { UserId = user.Id, Role = AppRole.User };

        _db.Users.Add(user);
        _db.Profiles.Add(profile);
        _db.UserRoles.Add(role);
        await _db.SaveChangesAsync(ct);
        return (user, profile);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // User topilmasa ham bcrypt bajariladi — javob vaqtidan email mavjudligini bilib bo'lmasin
        var passwordOk = BCrypt.Net.BCrypt.Verify(request.Password, user?.PasswordHash ?? DummyPasswordHash);
        if (user is null || !passwordOk)
            throw new UnauthorizedAccessException("Email yoki parol noto'g'ri.");

        if (user.Profile?.IsBanned == true)
            throw new UnauthorizedAccessException("Hisobingiz bloklangan. Administrator bilan bog'laning.");

        var roles = user.Roles.Select(r => r.Role.ToRoleString()).ToList();
        if (roles.Count == 0) roles.Add(Roles.User);

        return BuildAuthResponse(user, user.Profile!, roles);
    }

    public async Task<AuthResponse> AssignInitialSuperadminAsync(
        string email, string password, string fullName, CancellationToken ct = default)
    {
        var hasSuperadmin = await _db.UserRoles.AnyAsync(r => r.Role == AppRole.Superadmin, ct);
        if (hasSuperadmin)
            throw new ConflictException("Superadmin allaqachon mavjud.");

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users
            .Include(u => u.Profile)
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        if (user is null)
        {
            await CreateUserAsync(new RegisterRequest
            {
                Email = normalizedEmail,
                Password = password,
                FullName = fullName,
                AcceptTerms = true
            }, emailConfirmed: true, ct);

            user = await _db.Users
                .Include(u => u.Profile)
                .Include(u => u.Roles)
                .FirstAsync(u => u.Email == normalizedEmail, ct);
        }
        else
        {
            if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                throw new UnauthorizedAccessException("Mavjud user uchun parol noto'g'ri.");
        }

        if (!user.Roles.Any(r => r.Role == AppRole.Superadmin))
        {
            _db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, Role = AppRole.Superadmin });
            await _db.SaveChangesAsync(ct);
        }

        var allRoles = await _db.UserRoles
            .Where(r => r.UserId == user.Id)
            .Select(r => r.Role)
            .ToListAsync(ct);

        return BuildAuthResponse(user, user.Profile!, allRoles.Select(r => r.ToRoleString()));
    }

    public async Task SendEmailVerificationCodeAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Foydalanuvchi topilmadi.");
        if (user.EmailConfirmed)
            throw new BusinessRuleException("Email allaqachon tasdiqlangan.");

        await _codes.SendAsync(user, user.Profile?.FullName ?? user.Email, EmailCode.PurposeVerifyEmail, ct);
    }

    public async Task VerifyEmailAsync(Guid userId, string code, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct)
            ?? throw new NotFoundException("Foydalanuvchi topilmadi.");
        if (user.EmailConfirmed) return;

        await _codes.VerifyAsync(userId, EmailCode.PurposeVerifyEmail, code, ct);
        user.EmailConfirmed = true;
        await _db.SaveChangesAsync(ct);
    }

    public async Task ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct);

        // Xavfsizlik: user topilmasa ham muvaffaqiyatli javob qaytaramiz (email mavjudligi bilinmasin)
        if (user is null) return;

        try
        {
            await _codes.SendAsync(user, user.Profile?.FullName ?? user.Email, EmailCode.PurposeResetPassword, ct);
            _logger.LogInformation("Parolni tiklash kodi yuborildi: {UserId}", user.Id);
        }
        catch (BusinessRuleException)
        {
            // Qayta yuborish cheklovi — javobni o'zgartirmaymiz (email mavjudligi bilinmasin)
        }
    }

    public async Task ResetPasswordAsync(string email, string code, string newPassword, CancellationToken ct = default)
    {
        var normalizedEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail, ct)
            ?? throw new BusinessRuleException("Kod noto'g'ri yoki muddati tugagan. Yangi kod so'rang.");

        await _codes.VerifyAsync(user.Id, EmailCode.PurposeResetPassword, code, ct);

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
        user.EmailConfirmed = true; // kod emailga keldi — demak email egasi shu
        await _db.SaveChangesAsync(ct);
    }

    private AuthResponse BuildAuthResponse(AppUser user, Profile profile, IEnumerable<string> roles)
    {
        var roleList = roles.ToList();
        var token = _tokenService.CreateAccessToken(user, roleList);
        return new AuthResponse
        {
            AccessToken = token,
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes),
            User = new AuthUserDto
            {
                Id = user.Id,
                Email = user.Email,
                FullName = profile.FullName,
                Roles = roleList,
                YumsBalance = profile.YumsBalance,
                AvatarUrl = profile.AvatarUrl,
                PreferredLanguage = profile.PreferredLanguage,
                EmailConfirmed = user.EmailConfirmed
            }
        };
    }
}
