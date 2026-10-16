using DineMaster.Api.Extensions;
using DineMaster.Api.Services;
using DineMaster.Application.Abstractions;
using DineMaster.Application.DTOs.Reservations;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

/// <summary>
/// Restoran boshqaruvi (egasi, menejer, host). Ruxsat darajasi har bir restoran uchun
/// RestaurantAccess orqali tekshiriladi.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Roles = "admin,superadmin,staff")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBookingService _booking;
    private readonly AuditLogger _audit;
    private readonly ReportService _reports;
    private readonly RestaurantAccess _access;

    public AdminController(AppDbContext db, IBookingService booking, AuditLogger audit, ReportService reports, RestaurantAccess access)
    {
        _db = db; _booking = booking; _audit = audit; _reports = reports; _access = access;
    }

    // ───────────────────────────── Restoranlar ─────────────────────────────

    /// <summary>Foydalanuvchi boshqaradigan restoranlar (egasi yoki xodimi), har biri ruxsat darajasi bilan.</summary>
    [HttpGet("restaurants")]
    public async Task<IActionResult> MyRestaurants(CancellationToken ct)
    {
        var access = await _access.AccessibleAsync(AccessLevel.Host, ct);
        var ids = access.Keys.ToList();
        var restaurants = await _db.Restaurants.Where(r => ids.Contains(r.Id)).OrderBy(r => r.CreatedAt).ToListAsync(ct);
        return Ok(restaurants.Select(r => MapRestaurant(r, access[r.Id])));
    }

    // ───────────────────────────── Bronlar ─────────────────────────────

    [HttpGet("reservations")]
    public async Task<IActionResult> MyReservations(
        [FromQuery] string? status, [FromQuery] DateOnly? date, [FromQuery] Guid? restaurantId,
        [FromQuery] int page = 1, [FromQuery] int size = 30, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 500);
        var ids = (await _access.AccessibleAsync(AccessLevel.Host, ct)).Keys.ToList();

        var q = _db.Reservations
            .Where(r => ids.Contains(r.RestaurantId))
            .Include(r => r.User).ThenInclude(u => u!.Profile)
            .Include(r => r.Restaurant)
            .Include(r => r.Table)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (date.HasValue) q = q.Where(r => r.Date == date.Value);
        if (restaurantId is Guid rid) q = q.Where(r => r.RestaurantId == rid);

        q = q.OrderByDescending(r => r.Date).ThenByDescending(r => r.Time);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);

        return Ok(new { total, page, size, items = items.Select(r => new {
            id = r.Id, date = r.Date, time = r.Time, guestsCount = r.GuestsCount, status = r.Status,
            restaurantId = r.RestaurantId, restaurantName = r.Restaurant?.Name,
            tableId = r.TableId, tableLabel = r.Table?.Label,
            userId = r.UserId, userFullName = r.User?.Profile?.FullName, userPhone = r.User?.Profile?.Phone,
            depositPaid = r.DepositPaid, depositAmount = r.DepositAmount, depositOutcome = r.DepositOutcome,
            specialRequests = r.SpecialRequests, isNoShow = r.IsNoShow,
            preOrderTotal = r.PreOrderTotal, preOrderDiscount = r.PreOrderDiscount,
            yumsUsed = r.YumsUsed, totalDiscount = r.TotalDiscount, restaurantDiscount = r.RestaurantDiscount,
            cancelledBy = r.CancelledBy, cancelReason = r.CancelReason, isLateCancellation = r.IsLateCancellation,
            createdAt = r.CreatedAt, updatedAt = r.UpdatedAt
        })});
    }

    private async Task<Reservation> LoadReservationAsync(Guid id, AccessLevel min, CancellationToken ct)
    {
        var res = await _db.Reservations.Include(r => r.User).ThenInclude(u => u!.Profile)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DineMaster.Application.Common.NotFoundException("Bron topilmadi.");
        await _access.RequireAsync(res.RestaurantId, min, ct);
        return res;
    }

    private async Task LogReservationAsync(Guid id, string action, string what, CancellationToken ct)
    {
        _db.ChangeTracker.Clear();
        var info = await _db.Reservations.Where(r => r.Id == id)
            .Select(r => new { r.RestaurantId, r.Date, r.Time, Guest = r.User!.Profile!.FullName })
            .FirstAsync(ct);
        _audit.Log("reservation", id, info.RestaurantId, action, $"{what} — {info.Guest}, {info.Date:dd.MM.yyyy} {info.Time:HH:mm}");
        await _db.SaveChangesAsync(ct);
    }

    [HttpPut("reservations/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest req, CancellationToken ct)
    {
        await LoadReservationAsync(id, AccessLevel.Host, ct);
        // Yums, depozit va mijozga xabar — BookingService ichida
        var status = await _booking.ChangeStatusAsync(id, req.Status, User.GetUserIdOrThrow(), req.Reason, ct);
        await LogReservationAsync(id, "status", $"Bron holati: {status}" + (string.IsNullOrWhiteSpace(req.Reason) ? "" : $" ({req.Reason})"), ct);
        return Ok(new { id, status });
    }

    [HttpPost("reservations/{id:guid}/no-show")]
    public async Task<IActionResult> MarkNoShow(Guid id, CancellationToken ct)
    {
        await LoadReservationAsync(id, AccessLevel.Host, ct);
        var status = await _booking.ChangeStatusAsync(id, BookingRules.NoShow, User.GetUserIdOrThrow(), null, ct);
        await LogReservationAsync(id, "status", $"Bron holati: {status}", ct);
        return Ok(new { id, status });
    }

    /// <summary>Bronga stol biriktirish (tableId = null — olib tashlash).</summary>
    [HttpPut("reservations/{id:guid}/table")]
    public async Task<IActionResult> AssignTable(Guid id, [FromBody] AssignTableRequest req, CancellationToken ct)
    {
        await LoadReservationAsync(id, AccessLevel.Host, ct);
        var label = await _booking.AssignTableAsync(id, req.TableId, ct);
        await LogReservationAsync(id, "table", label is null ? "Stol olib tashlandi" : $"Stol biriktirildi: {label}", ct);
        return Ok(new { id, tableId = req.TableId, tableLabel = label });
    }

    [HttpPost("reservations/{id:guid}/deposit-paid")]
    public async Task<IActionResult> MarkDepositPaid(Guid id, CancellationToken ct)
    {
        var res = await LoadReservationAsync(id, AccessLevel.Host, ct);
        res.DepositPaid = true;
        res.UpdatedAt = DateTime.UtcNow;
        _audit.Log("reservation", res.Id, res.RestaurantId, "deposit_paid",
            $"Depozit qo'lda to'langan deb belgilandi: {res.DepositAmount:N0} so'm ({res.Date:dd.MM.yyyy} {res.Time:HH:mm})");
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, depositPaid = res.DepositPaid });
    }

    /// <summary>Bekor qilingan bronning depoziti mijozga qaytarildi (naqd yoki provayder kabinetida).</summary>
    [HttpPost("reservations/{id:guid}/deposit-refunded")]
    public async Task<IActionResult> MarkDepositRefunded(Guid id, CancellationToken ct)
    {
        var res = await LoadReservationAsync(id, AccessLevel.Manager, ct);
        if (res.DepositOutcome != "refund_requested")
            return BadRequest(new { message = "Bu bron bo'yicha qaytarish so'ralmagan." });

        res.DepositOutcome = "refunded";
        var payments = await _db.Payments.Where(p => p.ReservationId == id && p.State == Payment.StatePaid).ToListAsync(ct);
        foreach (var p in payments) { p.State = Payment.StateRefunded; p.CancelledAt = DateTime.UtcNow; }
        _db.Notifications.Add(new Notification
        {
            UserId = res.UserId, Type = "deposit_refunded", Title = "Depozit qaytarildi",
            Body = $"{res.DepositAmount:N0} so'm qaytarildi ({res.Date:dd.MM.yyyy} {res.Time:HH:mm} bron).", Link = "/profile",
            ReservationId = res.Id
        });
        _audit.Log("reservation", res.Id, res.RestaurantId, "deposit_refunded", $"Depozit qaytarildi: {res.DepositAmount:N0} so'm");
        await _db.SaveChangesAsync(ct);
        return Ok(new { id, depositOutcome = res.DepositOutcome });
    }

    // ───────────────────────────── Statistika va hisobotlar ─────────────────────────────

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var ids = (await _access.AccessibleAsync(AccessLevel.Host, ct)).Keys.ToList();
        var today = BusinessTime.Today();
        var q = _db.Reservations.Where(r => ids.Contains(r.RestaurantId));
        return Ok(new
        {
            totalReservations = await q.CountAsync(ct),
            todayReservations = await q.CountAsync(r => r.Date == today, ct),
            confirmed = await q.CountAsync(r => r.Status == BookingRules.Confirmed, ct),
            refundsPending = await q.CountAsync(r => r.DepositOutcome == "refund_requested", ct),
            restaurantCount = ids.Count
        });
    }

    /// <summary>Hisobot: period = day | week | month | year, date — davr ichidagi istalgan kun.</summary>
    [HttpGet("reports")]
    public async Task<IActionResult> Report(
        [FromQuery] Guid? restaurantId, [FromQuery] string period = "day", [FromQuery] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var rid = await ResolveRestaurantAsync(restaurantId, ct);
        if (rid is null) return NotFound(new { message = "Restoran topilmadi." });
        return Ok(await _reports.BuildAsync(rid.Value, period, date ?? BusinessTime.Today(), ct));
    }

    [HttpGet("reports/pdf")]
    public async Task<IActionResult> ReportPdfFile(
        [FromQuery] Guid? restaurantId, [FromQuery] string period = "day", [FromQuery] DateOnly? date = null,
        CancellationToken ct = default)
    {
        var rid = await ResolveRestaurantAsync(restaurantId, ct);
        if (rid is null) return NotFound(new { message = "Restoran topilmadi." });
        var report = await _reports.BuildAsync(rid.Value, period, date ?? BusinessTime.Today(), ct);
        return File(ReportPdf.Generate(report), "application/pdf", $"hisobot-{period}-{report.From:yyyyMMdd}.pdf");
    }

    /// <summary>restaurantId berilsa — kamida menejer ruxsati tekshiriladi; aks holda birinchi mos restoran.</summary>
    private async Task<Guid?> ResolveRestaurantAsync(Guid? restaurantId, CancellationToken ct)
    {
        if (restaurantId is Guid id)
        {
            await _access.RequireAsync(id, AccessLevel.Manager, ct);
            return id;
        }
        var ids = (await _access.AccessibleAsync(AccessLevel.Manager, ct)).Keys.ToList();
        return await _db.Restaurants.Where(r => ids.Contains(r.Id)).OrderBy(r => r.CreatedAt)
            .Select(r => (Guid?)r.Id).FirstOrDefaultAsync(ct);
    }

    private static object MapRestaurant(Restaurant r, AccessLevel level) => new
    {
        id = r.Id, name = r.Name, description = r.Description,
        cuisineType = r.CuisineType, priceRange = r.PriceRange,
        address = r.Address, city = r.City, phone = r.Phone, website = r.Website,
        latitude = r.Latitude, longitude = r.Longitude,
        rating = r.Rating, reviewCount = r.ReviewCount,
        foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
        imagesUrl = r.ImagesUrl, amenities = r.Amenities, policies = r.Policies,
        openingHours = r.OpeningHours, specialHours = r.SpecialHours,
        totalSlots = r.TotalSlots, hourlySlots = r.HourlySlots,
        pendingChanges = RestaurantChangeSet.FromJson(r.PendingChanges),
        pendingChangesSubmittedAt = r.PendingChangesSubmittedAt,
        depositRequired = r.DepositRequired, depositPerGuest = r.DepositPerGuest,
        discount = r.Discount, discountTimeStart = r.DiscountTimeStart, discountTimeEnd = r.DiscountTimeEnd,
        isApproved = r.IsApproved, isSuspended = r.IsSuspended, rejectionReason = r.RejectionReason,
        ownerId = r.OwnerId, createdAt = r.CreatedAt, updatedAt = r.UpdatedAt,
        accessLevel = RestaurantAccess.Name(level)
    };
}

public record UpdateStatusRequest(string Status, string? Reason = null);
