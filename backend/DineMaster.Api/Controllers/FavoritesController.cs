using DineMaster.Api.Extensions;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/favorites")]
[Authorize]
public class FavoritesController : ControllerBase
{
    private readonly AppDbContext _db;
    public FavoritesController(AppDbContext db) { _db = db; }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var favs = await _db.Favorites
            .Where(f => f.UserId == userId)
            .Include(f => f.Restaurant)
            .OrderByDescending(f => f.CreatedAt)
            .ToListAsync(ct);
        return Ok(favs.Select(f => new {
            f.Id, restaurantId = f.RestaurantId,
            restaurantName = f.Restaurant?.Name, f.CreatedAt
        }));
    }

    [HttpPost("{restaurantId:guid}")]
    public async Task<IActionResult> Add(Guid restaurantId, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (await _db.Favorites.AnyAsync(f => f.UserId == userId && f.RestaurantId == restaurantId, ct))
            return Conflict(new { message = "Allaqachon sevimlilar ro'yxatida." });
        _db.Favorites.Add(new Favorite { UserId = userId, RestaurantId = restaurantId });
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Sevimlilar ro'yxatiga qo'shildi." });
    }

    [HttpDelete("{restaurantId:guid}")]
    public async Task<IActionResult> Remove(Guid restaurantId, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var fav = await _db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.RestaurantId == restaurantId, ct);
        if (fav is null) return NotFound();
        _db.Favorites.Remove(fav);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Sevimlilar ro'yxatidan o'chirildi." });
    }
}
