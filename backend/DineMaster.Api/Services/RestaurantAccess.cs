using System.Security.Claims;
using DineMaster.Application.Common;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Services;

/// <summary>Restoranga ruxsat darajasi (o'sish tartibida).</summary>
public enum AccessLevel
{
    None = 0,
    /// <summary>Bronlar, kutish ro'yxati, "Keldi/Kelmadi", stol biriktirish.</summary>
    Host = 1,
    /// <summary>+ menyu, stollar, sharhlarga javob, hisobotlar, ish vaqti/slotlar.</summary>
    Manager = 2,
    /// <summary>+ restoranni o'chirish, xodimlarni boshqarish. Superadmin ham shu darajada.</summary>
    Owner = 3
}

/// <summary>Joriy foydalanuvchining restoranlarga ruxsatini aniqlaydi (egasi, xodim yoki superadmin).</summary>
public class RestaurantAccess
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly Dictionary<Guid, AccessLevel> _cache = new();

    public RestaurantAccess(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    private ClaimsPrincipal? User => _http.HttpContext?.User;
    private Guid? UserId => Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
    private bool IsSuperadmin => User?.IsInRole(Roles.Superadmin) == true;

    public async Task<AccessLevel> LevelAsync(Guid restaurantId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(restaurantId, out var cached)) return cached;
        var level = AccessLevel.None;
        if (IsSuperadmin)
        {
            level = AccessLevel.Owner;
        }
        else if (UserId is Guid uid)
        {
            var info = await _db.Restaurants.Where(r => r.Id == restaurantId)
                .Select(r => new
                {
                    r.OwnerId,
                    StaffRole = r.Staff.Where(s => s.UserId == uid).Select(s => s.Role).FirstOrDefault()
                })
                .FirstOrDefaultAsync(ct);
            if (info is not null)
                level = info.OwnerId == uid ? AccessLevel.Owner
                    : info.StaffRole == RestaurantStaff.Manager ? AccessLevel.Manager
                    : info.StaffRole == RestaurantStaff.Host ? AccessLevel.Host
                    : AccessLevel.None;
        }
        _cache[restaurantId] = level;
        return level;
    }

    /// <summary>Daraja yetarli bo'lmasa — 403 (ForbiddenAccessException).</summary>
    public async Task RequireAsync(Guid restaurantId, AccessLevel min, CancellationToken ct = default)
    {
        if (await LevelAsync(restaurantId, ct) < min)
            throw new ForbiddenAccessException(min switch
            {
                AccessLevel.Owner => "Bu amal faqat restoran egasi uchun.",
                AccessLevel.Manager => "Bu amal uchun menejer huquqi kerak.",
                _ => "Bu restoranga ruxsatingiz yo'q."
            });
    }

    /// <summary>Foydalanuvchi kamida <paramref name="min"/> darajada kira oladigan restoranlar.</summary>
    public async Task<Dictionary<Guid, AccessLevel>> AccessibleAsync(AccessLevel min, CancellationToken ct = default)
    {
        if (IsSuperadmin)
            return await _db.Restaurants.Select(r => r.Id).ToDictionaryAsync(id => id, _ => AccessLevel.Owner, ct);
        if (UserId is not Guid uid) return new();

        var result = await _db.Restaurants.Where(r => r.OwnerId == uid)
            .Select(r => r.Id).ToDictionaryAsync(id => id, _ => AccessLevel.Owner, ct);
        var staff = await _db.RestaurantStaff.Where(s => s.UserId == uid).ToListAsync(ct);
        foreach (var s in staff)
        {
            var lvl = s.Role == RestaurantStaff.Manager ? AccessLevel.Manager : AccessLevel.Host;
            if (!result.ContainsKey(s.RestaurantId)) result[s.RestaurantId] = lvl;
        }
        return result.Where(kv => kv.Value >= min).ToDictionary(kv => kv.Key, kv => kv.Value);
    }

    public static string Name(AccessLevel l) => l switch
    {
        AccessLevel.Owner => "owner", AccessLevel.Manager => "manager", AccessLevel.Host => "host", _ => "none"
    };
}
