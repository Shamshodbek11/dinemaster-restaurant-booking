using DineMaster.Api.Extensions;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public NotificationsController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int size = 30, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var userId = User.GetUserIdOrThrow();
        var q = _db.Notifications.Where(n => n.UserId == userId).OrderByDescending(n => n.CreatedAt);
        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        var unreadCount = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return Ok(new { total, page, size, unreadCount, items = items.Select(n => new {
            n.Id, n.Type, n.Title, n.Body, n.Link, n.IsRead, n.ReservationId, n.CreatedAt
        })});
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var count = await _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, ct);
        return Ok(new { count });
    }

    [HttpPost("{id:guid}/read")]
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var n = await _db.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (n is null) return NotFound();
        n.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return Ok();
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var unread = await _db.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync(ct);
        foreach (var n in unread) n.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return Ok(new { marked = unread.Count });
    }
}
