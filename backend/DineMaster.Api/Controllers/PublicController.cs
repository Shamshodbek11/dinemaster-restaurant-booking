using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace DineMaster.Api.Controllers;

/// <summary>Bosh sahifa uchun haqiqiy ko'rsatkichlar (5 daqiqa keshlanadi).</summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    public PublicController(AppDbContext db, IMemoryCache cache) { _db = db; _cache = cache; }

    [HttpGet("stats")]
    public async Task<IActionResult> Stats(CancellationToken ct)
    {
        var stats = await _cache.GetOrCreateAsync("public-stats", async e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            var visible = _db.Restaurants.Where(r => r.IsApproved && !r.IsSuspended);
            var restaurants = await visible.CountAsync(ct);
            var cities = await visible.Select(r => r.City).Distinct().CountAsync(ct);
            var reservations = await _db.Reservations.CountAsync(r => r.Status == "completed" || r.Status == "confirmed" || r.Status == "pending", ct);
            var guests = await _db.Reservations.Where(r => r.Status == "completed").SumAsync(r => (int?)r.GuestsCount, ct) ?? 0;
            var reviews = await _db.Reviews.CountAsync(ct);
            var avgRating = reviews == 0 ? 0 : Math.Round(await _db.Reviews.AverageAsync(r => (double)r.Rating, ct), 1);
            var users = await _db.Users.CountAsync(ct);
            return new { restaurants, cities, reservations, guests, reviews, avgRating, users };
        });
        return Ok(stats);
    }
}
