using DineMaster.Api.Services;
using DineMaster.Api.Extensions;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/reviews")]
public class ReviewsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;
    private readonly RestaurantAccess _access;
    public ReviewsController(AppDbContext db, AuditLogger audit, RestaurantAccess access) { _db = db; _audit = audit; _access = access; }

    [HttpGet("restaurant/{restaurantId:guid}")]
    public async Task<IActionResult> ByRestaurant(
        Guid restaurantId, [FromQuery] int page = 1, [FromQuery] int size = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var q = _db.Reviews
            .Where(r => r.RestaurantId == restaurantId)
            .Include(r => r.User).ThenInclude(u => u!.Profile)
            .OrderByDescending(r => r.CreatedAt);

        var total = await q.CountAsync(ct);
        var items = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new { total, page, size, items = items.Select(MapReview) });
    }

    /// <summary>Foydalanuvchining o'z sharhlari (restoran nomi bilan).</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var items = await _db.Reviews.Where(r => r.UserId == userId)
            .Include(r => r.Restaurant)
            .OrderByDescending(r => r.CreatedAt).Take(200)
            .ToListAsync(ct);
        return Ok(items.Select(r => new
        {
            id = r.Id, restaurantId = r.RestaurantId, restaurantName = r.Restaurant?.Name,
            rating = Math.Round((r.FoodRating + r.ServiceRating + r.InteriorRating) / 3m, 1),
            foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
            comment = r.Comment, ownerResponse = r.OwnerResponse, ownerRespondedAt = r.OwnerRespondedAt,
            createdAt = r.CreatedAt
        }));
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();

        if (!AreRatingsValid(req.FoodRating, req.ServiceRating, req.InteriorRating))
            return BadRequest(new { message = "Reytinglar 1 dan 10 gacha bo'lishi kerak." });

        // Sharh faqat shu restoranga haqiqatda tashrif buyurgan (bron "completed") foydalanuvchidan
        var visits = _db.Reservations.Where(r =>
            r.UserId == userId && r.RestaurantId == req.RestaurantId && r.Status == BookingRules.Completed);
        if (req.ReservationId is Guid reservationId)
            visits = visits.Where(r => r.Id == reservationId);
        if (!await visits.AnyAsync(ct))
            return BadRequest(new { message = req.ReservationId is null
                ? "Sharhni restoranga tashrif buyurganingizdan keyin qoldirishingiz mumkin."
                : "Ko'rsatilgan bron sizga tegishli emas yoki hali yakunlanmagan." });

        // BUG FIX: duplicate review oldini olish (DB unique index + application check)
        var existing = await _db.Reviews
            .AnyAsync(r => r.UserId == userId && r.RestaurantId == req.RestaurantId, ct);
        if (existing)
            return Conflict(new { message = "Siz bu restoranga allaqachon sharh qoldirgansiz." });

        var review = new Review
        {
            UserId = userId,
            RestaurantId = req.RestaurantId,
            FoodRating = req.FoodRating,
            ServiceRating = req.ServiceRating,
            InteriorRating = req.InteriorRating,
            Comment = req.Comment?.Trim(),
            ReservationId = req.ReservationId
        };
        _db.Reviews.Add(review);
        // Restoran egasiga: yangi sharh
        var restaurantInfo = await _db.Restaurants.Where(r => r.Id == req.RestaurantId)
            .Select(r => new { r.OwnerId, r.Name }).FirstOrDefaultAsync(ct);
        if (restaurantInfo?.OwnerId is Guid ownerId)
            _db.Notifications.Add(new Notification
            {
                UserId = ownerId, Type = "new_review", Title = "Yangi sharh",
                Body = $"{restaurantInfo.Name}: {Math.Round((req.FoodRating + req.ServiceRating + req.InteriorRating) / 3.0, 1)}/10" +
                       (string.IsNullOrWhiteSpace(req.Comment) ? "" : $" — \"{Truncate(req.Comment!, 80)}\""),
                Link = "/manager"
            });
        await _db.SaveChangesAsync(ct);

        await UpdateRestaurantRatingAsync(req.RestaurantId, ct);
        return Ok(MapReview(review));
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateReviewRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (!AreRatingsValid(req.FoodRating, req.ServiceRating, req.InteriorRating))
            return BadRequest(new { message = "Reytinglar 1 dan 10 gacha bo'lishi kerak." });
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId, ct);
        if (review is null) return NotFound();

        review.FoodRating = req.FoodRating;
        review.ServiceRating = req.ServiceRating;
        review.InteriorRating = req.InteriorRating;
        review.Comment = req.Comment;
        await _db.SaveChangesAsync(ct);

        await UpdateRestaurantRatingAsync(review.RestaurantId, ct);
        return Ok(MapReview(review));
    }

    [HttpPost("{id:guid}/respond")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Respond(Guid id, [FromBody] RespondRequest req, CancellationToken ct)
    {
        var review = await _db.Reviews.Include(r => r.Restaurant).FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review is null) return NotFound();
        // Faqat o'z restoran sharhiga javob yoziladi (superadmin bundan mustasno)
        await _access.RequireAsync(review.RestaurantId, AccessLevel.Manager, ct);
        var reply = (req.Reply ?? req.OwnerResponse)?.Trim();
        if (string.IsNullOrEmpty(reply)) return BadRequest(new { message = "Javob matni bo'sh." });
        review.OwnerResponse = reply;
        _audit.Log("review", review.Id, review.RestaurantId, "respond", $"Sharhga javob yozildi: \"{Truncate(reply, 80)}\"");
        _db.Notifications.Add(new Notification
        {
            UserId = review.UserId, Type = "review_reply", Title = "Sharhingizga javob berildi",
            Body = $"{review.Restaurant?.Name}: \"{Truncate(reply, 100)}\"", Link = $"/restaurant/{review.RestaurantId}"
        });
        review.OwnerRespondedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Ok(MapReview(review));
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var review = await _db.Reviews.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (review is null) return NotFound();
        if (review.UserId != userId && !User.IsSuperadmin()) return Forbid();

        var restaurantId = review.RestaurantId;
        if (review.UserId != userId)
            _audit.Log("review", review.Id, review.RestaurantId, "delete", $"Sharh o'chirildi (moderatsiya): \"{Truncate(review.Comment ?? "", 80)}\"");
        _db.Reviews.Remove(review);
        await _db.SaveChangesAsync(ct);
        await UpdateRestaurantRatingAsync(restaurantId, ct);
        return Ok(new { message = "Sharh o'chirildi." });
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    private static bool AreRatingsValid(params int[] ratings) => ratings.All(r => r >= 1 && r <= 10);

    private async Task UpdateRestaurantRatingAsync(Guid restaurantId, CancellationToken ct)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct);
        if (restaurant is null) return;

        var reviews = await _db.Reviews
            .Where(r => r.RestaurantId == restaurantId)
            .Select(r => new { r.FoodRating, r.ServiceRating, r.InteriorRating })
            .ToListAsync(ct);

        if (reviews.Count == 0)
        {
            restaurant.Rating = 0; restaurant.FoodRating = 0;
            restaurant.ServiceRating = 0; restaurant.InteriorRating = 0;
            restaurant.ReviewCount = 0;
        }
        else
        {
            restaurant.FoodRating = (decimal)reviews.Average(r => r.FoodRating);
            restaurant.ServiceRating = (decimal)reviews.Average(r => r.ServiceRating);
            restaurant.InteriorRating = (decimal)reviews.Average(r => r.InteriorRating);
            restaurant.Rating = (restaurant.FoodRating + restaurant.ServiceRating + restaurant.InteriorRating) / 3;
            restaurant.ReviewCount = reviews.Count;
        }
        await _db.SaveChangesAsync(ct);
    }

    private static object MapReview(Review r) => new
    {
        id = r.Id, restaurantId = r.RestaurantId,
        userId = r.UserId,
        userFullName = r.User?.Profile?.FullName,
        userName = r.User?.Profile?.FullName,
        rating = Math.Round((r.FoodRating + r.ServiceRating + r.InteriorRating) / 3m, 1),
        foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
        comment = r.Comment,
        ownerResponse = r.OwnerResponse,
        ownerReply = r.OwnerResponse,
        ownerRespondedAt = r.OwnerRespondedAt,
        reservationId = r.ReservationId,
        createdAt = r.CreatedAt
    };
}

public record CreateReviewRequest(
    Guid RestaurantId, int FoodRating, int ServiceRating, int InteriorRating,
    string? Comment, Guid? ReservationId = null);
// Frontend "ownerResponse" yuboradi; eski klientlar uchun "reply" ham qabul qilinadi
public record RespondRequest(string? Reply = null, string? OwnerResponse = null);
