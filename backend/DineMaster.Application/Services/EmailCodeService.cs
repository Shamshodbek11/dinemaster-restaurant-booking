using System.Security.Cryptography;
using System.Text;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using DineMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Application.Services;

/// <summary>
/// Emailga 6 xonali bir martalik kod yuborish va tekshirish.
///  - kod bazada faqat hash ko'rinishida saqlanadi
///  - yangi kod eski faol kodlarni bekor qiladi; qayta yuborish 60 soniyada bir marta
///  - 5 ta xato urinishdan keyin kod bekor bo'ladi
/// </summary>
public class EmailCodeService
{
    public const int CodeLifetimeMinutes = 15;
    public const int MaxAttempts = 5;
    private static readonly TimeSpan ResendCooldown = TimeSpan.FromSeconds(60);

    private readonly IAppDbContext _db;
    private readonly IEmailService _email;

    public EmailCodeService(IAppDbContext db, IEmailService email)
    {
        _db = db;
        _email = email;
    }

    public async Task SendAsync(AppUser user, string fullName, string purpose, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var active = await _db.EmailCodes
            .Where(c => c.UserId == user.Id && c.Purpose == purpose && c.ConsumedAt == null && c.ExpiresAt > now)
            .ToListAsync(ct);

        var latest = active.MaxBy(c => c.CreatedAt);
        if (latest is not null && now - latest.CreatedAt < ResendCooldown)
        {
            var wait = (int)Math.Ceiling((ResendCooldown - (now - latest.CreatedAt)).TotalSeconds);
            throw new BusinessRuleException($"Kod yaqinda yuborildi. {wait} soniyadan keyin qayta urinib ko'ring.");
        }

        foreach (var old in active) old.ExpiresAt = now;

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        _db.EmailCodes.Add(new EmailCode
        {
            UserId = user.Id,
            Email = user.Email,
            Purpose = purpose,
            CodeHash = Hash(user.Id, purpose, code),
            ExpiresAt = now.AddMinutes(CodeLifetimeMinutes)
        });
        await _db.SaveChangesAsync(ct);

        await _email.SendAsync(EmailTemplates.Code(user.Email, fullName, purpose, code, CodeLifetimeMinutes), ct);
    }

    /// <summary>Kod to'g'ri bo'lsa uni ishlatilgan deb belgilaydi (SaveChanges chaqiruvchida emas — shu yerda).</summary>
    public async Task VerifyAsync(Guid userId, string purpose, string? code, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var entry = await _db.EmailCodes
            .Where(c => c.UserId == userId && c.Purpose == purpose && c.ConsumedAt == null && c.ExpiresAt > now)
            .OrderByDescending(c => c.CreatedAt)
            .FirstOrDefaultAsync(ct);

        if (entry is null)
            throw new BusinessRuleException("Kod noto'g'ri yoki muddati tugagan. Yangi kod so'rang.");

        var expected = Convert.FromHexString(entry.CodeHash);
        var actual = Convert.FromHexString(Hash(userId, purpose, (code ?? string.Empty).Trim()));
        if (!CryptographicOperations.FixedTimeEquals(expected, actual))
        {
            entry.Attempts++;
            var left = MaxAttempts - entry.Attempts;
            if (left <= 0) entry.ExpiresAt = now;
            await _db.SaveChangesAsync(ct);
            throw new BusinessRuleException(left <= 0
                ? "Juda ko'p noto'g'ri urinish. Yangi kod so'rang."
                : $"Kod noto'g'ri. Qolgan urinishlar: {left}.");
        }

        entry.ConsumedAt = now;
        await _db.SaveChangesAsync(ct);
    }

    private static string Hash(Guid userId, string purpose, string code)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{userId:N}:{purpose}:{code}")));
}
