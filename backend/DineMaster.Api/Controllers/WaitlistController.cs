using DineMaster.Api.Extensions;
using DineMaster.Api.Services;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/waitlist")]
[Authorize]
public class WaitlistController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RestaurantAccess _access;
    public WaitlistController(AppDbContext db, RestaurantAccess access) { _db = db; _access = access; }

    [HttpPost]
    public async Task<IActionResult> Join([FromBody] JoinWaitlistRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (req.Date < BusinessTime.Today())
            return BadRequest(new { message = "O'tgan sana uchun kutish ro'yxatiga yozilib bo'lmaydi." });
        if (req.GuestsCount is < 1 or > 50)
            return BadRequest(new { message = "Mehmonlar soni 1 dan 50 gacha bo'lishi kerak." });
        var restaurantActive = await _db.Restaurants.AnyAsync(r => r.Id == req.RestaurantId && r.IsApproved && !r.IsSuspended, ct);
        if (!restaurantActive) return NotFound(new { message = "Restoran topilmadi yoki faol emas." });

        var already = await _db.Waitlist.AnyAsync(w =>
            w.UserId == userId && w.RestaurantId == req.RestaurantId &&
            w.Date == req.Date && w.Status == "waiting", ct);
        if (already) return Conflict(new { message = "Allaqachon kutish ro'yxatidasiz." });

        var position = await _db.Waitlist.CountAsync(w =>
            w.RestaurantId == req.RestaurantId && w.Date == req.Date && w.Status == "waiting", ct) + 1;

        var entry = new Waitlist {
            UserId = userId, RestaurantId = req.RestaurantId,
            Date = req.Date, GuestsCount = req.GuestsCount,
            Time = req.Time, Note = string.IsNullOrWhiteSpace(req.Note) ? null : req.Note.Trim(),
            Position = position, Status = "waiting"
        };
        _db.Waitlist.Add(entry);
        var owner = await _db.Restaurants.Where(r => r.Id == req.RestaurantId).Select(r => new { r.OwnerId, r.Name }).FirstAsync(ct);
        if (owner.OwnerId is Guid ownerId)
            _db.Notifications.Add(new Notification
            {
                UserId = ownerId, Type = "waitlist_join", Title = "Kutish ro'yxatiga yangi mijoz",
                Body = $"{owner.Name} — {req.Date:dd.MM.yyyy}, {req.GuestsCount} kishi (navbat: {position})", Link = "/manager"
            });
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = entry.Id, position = entry.Position });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Leave(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var entry = await _db.Waitlist.FirstOrDefaultAsync(w => w.Id == id && w.UserId == userId, ct);
        if (entry is null) return NotFound();
        if (entry.Status is not ("waiting" or "notified"))
            return BadRequest(new { message = "Bu yozuv allaqachon yopilgan." });
        entry.Status = "cancelled";
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Kutish ro'yxatidan chiqildi." });
    }

    [HttpGet("restaurant/{restaurantId:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> ForRestaurant(Guid restaurantId, CancellationToken ct)
    {
        await _access.RequireAsync(restaurantId, AccessLevel.Host, ct);
        var list = await _db.Waitlist
            .Where(w => w.RestaurantId == restaurantId)
            .Include(w => w.User).ThenInclude(u => u!.Profile)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync(ct);
        return Ok(list.Select(w => new
        {
            id = w.Id,
            userId = w.UserId,
            userFullName = w.User?.Profile?.FullName,
            userName = w.User?.Profile?.FullName,
            restaurantId = w.RestaurantId,
            date = w.Date,
            time = w.Time,
            guestsCount = w.GuestsCount,
            position = w.Position,
            status = w.Status,
            note = w.Note,
            createdAt = w.CreatedAt
        }));
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateWaitlistStatusRequest req, CancellationToken ct)
    {
        var entry = await _db.Waitlist.FirstOrDefaultAsync(w => w.Id == id, ct);
        if (entry is null) return NotFound();
        await _access.RequireAsync(entry.RestaurantId, AccessLevel.Host, ct);

        var allowed = new[] { "waiting", "notified", "seated", "cancelled" };
        if (!allowed.Contains(req.Status)) return BadRequest(new { message = "Noto'g'ri status." });

        entry.Status = req.Status;
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = entry.Id, status = entry.Status, position = entry.Position });
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var list = await _db.Waitlist
            .Where(w => w.UserId == userId)
            .Include(w => w.Restaurant)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);
        return Ok(list.Select(w => new {
            w.Id, restaurantId = w.RestaurantId, restaurantName = w.Restaurant?.Name,
            w.Date, w.GuestsCount, w.Position, w.Status, w.CreatedAt
        }));
    }
}
public record JoinWaitlistRequest(Guid RestaurantId, DateOnly Date, int GuestsCount, TimeOnly? Time = null, string? Note = null);

public record UpdateWaitlistStatusRequest(string Status);
