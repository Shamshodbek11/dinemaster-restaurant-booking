using DineMaster.Api.Extensions;
using DineMaster.Api.Services;
using DineMaster.Application.Abstractions;
using DineMaster.Application.DTOs.Reservations;
using DineMaster.Application.Services;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/reservations")]
[Authorize]
public class ReservationsController : ControllerBase
{
    private readonly IBookingService _booking;
    private readonly AppDbContext _db;

    private readonly RestaurantAccess _access;

    public ReservationsController(IBookingService booking, AppDbContext db, RestaurantAccess access)
    {
        _booking = booking;
        _db = db;
        _access = access;
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        var userId = User.GetUserIdOrThrow();
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var q = _db.Reservations
            .Where(r => r.UserId == userId)
            .Include(r => r.Restaurant)
            .Include(r => r.Table)
            .Include(r => r.Items).ThenInclude(i => i.MenuItem)
            .OrderByDescending(r => r.Date).ThenByDescending(r => r.Time);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new { total, page, size, items = items.Select(MapReservation) });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReservationRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _booking.ProcessBookingAsync(userId, req, ct);
        return Ok(result);
    }

    /// <summary>Bron oldidan: depozit kerakmi va nima sababdan, email tasdiqlanganmi.</summary>
    [HttpGet("quote")]
    public async Task<IActionResult> Quote([FromQuery] Guid restaurantId, [FromQuery] int guests = 2, CancellationToken ct = default)
        => Ok(await _booking.GetQuoteAsync(User.GetUserIdOrThrow(), restaurantId, guests, ct));

    /// <summary>Kun bo'yicha bron qilish mumkin bo'lgan vaqtlar (ish vaqti va sig'im hisobga olingan).</summary>
    [HttpGet("/api/restaurants/{restaurantId:guid}/availability")]
    [AllowAnonymous]
    public async Task<IActionResult> Availability(Guid restaurantId, [FromQuery] DateOnly date, CancellationToken ct)
        => Ok(await _booking.GetAvailabilityAsync(restaurantId, date, ct));

    /// <summary>
    /// Bekor qilish. Depozit: <see cref="BookingOptions.FreeCancellationHours"/> soat oldin — qaytariladi,
    /// keyin — qaytarilmaydi.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelReservationRequest? req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var result = await _booking.CancelBookingAsync(userId, id, req?.Reason, ct);
        var message = result.DepositOutcome switch
        {
            "refunded" => "Bron bekor qilindi. Depozit qaytarildi.",
            "refund_requested" => "Bron bekor qilindi. Depozit qaytariladi (1–3 ish kuni).",
            "forfeited" => "Bron bekor qilindi. Kech bekor qilingani uchun depozit qaytarilmaydi.",
            _ => "Bron bekor qilindi."
        };
        return Ok(new { message, depositOutcome = result.DepositOutcome, isLateCancellation = result.IsLateCancellation });
    }

    /// <summary>Sana, vaqt, mehmonlar sonini o'zgartirish (bron boshlanishidan oldin).</summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Modify(Guid id, [FromBody] ModifyReservationRequest req, CancellationToken ct)
        => Ok(await _booking.ModifyBookingAsync(User.GetUserIdOrThrow(), id, req, ct));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var r = await _db.Reservations
            .Include(x => x.Restaurant)
            .Include(x => x.Table)
            .Include(x => x.Items).ThenInclude(i => i.MenuItem)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (r is null) return NotFound();
        return Ok(MapReservation(r));
    }

    /// <summary>
    /// Restoran egasi (yoki superadmin) uchun bandlik kalendari.
    /// month=yyyy-MM → kunlar bo'yicha statistika; date=yyyy-MM-dd → shu kungi bronlar.
    /// </summary>
    [HttpGet("calendar")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Calendar(
        [FromQuery] Guid restaurantId,
        [FromQuery] DateOnly? date,
        [FromQuery] string? month,
        CancellationToken ct)
    {
        var restaurant = await _db.Restaurants
            .Select(r => new { r.Id, r.TotalSlots, r.OwnerId })
            .FirstOrDefaultAsync(r => r.Id == restaurantId, ct);
        if (restaurant is null) return NotFound(new { message = "Restoran topilmadi." });
        await _access.RequireAsync(restaurant.Id, AccessLevel.Host, ct);

        if (!string.IsNullOrWhiteSpace(month))
        {
            if (!DateOnly.TryParseExact(month + "-01", "yyyy-MM-dd", out var start))
                return BadRequest(new { message = "Oy yyyy-MM formatida bo'lishi kerak." });

            var end = start.AddMonths(1);
            var stats = await _db.Reservations
                .Where(r => r.RestaurantId == restaurantId
                            && r.Date >= start
                            && r.Date < end
                            && r.Status != BookingRules.Cancelled)
                .GroupBy(r => r.Date)
                .Select(g => new
                {
                    date = g.Key,
                    count = g.Count(),
                    guests = g.Sum(r => r.GuestsCount)
                })
                .OrderBy(x => x.date)
                .ToListAsync(ct);

            return Ok(stats.Select(s => new
            {
                date = s.date.ToString("yyyy-MM-dd"),
                s.count,
                s.guests
            }));
        }

        if (!date.HasValue) return BadRequest(new { message = "date yoki month parametri kerak." });

        var reserved = await _db.Reservations
            .Where(r => r.RestaurantId == restaurantId && r.Date == date.Value
                        && (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending))
            .Select(r => new { r.Time, r.GuestsCount, tableId = r.TableId })
            .ToListAsync(ct);

        return Ok(new
        {
            date = date.Value,
            totalSlots = restaurant.TotalSlots,
            bookedCount = reserved.Count,
            slots = reserved
        });
    }

    private static object MapReservation(Domain.Entities.Reservation r) => new
    {
        id = r.Id,
        userId = r.UserId,
        restaurantId = r.RestaurantId,
        restaurantName = r.Restaurant?.Name,
        restaurantCity = r.Restaurant?.City,
        tableId = r.TableId,
        tableLabel = r.Table?.Label,
        date = r.Date,
        time = r.Time,
        guestsCount = r.GuestsCount,
        status = r.Status,
        specialRequests = r.SpecialRequests,
        yumsUsed = r.YumsUsed,
        yumsEarned = r.YumsEarned,
        totalDiscount = r.TotalDiscount,
        depositAmount = r.DepositAmount,
        depositPaid = r.DepositPaid,
        preOrderTotal = r.PreOrderTotal,
        isNoShow = r.IsNoShow,
        restaurantDiscount = r.RestaurantDiscount,
        preOrderDiscount = r.PreOrderDiscount,
        cancelledBy = r.CancelledBy,
        cancelReason = r.CancelReason,
        isLateCancellation = r.IsLateCancellation,
        depositOutcome = r.DepositOutcome,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt,
        items = r.Items?.Select(i => new {
            menuItemId = i.MenuItemId,
            name = i.MenuItem?.Name,
            menuItemName = i.MenuItem?.Name,
            quantity = i.Quantity,
            unitPrice = i.UnitPrice
        })
    };
}
