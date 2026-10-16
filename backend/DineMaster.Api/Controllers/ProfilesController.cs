using DineMaster.Api.Extensions;
using DineMaster.Application.Services;
using DineMaster.Application.Common;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/profiles")]
[Authorize]
public class ProfilesController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProfilesController(AppDbContext db) { _db = db; }

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (p is null) return NotFound();
        return Ok(MapProfile(p));
    }

    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (p is null) return NotFound();

        if (req.FullName is not null)
        {
            var name = req.FullName.Trim();
            if (name.Length is < 2 or > 150)
                return BadRequest(new { message = "Ism 2–150 belgi bo'lishi kerak." });
            p.FullName = name;
        }
        if (req.Phone is not null)
        {
            var phone = PhoneNumber.Normalize(req.Phone);
            if (phone.Length > 0 && !PhoneNumber.IsValid(phone))
                return BadRequest(new { message = "Telefon raqami +998XXXXXXXXX formatida bo'lishi kerak." });
            p.Phone = phone.Length > 0 ? phone : null;
        }
        p.AvatarUrl = req.AvatarUrl ?? p.AvatarUrl;
        p.Bio = req.Bio ?? p.Bio;
        p.PreferredLanguage = req.PreferredLanguage ?? p.PreferredLanguage;
        p.DietaryPreferences = req.DietaryPreferences ?? p.DietaryPreferences;
        await _db.SaveChangesAsync(ct);
        return Ok(MapProfile(p));
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < 8)
            return BadRequest(new { message = "Yangi parol kamida 8 belgidan iborat bo'lishi kerak." });
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return NotFound();
        if (!BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Joriy parol noto'g'ri." });
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Parol yangilandi." });
    }

    // BUG FIX / FEATURE CHANGE: RedeemYums endpoint olib tashlandi.
    // Yums faqat bron uchun sarflanadi, naqd pul yoki kupon ga almashtirish yo'q.
    // Bron yaratishda UseYums=true orqali ishlatiladi.

    [HttpGet("yums/history")]
    public async Task<IActionResult> YumsHistory(
        [FromQuery] int page = 1, [FromQuery] int size = 30, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var q = _db.YumsAudits
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new
        {
            total, page, size,
            items = items.Select(a => new
            {
                a.Id, a.Amount, a.Reason, a.CreatedAt
            })
        });
    }

    /// <summary>Do'st taklifi: kod, taklif qilinganlar va olingan bonus.</summary>
    [HttpGet("referral")]
    public async Task<IActionResult> Referral([FromServices] Microsoft.Extensions.Options.IOptions<BookingOptions> booking, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (p is null) return NotFound();
        if (string.IsNullOrEmpty(p.ReferralCode))
        {
            p.ReferralCode = await ReferralCodes.NewUniqueAsync(_db, ct);
            await _db.SaveChangesAsync(ct);
        }
        var invited = await _db.Profiles.CountAsync(x => x.ReferredBy == userId, ct);
        var rewarded = await _db.Profiles.CountAsync(x => x.ReferredBy == userId && x.ReferralRewarded, ct);
        return Ok(new
        {
            code = p.ReferralCode, invited, rewarded,
            bonusPerFriend = booking.Value.ReferralBonus,
            earned = rewarded * booking.Value.ReferralBonus
        });
    }

    /// <summary>Telegram'ni ulash: bot havolasi (/start KOD) qaytadi.</summary>
    [HttpPost("telegram/link")]
    public async Task<IActionResult> TelegramLink([FromServices] Microsoft.Extensions.Options.IOptions<DineMaster.Infrastructure.Services.TelegramOptions> tg, CancellationToken ct)
    {
        if (!tg.Value.Enabled)
            return StatusCode(503, new { message = "Telegram bot hali sozlanmagan. Administrator bilan bog'laning." });
        var userId = User.GetUserIdOrThrow();
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (p is null) return NotFound();
        p.TelegramLinkCode = ReferralCodes.Random(10);
        await _db.SaveChangesAsync(ct);
        return Ok(new { url = $"https://t.me/{tg.Value.BotUsername}?start={p.TelegramLinkCode}" });
    }

    [HttpDelete("telegram")]
    public async Task<IActionResult> TelegramUnlink(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var p = await _db.Profiles.FirstOrDefaultAsync(x => x.Id == userId, ct);
        if (p is null) return NotFound();
        p.TelegramChatId = null;
        p.TelegramLinkCode = null;
        await _db.SaveChangesAsync(ct);
        return Ok(new { telegramLinked = false });
    }

    private static object MapProfile(Domain.Entities.Profile p) => new
    {
        id = p.Id, email = p.Email, fullName = p.FullName,
        phone = p.Phone, avatarUrl = p.AvatarUrl, bio = p.Bio,
        yumsBalance = p.YumsBalance, preferredLanguage = p.PreferredLanguage,
        dietaryPreferences = p.DietaryPreferences, createdAt = p.CreatedAt,
        telegramLinked = p.TelegramChatId != null
    };
}

public record UpdateProfileRequest(
    string? FullName, string? Phone, string? AvatarUrl,
    string? Bio, string? PreferredLanguage, List<string>? DietaryPreferences);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
