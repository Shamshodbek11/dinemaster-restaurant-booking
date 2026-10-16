using System.Data;
using DineMaster.Api.Services;
using DineMaster.Api.Extensions;
using DineMaster.Application.Common;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/venues")]
public class VenuesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;
    public VenuesController(AppDbContext db, AuditLogger audit) { _db = db; _audit = audit; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? city, CancellationToken ct = default)
    {
        var q = _db.Venues.Where(v => v.IsApproved && !v.IsSuspended);
        if (!string.IsNullOrWhiteSpace(city)) q = q.Where(v => v.City.ToLower().Contains(city.ToLower()));
        var items = await q.OrderByDescending(v => v.Rating).ToListAsync(ct);
        return Ok(items.Select(MapVenue));
    }

    [HttpPost]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Create([FromBody] CreateVenueRequest req, CancellationToken ct)
    {
        var error = ValidateVenue(req.Name, req.CapacityMin, req.CapacityMax, req.PricePerHead, req.PricePerHour);
        if (error is not null) return BadRequest(new { message = error });

        var venue = new Venue
        {
            OwnerId     = User.GetUserIdOrThrow(),
            Name        = req.Name.Trim(), Description = req.Description ?? string.Empty,
            VenueType   = req.VenueType,   Address     = req.Address,
            City        = req.City,        Phone       = req.Phone,
            CapacityMin = req.CapacityMin, CapacityMax = req.CapacityMax,
            PricePerHead = req.PricePerHead, PricePerHour = req.PricePerHour,
            ImagesUrl   = req.ImagesUrl ?? [],
            OpeningHours = req.OpeningHours ?? "{}",
            IsApproved  = User.IsSuperadmin()
        };
        _db.Venues.Add(venue);
        _audit.Log("venue", venue.Id, null, "create", $"Zal qo'shildi: {venue.Name}");
        await _db.SaveChangesAsync(ct);
        return Ok(MapVenue(venue));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVenueRequest req, CancellationToken ct)
    {
        var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (venue is null) return NotFound();
        if (venue.OwnerId != User.GetUserId() && !User.IsSuperadmin()) return Forbid();

        var name = req.Name ?? venue.Name;
        var capMin = req.CapacityMin ?? venue.CapacityMin;
        var capMax = req.CapacityMax ?? venue.CapacityMax;
        var perHead = req.PricePerHead ?? venue.PricePerHead;
        var perHour = req.PricePerHour ?? venue.PricePerHour;
        var error = ValidateVenue(name, capMin, capMax, perHead, perHour);
        if (error is not null) return BadRequest(new { message = error });

        venue.Name = name.Trim();
        venue.Description = req.Description ?? venue.Description;
        venue.VenueType = req.VenueType ?? venue.VenueType;
        venue.Address = req.Address ?? venue.Address;
        venue.City = req.City ?? venue.City;
        venue.Phone = req.Phone ?? venue.Phone;
        venue.CapacityMin = capMin;
        venue.CapacityMax = capMax;
        venue.PricePerHead = perHead;
        venue.PricePerHour = perHour;
        if (req.ImagesUrl is not null) venue.ImagesUrl = req.ImagesUrl;
        venue.OpeningHours = req.OpeningHours ?? venue.OpeningHours;
        var ve = _db.Entry(venue);
        var vDiff = new AuditDiff()
            .Add("Nomi", ve.Property(x => x.Name).OriginalValue, venue.Name)
            .Add("Manzil", ve.Property(x => x.Address).OriginalValue, venue.Address)
            .Add("Min sig'im", ve.Property(x => x.CapacityMin).OriginalValue, venue.CapacityMin)
            .Add("Max sig'im", ve.Property(x => x.CapacityMax).OriginalValue, venue.CapacityMax)
            .Add("Narx (1 mehmon)", ve.Property(x => x.PricePerHead).OriginalValue, venue.PricePerHead)
            .Add("Narx (1 soat)", ve.Property(x => x.PricePerHour).OriginalValue, venue.PricePerHour);
        if (vDiff.Any)
            _audit.Log("venue", venue.Id, null, "update", $"Zal o'zgartirildi: {venue.Name} ({vDiff.Fields})", vDiff.Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(MapVenue(venue));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        // Entity to'g'ridan-to'g'ri qaytarilmaydi: Reservations → Venue aylanma bog'lanishi
        // JSON serializatsiyani yiqitardi va boshqa mijozlarning bronlari ochilib qolardi.
        var v = await _db.Venues.FirstOrDefaultAsync(x => x.Id == id && x.IsApproved && !x.IsSuspended, ct);
        if (v is null) return NotFound();
        return Ok(MapVenue(v));
    }

    [HttpGet("reservations/mine")]
    [Authorize]
    public async Task<IActionResult> MyReservations(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var reservations = await _db.VenueReservations
            .Include(r => r.Venue)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.EventDate)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return Ok(reservations.Select(MapReservation));
    }

    /// <summary>Zal egasi yoki superadmin: tadbir bronini tasdiqlash / bekor qilish / yakunlash.</summary>
    [HttpPut("reservations/{reservationId:guid}/status")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> SetReservationStatus(Guid reservationId, [FromBody] VenueReservationStatusRequest req, CancellationToken ct)
    {
        var allowed = new[] { "confirmed", "cancelled", "completed" };
        if (!allowed.Contains(req.Status)) return BadRequest(new { message = "Noto'g'ri status." });

        var res = await _db.VenueReservations.Include(r => r.Venue).FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (res is null) return NotFound();
        if (res.Venue?.OwnerId != User.GetUserId() && !User.IsSuperadmin()) return Forbid();
        if (res.Status is "cancelled" or "completed")
            return BadRequest(new { message = "Bu bron allaqachon yopilgan." });

        res.Status = req.Status;
        var title = req.Status switch
        {
            "confirmed" => "Tadbir broningiz tasdiqlandi",
            "cancelled" => "Tadbir broningiz bekor qilindi",
            _ => "Tadbir yakunlandi"
        };
        _db.Notifications.Add(new Notification
        {
            UserId = res.UserId, Type = "venue_booking", Title = title,
            Body = $"{res.Venue?.Name} — {res.EventDate:dd.MM.yyyy}, {res.GuestsCount} kishi" +
                   (string.IsNullOrWhiteSpace(req.Note) ? "" : $". {req.Note.Trim()}"),
            Link = "/profile"
        });
        _audit.Log("venue_reservation", res.Id, null, "status",
            $"Zal broni: {req.Status} — {res.Venue?.Name}, {res.EventDate:dd.MM.yyyy}");
        await _db.SaveChangesAsync(ct);
        return Ok(MapReservation(res));
    }

    /// <summary>Zal egasining zallari (tasdiqlanmaganlari ham).</summary>
    [HttpGet("mine")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var venues = await _db.Venues.Where(v => v.OwnerId == userId).OrderBy(v => v.CreatedAt).ToListAsync(ct);
        return Ok(venues.Select(MapVenue));
    }

    /// <summary>Zal egasi: o'z zallariga kelgan tadbir bronlari.</summary>
    [HttpGet("owner-reservations")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> OwnerReservations([FromQuery] string? status, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var q = _db.VenueReservations.Where(r => r.Venue!.OwnerId == userId);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        var list = await q.OrderBy(r => r.EventDate).Take(500)
            .Select(r => new
            {
                id = r.Id, venueId = r.VenueId, venueName = r.Venue!.Name, userFullName = r.User!.Profile!.FullName,
                userEmail = r.User.Email, userPhone = r.User.Profile.Phone, eventDate = r.EventDate, eventType = r.EventType,
                guestsCount = r.GuestsCount, totalPrice = r.TotalPrice, status = r.Status,
                specialRequests = r.SpecialRequests ?? r.Notes, createdAt = r.CreatedAt
            }).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}/booked-dates")]
    public async Task<IActionResult> BookedDates(Guid id, CancellationToken ct)
    {
        var today = BusinessTime.Today();
        var dates = await _db.VenueReservations
            .Where(r => r.VenueId == id
                        && r.EventDate >= today
                        && (r.Status == "pending" || r.Status == "confirmed"))
            .Select(r => r.EventDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        return Ok(dates.Select(d => d.ToString("yyyy-MM-dd")));
    }

    [HttpPost("{id:guid}/reservations")]
    [Authorize]
    public async Task<IActionResult> CreateReservation(Guid id, [FromBody] CreateVenueReservationRequest req, CancellationToken ct)
    {
        var res = await BookVenueAsync(id, req.EventDate, req.GuestsCount, (venue, r) =>
        {
            r.EventType = string.IsNullOrWhiteSpace(req.EventType) ? "Boshqa" : req.EventType.Trim();
            r.SpecialRequests = req.SpecialRequests;
            r.Notes = req.SpecialRequests;
            r.TotalPrice = venue.PricePerHead * req.GuestsCount;
        }, ct);
        return Ok(MapReservation(res));
    }

    [HttpPost("{id:guid}/reserve")]
    [Authorize]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveVenueRequest req, CancellationToken ct)
    {
        if (req.Hours < 1 || req.Hours > 24)
            return BadRequest(new { message = "Soat 1 dan 24 gacha bo'lishi kerak." });

        var res = await BookVenueAsync(id, req.Date, req.GuestsCount, (venue, r) =>
        {
            r.Notes = req.Notes;
            r.TotalPrice = venue.PricePerHour * req.Hours;
        }, ct);
        return Ok(new { id = res.Id, status = res.Status, totalPrice = res.TotalPrice });
    }

    /// <summary>
    /// Zal bir kunda faqat bitta tadbirga bron qilinadi. Tekshiruv va yozish Serializable
    /// tranzaksiyada — ikki kishi bir vaqtda bir sanani ololmaydi.
    /// </summary>
    private async Task<VenueReservation> BookVenueAsync(
        Guid venueId, DateOnly eventDate, int guestsCount,
        Action<Venue, VenueReservation> fill, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (eventDate < BusinessTime.Today())
            throw new BusinessRuleException("O'tgan sanaga bron qilib bo'lmaydi.");
        if (guestsCount < 1)
            throw new BusinessRuleException("Mehmonlar soni kamida 1 bo'lishi kerak.");

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var venue = await _db.Venues.FirstOrDefaultAsync(v => v.Id == venueId && v.IsApproved && !v.IsSuspended, ct)
                ?? throw new NotFoundException("Zal topilmadi.");
            if (guestsCount < venue.CapacityMin || guestsCount > venue.CapacityMax)
                throw new BusinessRuleException($"Mehmonlar soni {venue.CapacityMin}–{venue.CapacityMax} oralig'ida bo'lishi kerak.");

            var taken = await _db.VenueReservations.AnyAsync(r =>
                r.VenueId == venueId && r.EventDate == eventDate &&
                (r.Status == "pending" || r.Status == "confirmed"), ct);
            if (taken)
                throw new ConflictException("Bu sana band. Boshqa kunni tanlang.");

            var res = new VenueReservation
            {
                VenueId = venueId,
                UserId = userId,
                EventDate = eventDate,
                Date = eventDate,
                GuestsCount = guestsCount,
                Status = "pending",
                Venue = venue
            };
            fill(venue, res);
            _db.VenueReservations.Add(res);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return res;
        });
    }

    private static string? ValidateVenue(string? name, int capMin, int capMax, int perHead, int perHour)
    {
        if (string.IsNullOrWhiteSpace(name)) return "Zal nomi kiritilishi shart.";
        if (capMin < 1) return "Minimal sig'im kamida 1 bo'lishi kerak.";
        if (capMax < capMin) return "Maksimal sig'im minimaldan kichik bo'lmasligi kerak.";
        if (perHead < 0 || perHour < 0) return "Narx manfiy bo'lishi mumkin emas.";
        return null;
    }

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

    private static object MapReservation(VenueReservation r) => new
    {
        id = r.Id,
        userId = r.UserId,
        venueId = r.VenueId,
        venueName = r.Venue?.Name,
        eventDate = r.EventDate,
        eventType = r.EventType,
        guestsCount = r.GuestsCount,
        specialRequests = r.SpecialRequests,
        totalPrice = r.TotalPrice,
        status = r.Status,
        yumsUsed = r.YumsUsed,
        createdAt = r.CreatedAt,
        updatedAt = r.UpdatedAt
    };
}
public record VenueReservationStatusRequest(string Status, string? Note = null);
public record ReserveVenueRequest(DateOnly Date, int GuestsCount, int Hours = 3, string? Notes = null);
public record CreateVenueReservationRequest(DateOnly EventDate, string EventType, int GuestsCount, string? SpecialRequests = null);
public record CreateVenueRequest(
    string Name, string? Description, string VenueType,
    string Address, string City, string? Phone,
    int CapacityMin, int CapacityMax,
    int PricePerHead, int PricePerHour,
    List<string>? ImagesUrl, string? OpeningHours);
public record UpdateVenueRequest(
    string? Name = null, string? Description = null, string? VenueType = null,
    string? Address = null, string? City = null, string? Phone = null,
    int? CapacityMin = null, int? CapacityMax = null,
    int? PricePerHead = null, int? PricePerHour = null,
    List<string>? ImagesUrl = null, string? OpeningHours = null);
