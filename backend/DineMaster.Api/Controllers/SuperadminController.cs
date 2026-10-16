using DineMaster.Api.Extensions;
using DineMaster.Api.Services;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

/// <summary>
/// Platforma boshqaruvi: foydalanuvchilar, restoranlar, zallar, bronlar, sharhlar, to'lovlar,
/// hamkorlik arizalari, moderatsiya, faoliyat jurnali, ommaviy xabarlar.
/// (Bronlar statusini o'zgartirish — /api/admin/reservations/{id}/status, sharhni o'chirish —
/// DELETE /api/reviews/{id}: ular superadminga ham ruxsat beradi.)
/// </summary>
[ApiController]
[Route("api/superadmin")]
[Authorize(Roles = "superadmin")]
public class SuperadminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;

    public SuperadminController(AppDbContext db, AuditLogger audit)
    {
        _db = db;
        _audit = audit;
    }

    // ───────────────────────────── Statistika ─────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var today = BusinessTime.Today();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var weekAgo = DateTime.UtcNow.AddDays(-7);
        var monthRes = _db.Reservations.Where(r => r.Date >= monthStart && r.Date <= today);

        return Ok(new
        {
            totalUsers = await _db.Users.CountAsync(ct),
            newUsersThisWeek = await _db.Users.CountAsync(u => u.CreatedAt >= weekAgo, ct),
            totalAdmins = await _db.UserRoles.CountAsync(r => r.Role == AppRole.Admin, ct),
            bannedUsers = await _db.Profiles.CountAsync(p => p.IsBanned, ct),
            totalRestaurants = await _db.Restaurants.CountAsync(ct),
            activeRestaurants = await _db.Restaurants.CountAsync(r => r.IsApproved && !r.IsSuspended, ct),
            pendingRestaurants = await _db.Restaurants.CountAsync(r => !r.IsApproved, ct),
            pendingChanges = await _db.Restaurants.CountAsync(r => r.PendingChanges != null, ct),
            totalVenues = await _db.Venues.CountAsync(ct),
            pendingVenues = await _db.Venues.CountAsync(v => !v.IsApproved, ct),
            pendingVenueReservations = await _db.VenueReservations.CountAsync(v => v.Status == "pending", ct),
            pendingPartnerApplications = await _db.PartnerApplications.CountAsync(a => a.Status == PartnerApplication.Pending, ct),
            totalReservations = await _db.Reservations.CountAsync(ct),
            totalReservationsToday = await _db.Reservations.CountAsync(r => r.Date == today, ct),
            reservationsThisMonth = await monthRes.CountAsync(ct),
            completedThisMonth = await monthRes.CountAsync(r => r.Status == BookingRules.Completed, ct),
            cancelledThisMonth = await monthRes.CountAsync(r => r.Status == BookingRules.Cancelled, ct),
            noShowThisMonth = await monthRes.CountAsync(r => r.Status == BookingRules.NoShow, ct),
            depositsPaidTotal = await _db.Reservations.Where(r => r.DepositPaid && r.DepositAmount > 0 && r.DepositOutcome != "refunded").SumAsync(r => (decimal?)r.DepositAmount, ct) ?? 0,
            totalYumsInCirculation = await _db.Profiles.SumAsync(p => p.YumsBalance, ct)
        });
    }

    // ───────────────────────────── Foydalanuvchilar ─────────────────────────────

    /// <summary>
    /// role: user | admin | superadmin | banned (bo'sh — hammasi).
    /// search: ism yoki email; adminlar uchun restoran nomi bo'yicha ham.
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> Users(
        [FromQuery] string? search, [FromQuery] string? role,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.Profiles.AsQueryable();

        switch (role?.ToLowerInvariant())
        {
            case "superadmin":
                q = q.Where(p => p.User!.Roles.Any(r => r.Role == AppRole.Superadmin));
                break;
            case "admin":
                q = q.Where(p => p.User!.Roles.Any(r => r.Role == AppRole.Admin) &&
                                 !p.User.Roles.Any(r => r.Role == AppRole.Superadmin));
                break;
            case "user":
                q = q.Where(p => !p.User!.Roles.Any(r => r.Role == AppRole.Admin || r.Role == AppRole.Superadmin));
                break;
            case "staff":
                q = q.Where(p => p.User!.Roles.Any(r => r.Role == AppRole.Staff));
                break;
            case "banned":
                q = q.Where(p => p.IsBanned);
                break;
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(p => p.Email.ToLower().Contains(s) || p.FullName.ToLower().Contains(s) ||
                             (p.Phone != null && p.Phone.Contains(s)) ||
                             _db.Restaurants.Any(r => r.OwnerId == p.Id && r.Name.ToLower().Contains(s)));
        }

        var total = await q.CountAsync(ct);
        var profiles = await q.Include(p => p.User).ThenInclude(u => u!.Roles)
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size).Take(size)
            .ToListAsync(ct);

        var ids = profiles.Select(p => p.Id).ToList();
        var restaurants = await _db.Restaurants.Where(r => r.OwnerId != null && ids.Contains(r.OwnerId.Value))
            .Select(r => new { OwnerId = r.OwnerId!.Value, r.Id, r.Name, r.IsApproved, r.IsSuspended })
            .ToListAsync(ct);
        var bookingCounts = await _db.Reservations.Where(r => ids.Contains(r.UserId))
            .GroupBy(r => r.UserId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        return Ok(new
        {
            total, page, size,
            items = profiles.Select(p => new
            {
                id = p.Id, email = p.Email, fullName = p.FullName, phone = p.Phone,
                yumsBalance = p.YumsBalance, isBanned = p.IsBanned,
                emailConfirmed = p.User?.EmailConfirmed ?? false,
                roles = RolesOf(p),
                restaurants = restaurants.Where(r => r.OwnerId == p.Id)
                    .Select(r => new { r.Id, r.Name, r.IsApproved, r.IsSuspended }),
                reservationsCount = bookingCounts.GetValueOrDefault(p.Id),
                createdAt = p.CreatedAt
            })
        });
    }

    /// <summary>Foydalanuvchi kartochkasi: bronlari, yums tarixi, restoranlari.</summary>
    [HttpGet("users/{id:guid}")]
    public async Task<IActionResult> UserDetail(Guid id, CancellationToken ct)
    {
        var p = await _db.Profiles.Include(x => x.User).ThenInclude(u => u!.Roles).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();

        var reservations = await _db.Reservations.Where(r => r.UserId == id)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Time).Take(30)
            .Select(r => new
            {
                r.Id, r.Date, r.Time, r.GuestsCount, r.Status, r.DepositAmount, r.DepositPaid,
                restaurantName = r.Restaurant!.Name
            }).ToListAsync(ct);
        var yums = await _db.YumsAudits.Where(a => a.UserId == id).OrderByDescending(a => a.CreatedAt).Take(30)
            .Select(a => new { a.Id, a.Amount, a.Reason, a.CreatedAt }).ToListAsync(ct);
        var restaurants = await _db.Restaurants.Where(r => r.OwnerId == id)
            .Select(r => new { r.Id, r.Name, r.City, r.IsApproved, r.IsSuspended }).ToListAsync(ct);

        return Ok(new
        {
            user = MapUser(p),
            emailConfirmed = p.User?.EmailConfirmed ?? false,
            noShowCount = reservations.Count(r => r.Status == BookingRules.NoShow),
            reservations, yums, restaurants
        });
    }

    [HttpPost("users/ban")]
    public Task<IActionResult> Ban([FromBody] UserActionRequest req, CancellationToken ct) => SetBanAsync(req.UserId, true, req.Reason, ct);

    [HttpPost("users/unban")]
    public Task<IActionResult> Unban([FromBody] UserActionRequest req, CancellationToken ct) => SetBanAsync(req.UserId, false, null, ct);

    [HttpPost("users/adjust-yums")]
    public async Task<IActionResult> AdjustYums([FromBody] UserActionRequest req, CancellationToken ct)
    {
        if (req.Amount == 0) return BadRequest(new { message = "Miqdor 0 bo'lmasligi kerak." });
        var profile = await LoadProfileAsync(req.UserId, ct);
        if (profile is null) return NotFound();

        var before = profile.YumsBalance;
        profile.YumsBalance = Math.Max(0, profile.YumsBalance + req.Amount);
        var applied = profile.YumsBalance - before;
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "qo'lda tuzatish" : req.Reason.Trim();
        _db.YumsAudits.Add(new YumsAudit { UserId = req.UserId, Amount = applied, Reason = $"Superadmin: {reason}", ChangedBy = User.GetUserId() });
        _db.Notifications.Add(new Notification
        {
            UserId = req.UserId, Type = "yums_adjusted",
            Title = applied >= 0 ? $"+{applied} yums" : $"{applied} yums",
            Body = $"Balansingiz o'zgartirildi: {reason}", Link = "/yums"
        });
        _audit.Log("user", req.UserId, null, "yums", $"Yums {applied:+#;-#;0}: {profile.Email} ({reason})",
            new AuditDiff().Add("Yums", before, profile.YumsBalance).Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(MapUser(profile));
    }

    [HttpPost("users/assign-role")]
    public Task<IActionResult> AssignRole([FromBody] UserActionRequest req, CancellationToken ct) => ChangeRoleAsync(req.UserId, req.Role ?? "admin", true, ct);

    [HttpPost("users/revoke-role")]
    public Task<IActionResult> RevokeRole([FromBody] UserActionRequest req, CancellationToken ct) => ChangeRoleAsync(req.UserId, req.Role ?? "admin", false, ct);

    /// <summary>Email kod yetib bormasa — superadmin emailni qo'lda tasdiqlaydi.</summary>
    [HttpPost("users/{id:guid}/confirm-email")]
    public async Task<IActionResult> ConfirmEmail(Guid id, CancellationToken ct)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound();
        user.EmailConfirmed = true;
        _audit.Log("user", id, null, "confirm_email", $"Email qo'lda tasdiqlandi: {user.Email}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, emailConfirmed = true });
    }

    private async Task<IActionResult> SetBanAsync(Guid userId, bool isBanned, string? reason, CancellationToken ct)
    {
        if (isBanned && userId == User.GetUserId())
            return BadRequest(new { message = "O'zingizni bloklay olmaysiz." });
        var profile = await LoadProfileAsync(userId, ct);
        if (profile is null) return NotFound();

        profile.IsBanned = isBanned;
        _audit.Log("user", userId, null, isBanned ? "ban" : "unban",
            (isBanned ? "Bloklandi: " : "Blokdan chiqarildi: ") + profile.Email + (string.IsNullOrWhiteSpace(reason) ? "" : $" ({reason})"));
        await _db.SaveChangesAsync(ct);
        return Ok(MapUser(profile));
    }

    private async Task<IActionResult> ChangeRoleAsync(Guid userId, string roleValue, bool assign, CancellationToken ct)
    {
        if (!Enum.TryParse<AppRole>(roleValue, true, out var role) || role == AppRole.User)
            return BadRequest(new { message = "Noma'lum rol." });
        // Superadmin o'zidan superadmin rolini olib qo'ya olmaydi (platforma boshqaruvsiz qolmasin)
        if (!assign && role == AppRole.Superadmin && userId == User.GetUserId())
            return BadRequest(new { message = "O'zingizdan superadmin rolini olib bo'lmaydi." });

        var profile = await LoadProfileAsync(userId, ct);
        if (profile?.User is null) return NotFound();

        var existing = profile.User.Roles.FirstOrDefault(r => r.Role == role);
        if (assign && existing is null)
            _db.UserRoles.Add(new UserRoleEntity { UserId = userId, Role = role });
        else if (!assign && existing is not null)
            _db.UserRoles.Remove(existing);
        else
            return Ok(MapUser(profile));

        _audit.Log("user", userId, null, assign ? "assign_role" : "revoke_role",
            $"{role.ToRoleString()} roli {(assign ? "berildi" : "olib tashlandi")}: {profile.Email}");
        await _db.SaveChangesAsync(ct);
        return Ok(MapUser(profile));
    }

    private Task<Profile?> LoadProfileAsync(Guid userId, CancellationToken ct)
        => _db.Profiles.Include(p => p.User).ThenInclude(u => u!.Roles).FirstOrDefaultAsync(p => p.Id == userId, ct);

    // ───────────────────────────── Restoranlar ─────────────────────────────

    [HttpGet("restaurants")]
    public async Task<IActionResult> Restaurants(CancellationToken ct)
    {
        var list = await _db.Restaurants.Include(r => r.Owner).ThenInclude(o => o!.Profile)
            .OrderByDescending(r => r.CreatedAt).ToListAsync(ct);
        return Ok(list.Select(MapRestaurant));
    }

    [HttpPost("restaurants/{id:guid}/approval")]
    public async Task<IActionResult> SetRestaurantApproval(Guid id, [FromBody] ApprovalRequest req, CancellationToken ct)
    {
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();

        r.IsApproved = req.Approve;
        r.IsSuspended = false;
        r.RejectionReason = req.Approve ? null : req.RejectionReason;
        NotifyOwner(r.OwnerId, req.Approve ? "Restoraningiz tasdiqlandi" : "Restoran rad etildi",
            req.Approve ? $"\"{r.Name}\" endi mijozlarga ko'rinadi." : $"\"{r.Name}\": {req.RejectionReason ?? "sabab ko'rsatilmagan"}");
        _audit.Log("restaurant", r.Id, r.Id, req.Approve ? "approve" : "reject",
            req.Approve ? $"Restoran tasdiqlandi: {r.Name}" : $"Restoran rad etildi: {r.Name} ({req.RejectionReason})");
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurant(r));
    }

    [HttpPost("restaurants/{id:guid}/suspend")]
    public Task<IActionResult> SuspendRestaurant(Guid id, CancellationToken ct) => SetRestaurantSuspensionAsync(id, true, ct);

    [HttpPost("restaurants/{id:guid}/unsuspend")]
    public Task<IActionResult> UnsuspendRestaurant(Guid id, CancellationToken ct) => SetRestaurantSuspensionAsync(id, false, ct);

    private async Task<IActionResult> SetRestaurantSuspensionAsync(Guid id, bool suspended, CancellationToken ct)
    {
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        r.IsSuspended = suspended;
        NotifyOwner(r.OwnerId, suspended ? "Restoran to'xtatildi" : "Restoran qayta faollashtirildi",
            suspended ? $"\"{r.Name}\" vaqtincha mijozlarga ko'rinmaydi." : $"\"{r.Name}\" yana mijozlarga ko'rinadi.");
        _audit.Log("restaurant", r.Id, r.Id, suspended ? "suspend" : "unsuspend",
            (suspended ? "Restoran to'xtatildi: " : "Restoran faollashtirildi: ") + r.Name);
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurant(r));
    }

    /// <summary>Restoranni boshqa egaga o'tkazish (yangi egaga admin roli avtomatik beriladi).</summary>
    [HttpPost("restaurants/{id:guid}/owner")]
    public async Task<IActionResult> ChangeOwner(Guid id, [FromBody] ChangeOwnerRequest req, CancellationToken ct)
    {
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        var owner = await LoadProfileAsync(req.UserId, ct);
        if (owner?.User is null) return BadRequest(new { message = "Foydalanuvchi topilmadi." });

        if (!owner.User.Roles.Any(x => x.Role == AppRole.Admin || x.Role == AppRole.Superadmin))
            _db.UserRoles.Add(new UserRoleEntity { UserId = owner.Id, Role = AppRole.Admin });
        var oldOwner = r.OwnerId;
        r.OwnerId = owner.Id;
        NotifyOwner(owner.Id, "Sizga restoran biriktirildi", $"\"{r.Name}\" endi sizning boshqaruvingizda.");
        _audit.Log("restaurant", r.Id, r.Id, "owner", $"Restoran egasi o'zgartirildi: {r.Name} → {owner.Email}",
            new AuditDiff().Add("Egasi", oldOwner, owner.Id).Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurant(r));
    }

    [HttpGet("restaurants/pending-changes")]
    public async Task<IActionResult> PendingChanges(CancellationToken ct)
    {
        var list = await _db.Restaurants.Where(r => r.PendingChanges != null)
            .OrderBy(r => r.PendingChangesSubmittedAt).ToListAsync(ct);
        return Ok(list.Select(r => new
        {
            id = r.Id, name = r.Name, ownerId = r.OwnerId, submittedAt = r.PendingChangesSubmittedAt,
            current = new
            {
                name = r.Name, description = r.Description, cuisineType = r.CuisineType,
                priceRange = r.PriceRange, address = r.Address, city = r.City,
                latitude = r.Latitude, longitude = r.Longitude, imagesUrl = r.ImagesUrl
            },
            pending = RestaurantChangeSet.FromJson(r.PendingChanges)
        }));
    }

    [HttpPost("restaurants/{id:guid}/changes/approve")]
    public async Task<IActionResult> ApproveChanges(Guid id, CancellationToken ct)
    {
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        var changes = RestaurantChangeSet.FromJson(r.PendingChanges);
        if (changes is null) return BadRequest(new { message = "Tasdiq kutayotgan o'zgarish yo'q." });

        changes.ApplyTo(r);
        r.PendingChanges = null;
        r.PendingChangesSubmittedAt = null;
        NotifyOwner(r.OwnerId, "O'zgarishlar tasdiqlandi", $"\"{r.Name}\" ma'lumotlari yangilandi.");
        _audit.Log("restaurant", r.Id, r.Id, "approve_changes", $"Moderatsiya: o'zgarishlar tasdiqlandi — {r.Name}");
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurant(r));
    }

    [HttpPost("restaurants/{id:guid}/changes/reject")]
    public async Task<IActionResult> RejectChanges(Guid id, [FromBody] RejectChangesRequest req, CancellationToken ct)
    {
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        if (r.PendingChanges is null) return BadRequest(new { message = "Tasdiq kutayotgan o'zgarish yo'q." });

        r.PendingChanges = null;
        r.PendingChangesSubmittedAt = null;
        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "sabab ko'rsatilmagan" : req.Reason.Trim();
        NotifyOwner(r.OwnerId, "O'zgarishlar rad etildi", $"\"{r.Name}\": {reason}");
        _audit.Log("restaurant", r.Id, r.Id, "reject_changes", $"Moderatsiya: o'zgarishlar rad etildi — {r.Name} ({reason})");
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurant(r));
    }

    // ───────────────────────────── Zallar ─────────────────────────────

    [HttpGet("venues")]
    public async Task<IActionResult> Venues(CancellationToken ct)
    {
        var venues = await _db.Venues.OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
        return Ok(venues.Select(MapVenue));
    }

    [HttpPost("venues/{id:guid}/approval")]
    public async Task<IActionResult> SetVenueApproval(Guid id, [FromBody] ApprovalRequest req, CancellationToken ct)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (venue is null) return NotFound();

        venue.IsApproved = req.Approve;
        venue.IsSuspended = false;
        venue.RejectionReason = req.Approve ? null : req.RejectionReason;
        NotifyOwner(venue.OwnerId, req.Approve ? "Zalingiz tasdiqlandi" : "Zal rad etildi",
            req.Approve ? $"\"{venue.Name}\" endi mijozlarga ko'rinadi." : $"\"{venue.Name}\": {req.RejectionReason ?? "sabab ko'rsatilmagan"}");
        _audit.Log("venue", venue.Id, null, req.Approve ? "approve" : "reject",
            (req.Approve ? "Zal tasdiqlandi: " : "Zal rad etildi: ") + venue.Name);
        await _db.SaveChangesAsync(ct);
        return Ok(MapVenue(venue));
    }

    [HttpPost("venues/{id:guid}/suspend")]
    public Task<IActionResult> SuspendVenue(Guid id, CancellationToken ct) => SetVenueSuspensionAsync(id, true, ct);

    [HttpPost("venues/{id:guid}/unsuspend")]
    public Task<IActionResult> UnsuspendVenue(Guid id, CancellationToken ct) => SetVenueSuspensionAsync(id, false, ct);

    private async Task<IActionResult> SetVenueSuspensionAsync(Guid id, bool suspended, CancellationToken ct)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (venue is null) return NotFound();
        venue.IsSuspended = suspended;
        _audit.Log("venue", venue.Id, null, suspended ? "suspend" : "unsuspend",
            (suspended ? "Zal to'xtatildi: " : "Zal faollashtirildi: ") + venue.Name);
        await _db.SaveChangesAsync(ct);
        return Ok(MapVenue(venue));
    }

    [HttpGet("venue-reservations")]
    public async Task<IActionResult> VenueReservations(
        [FromQuery] string? status, [FromQuery] Guid? venueId,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.VenueReservations.AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (venueId is Guid v) q = q.Where(r => r.VenueId == v);

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(r => new
            {
                id = r.Id, venueId = r.VenueId, venueName = r.Venue!.Name, userId = r.UserId,
                userFullName = r.User!.Profile!.FullName, userEmail = r.User.Email, userPhone = r.User.Profile.Phone,
                eventDate = r.EventDate, eventType = r.EventType, guestsCount = r.GuestsCount,
                totalPrice = r.TotalPrice, status = r.Status, specialRequests = r.SpecialRequests ?? r.Notes,
                createdAt = r.CreatedAt
            }).ToListAsync(ct);
        return Ok(new { total, page, size, items });
    }

    // ───────────────────────────── Bronlar, sharhlar, to'lovlar ─────────────────────────────

    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations(
        [FromQuery] Guid? restaurantId, [FromQuery] string? status,
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.Reservations.AsQueryable();
        if (restaurantId is Guid rid) q = q.Where(r => r.RestaurantId == rid);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (from is DateOnly f) q = q.Where(r => r.Date >= f);
        if (to is DateOnly t) q = q.Where(r => r.Date <= t);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r => r.User!.Profile!.FullName.ToLower().Contains(s) || r.User.Email.ToLower().Contains(s) ||
                             r.Restaurant!.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.Date).ThenByDescending(r => r.Time)
            .Skip((page - 1) * size).Take(size)
            .Select(r => new
            {
                id = r.Id, userId = r.UserId, userFullName = r.User!.Profile!.FullName, userEmail = r.User.Email,
                userPhone = r.User.Profile.Phone, restaurantId = r.RestaurantId, restaurantName = r.Restaurant!.Name,
                tableId = r.TableId, tableLabel = r.Table != null ? r.Table.Label : null,
                date = r.Date, time = r.Time, guestsCount = r.GuestsCount, status = r.Status,
                specialRequests = r.SpecialRequests, yumsUsed = r.YumsUsed, totalDiscount = r.TotalDiscount,
                depositAmount = r.DepositAmount, depositPaid = r.DepositPaid, isNoShow = r.IsNoShow,
                preOrderTotal = r.PreOrderTotal, createdAt = r.CreatedAt, updatedAt = r.UpdatedAt
            }).ToListAsync(ct);
        return Ok(new { total, page, size, items });
    }

    [HttpGet("reviews")]
    public async Task<IActionResult> Reviews(
        [FromQuery] Guid? restaurantId, [FromQuery] int? maxRating, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.Reviews.AsQueryable();
        if (restaurantId is Guid rid) q = q.Where(r => r.RestaurantId == rid);
        if (maxRating is int m) q = q.Where(r => (r.FoodRating + r.ServiceRating + r.InteriorRating) <= m * 3);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(r => (r.Comment != null && r.Comment.ToLower().Contains(s)) ||
                             r.User!.Profile!.FullName.ToLower().Contains(s) || r.Restaurant!.Name.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(r => new
            {
                id = r.Id, restaurantId = r.RestaurantId, restaurantName = r.Restaurant!.Name,
                userId = r.UserId, userFullName = r.User!.Profile!.FullName,
                foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
                rating = Math.Round((r.FoodRating + r.ServiceRating + r.InteriorRating) / 3m, 1),
                comment = r.Comment, ownerResponse = r.OwnerResponse, createdAt = r.CreatedAt
            }).ToListAsync(ct);
        return Ok(new { total, page, size, items });
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments(
        [FromQuery] string? provider, [FromQuery] int? state, [FromQuery] DateTime? from, [FromQuery] DateTime? to,
        [FromQuery] bool? refundPending,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.Payments.AsQueryable();
        if (!string.IsNullOrWhiteSpace(provider)) q = q.Where(p => p.Provider == provider);
        if (state is int st) q = q.Where(p => p.State == st);
        if (from is DateTime f) q = q.Where(p => p.CreatedAt >= f.ToUniversalTime());
        if (to is DateTime t) q = q.Where(p => p.CreatedAt <= t.ToUniversalTime());
        if (refundPending == true) q = q.Where(p => p.RefundRequestedAt != null && p.State == Payment.StatePaid);

        var total = await q.CountAsync(ct);
        var refundsPending = await _db.Payments.CountAsync(p => p.RefundRequestedAt != null && p.State == Payment.StatePaid, ct);
        var paid = await q.Where(p => p.State == Payment.StatePaid).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var refunded = await q.Where(p => p.State == Payment.StateRefunded).SumAsync(p => (decimal?)p.Amount, ct) ?? 0;
        var items = await q.OrderByDescending(p => p.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(p => new
            {
                id = p.Id, number = p.Number, provider = p.Provider, providerTransactionId = p.ProviderTransactionId,
                amount = p.Amount, state = p.State, cancelReason = p.CancelReason,
                reservationId = p.ReservationId, restaurantName = p.Reservation!.Restaurant!.Name,
                reservationDate = p.Reservation.Date, userId = p.UserId,
                userFullName = _db.Profiles.Where(x => x.Id == p.UserId).Select(x => x.FullName).FirstOrDefault(),
                createdAt = p.CreatedAt, performedAt = p.PerformedAt, cancelledAt = p.CancelledAt,
                refundRequestedAt = p.RefundRequestedAt, reservationStatus = p.Reservation.Status
            }).ToListAsync(ct);
        return Ok(new { total, page, size, totals = new { paid, refunded, refundsPending }, items });
    }

    /// <summary>Provayder kabinetida (Click/Payme) pul qaytarilgach — qaytarildi deb belgilash.</summary>
    [HttpPost("payments/{id:guid}/refunded")]
    public async Task<IActionResult> MarkRefunded(Guid id, CancellationToken ct)
    {
        var payment = await _db.Payments.Include(p => p.Reservation).FirstOrDefaultAsync(p => p.Id == id, ct);
        if (payment is null) return NotFound();
        if (payment.State != Payment.StatePaid) return BadRequest(new { message = "Faqat to'langan to'lovni qaytarish mumkin." });

        payment.State = Payment.StateRefunded;
        payment.CancelledAt = DateTime.UtcNow;
        payment.RefundRequestedAt ??= DateTime.UtcNow;
        if (payment.Reservation is Reservation r)
        {
            if (BookingRules.IsActive(r.Status)) r.DepositPaid = false;
            else r.DepositOutcome = "refunded";
        }
        _db.Notifications.Add(new Notification
        {
            UserId = payment.UserId, Type = "deposit_refunded", Title = "Depozit qaytarildi",
            Body = $"{payment.Amount:N0} so'm ({payment.Provider}) qaytarildi.", Link = "/profile", ReservationId = payment.ReservationId
        });
        _audit.Log("payment", payment.Id, payment.Reservation?.RestaurantId, "refunded", $"To'lov qaytarildi: {payment.Amount:N0} so'm ({payment.Provider})");
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, state = payment.State });
    }

    [HttpGet("yums-audit")]
    public async Task<IActionResult> YumsAudit(CancellationToken ct)
    {
        var audit = await _db.YumsAudits.OrderByDescending(a => a.CreatedAt).Take(500).ToListAsync(ct);
        var userIds = audit.Select(a => a.UserId).Distinct().ToList();
        var profiles = await _db.Profiles.Where(p => userIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.FullName, ct);
        return Ok(audit.Select(a => new
        {
            id = a.Id, userId = a.UserId,
            userFullName = profiles.TryGetValue(a.UserId, out var fullName) ? fullName : null,
            amount = a.Amount, reason = a.Reason, adminId = a.ChangedBy, createdAt = a.CreatedAt
        }));
    }

    // ───────────────────────────── Hamkorlik arizalari ─────────────────────────────

    [HttpGet("partner-applications")]
    public async Task<IActionResult> PartnerApplications([FromQuery] string? status, CancellationToken ct)
    {
        var q = _db.PartnerApplications.Include(a => a.User).ThenInclude(u => u!.Profile).AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(a => a.Status == status);
        var list = await q.OrderByDescending(a => a.CreatedAt).Take(500).ToListAsync(ct);
        return Ok(list.Select(PartnerApplicationsController.Map));
    }

    [HttpPost("partner-applications/{id:guid}/approve")]
    public async Task<IActionResult> ApprovePartner(Guid id, CancellationToken ct)
    {
        var app = await _db.PartnerApplications.Include(a => a.User).ThenInclude(u => u!.Roles)
            .FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return NotFound();
        if (app.Status != PartnerApplication.Pending) return BadRequest(new { message = "Ariza allaqachon ko'rib chiqilgan." });

        if (app.User is not null && !app.User.Roles.Any(r => r.Role == AppRole.Admin || r.Role == AppRole.Superadmin))
            _db.UserRoles.Add(new UserRoleEntity { UserId = app.UserId, Role = AppRole.Admin });
        app.Status = PartnerApplication.Approved;
        app.ReviewedBy = User.GetUserId();
        app.ReviewedAt = DateTime.UtcNow;
        _db.Notifications.Add(new Notification
        {
            UserId = app.UserId, Type = "partner_approved", Title = "Arizangiz tasdiqlandi!",
            Body = $"Endi \"{app.RestaurantName}\" restoraningizni boshqaruv panelida qo'shishingiz mumkin.", Link = "/manager"
        });
        _audit.Log("partner_application", app.Id, null, "approve",
            $"Hamkorlik arizasi tasdiqlandi: {app.RestaurantName} ({app.User?.Email}) — admin roli berildi");
        await _db.SaveChangesAsync(ct);
        return Ok(PartnerApplicationsController.Map(app));
    }

    [HttpPost("partner-applications/{id:guid}/reject")]
    public async Task<IActionResult> RejectPartner(Guid id, [FromBody] RejectChangesRequest req, CancellationToken ct)
    {
        var app = await _db.PartnerApplications.Include(a => a.User).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (app is null) return NotFound();
        if (app.Status != PartnerApplication.Pending) return BadRequest(new { message = "Ariza allaqachon ko'rib chiqilgan." });

        var reason = string.IsNullOrWhiteSpace(req.Reason) ? "sabab ko'rsatilmagan" : req.Reason.Trim();
        app.Status = PartnerApplication.Rejected;
        app.RejectionReason = reason;
        app.ReviewedBy = User.GetUserId();
        app.ReviewedAt = DateTime.UtcNow;
        _db.Notifications.Add(new Notification
        {
            UserId = app.UserId, Type = "partner_rejected", Title = "Arizangiz rad etildi",
            Body = $"\"{app.RestaurantName}\": {reason}", Link = "/partner"
        });
        _audit.Log("partner_application", app.Id, null, "reject", $"Hamkorlik arizasi rad etildi: {app.RestaurantName} ({reason})");
        await _db.SaveChangesAsync(ct);
        return Ok(PartnerApplicationsController.Map(app));
    }

    // ───────────────────────────── Faoliyat jurnali ─────────────────────────────

    [HttpGet("audit-log")]
    public async Task<IActionResult> AuditLog(
        [FromQuery] Guid? restaurantId, [FromQuery] string? entityType, [FromQuery] string? actorRole,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 200);
        var q = _db.AuditLogs.AsQueryable();
        if (restaurantId is Guid rid) q = q.Where(a => a.RestaurantId == rid);
        if (!string.IsNullOrWhiteSpace(entityType)) q = q.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(actorRole)) q = q.Where(a => a.ActorRole == actorRole);
        if (from is DateTime f) q = q.Where(a => a.CreatedAt >= f.ToUniversalTime());
        if (to is DateTime t) q = q.Where(a => a.CreatedAt <= t.ToUniversalTime());
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            q = q.Where(a => a.Summary.ToLower().Contains(s));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderByDescending(a => a.CreatedAt).Skip((page - 1) * size).Take(size)
            .Select(a => new
            {
                id = a.Id, actorId = a.ActorId, actorRole = a.ActorRole,
                actorName = _db.Profiles.Where(p => p.Id == a.ActorId).Select(p => p.FullName).FirstOrDefault(),
                actorEmail = _db.Profiles.Where(p => p.Id == a.ActorId).Select(p => p.Email).FirstOrDefault(),
                entityType = a.EntityType, entityId = a.EntityId, restaurantId = a.RestaurantId,
                restaurantName = _db.Restaurants.Where(r => r.Id == a.RestaurantId).Select(r => r.Name).FirstOrDefault(),
                action = a.Action, summary = a.Summary, changes = a.Changes, createdAt = a.CreatedAt
            }).ToListAsync(ct);
        return Ok(new { total, page, size, items });
    }

    // ───────────────────────────── Ommaviy xabar ─────────────────────────────

    /// <summary>audience: all | users | admins</summary>
    [HttpPost("notifications/broadcast")]
    public async Task<IActionResult> Broadcast([FromBody] BroadcastRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Length > 200)
            return BadRequest(new { message = "Sarlavha 1–200 belgi bo'lishi kerak." });

        var q = _db.Users.Where(u => !u.Profile!.IsBanned);
        q = req.Audience?.ToLowerInvariant() switch
        {
            "admins" => q.Where(u => u.Roles.Any(r => r.Role == AppRole.Admin)),
            "users" => q.Where(u => !u.Roles.Any(r => r.Role == AppRole.Admin || r.Role == AppRole.Superadmin)),
            _ => q
        };
        var ids = await q.Select(u => u.Id).ToListAsync(ct);
        foreach (var id in ids)
            _db.Notifications.Add(new Notification
            {
                UserId = id, Type = "broadcast", Title = req.Title.Trim(), Body = req.Body?.Trim() ?? string.Empty,
                Link = string.IsNullOrWhiteSpace(req.Link) ? null : req.Link.Trim()
            });
        _audit.Log("notification", null, null, "broadcast", $"Ommaviy xabar ({req.Audience ?? "all"}, {ids.Count} kishi): {req.Title}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { sent = ids.Count });
    }

    // ───────────────────────────── Yordamchilar ─────────────────────────────

    private void NotifyOwner(Guid? ownerId, string title, string body)
    {
        if (ownerId is not Guid id) return;
        _db.Notifications.Add(new Notification { UserId = id, Type = "moderation", Title = title, Body = body, Link = "/manager" });
    }

    private static List<string> RolesOf(Profile p)
    {
        var roles = p.User?.Roles.Select(r => r.Role.ToRoleString()).Distinct().ToList() ?? new List<string>();
        if (roles.Count == 0) roles.Add(Roles.User);
        return roles;
    }

    private static object MapUser(Profile p) => new
    {
        id = p.Id, email = p.Email, fullName = p.FullName, phone = p.Phone,
        yumsBalance = p.YumsBalance, isBanned = p.IsBanned,
        emailConfirmed = p.User?.EmailConfirmed ?? false,
        roles = RolesOf(p), createdAt = p.CreatedAt
    };

    private static object MapRestaurant(Restaurant r) => new
    {
        id = r.Id, name = r.Name, description = r.Description,
        cuisineType = r.CuisineType, priceRange = r.PriceRange,
        address = r.Address, city = r.City, phone = r.Phone, website = r.Website,
        latitude = r.Latitude, longitude = r.Longitude,
        rating = r.Rating, reviewCount = r.ReviewCount,
        foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
        imagesUrl = r.ImagesUrl, amenities = r.Amenities, policies = r.Policies,
        openingHours = r.OpeningHours, totalSlots = r.TotalSlots, hourlySlots = r.HourlySlots,
        hasPendingChanges = r.PendingChanges != null,
        depositRequired = r.DepositRequired, depositPerGuest = r.DepositPerGuest,
        discount = r.Discount, discountTimeStart = r.DiscountTimeStart, discountTimeEnd = r.DiscountTimeEnd,
        isApproved = r.IsApproved, isSuspended = r.IsSuspended, rejectionReason = r.RejectionReason,
        ownerId = r.OwnerId, ownerName = r.Owner?.Profile?.FullName, ownerEmail = r.Owner?.Email,
        createdAt = r.CreatedAt, updatedAt = r.UpdatedAt
    };

    private static object MapVenue(Venue v) => new
    {
        id = v.Id, ownerId = v.OwnerId, name = v.Name, description = v.Description,
        venueType = v.VenueType, address = v.Address, city = v.City, phone = v.Phone,
        capacityMin = v.CapacityMin, capacityMax = v.CapacityMax,
        pricePerHead = v.PricePerHead, pricePerHour = v.PricePerHour, imagesUrl = v.ImagesUrl,
        rating = v.Rating, reviewCount = v.ReviewCount,
        isApproved = v.IsApproved, isSuspended = v.IsSuspended, rejectionReason = v.RejectionReason,
        openingHours = v.OpeningHours, createdAt = v.CreatedAt, updatedAt = v.UpdatedAt
    };
}

public record ApprovalRequest(bool Approve, string? RejectionReason = null);
public record RejectChangesRequest(string? Reason);
public record ChangeOwnerRequest(Guid UserId);
public record BroadcastRequest(string Title, string? Body, string? Link, string? Audience = "all");
public record UserActionRequest(Guid UserId, int Amount = 0, string? Reason = null, string? Role = null);
