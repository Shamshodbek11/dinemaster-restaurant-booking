using System.Text.Json;
using DineMaster.Api.Services;
using DineMaster.Api.Extensions;
using DineMaster.Application.Services;
using DineMaster.Domain.Enums;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/restaurants")]
public class RestaurantsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;
    private readonly RestaurantAccess _access;
    private readonly DineMaster.Application.Abstractions.IBookingService _booking;
    public RestaurantsController(AppDbContext db, AuditLogger audit, RestaurantAccess access,
        DineMaster.Application.Abstractions.IBookingService booking)
    {
        _db = db; _audit = audit; _access = access; _booking = booking;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? city,
        [FromQuery] string? cuisine,
        [FromQuery] string? search,
        [FromQuery] string? priceRange,
        [FromQuery] decimal? minRating,
        [FromQuery] string? sort,
        [FromQuery] DateOnly? date,
        [FromQuery] TimeOnly? time,
        [FromQuery] int? guests,
        [FromQuery] int page = 1,
        [FromQuery] int size = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        size = Math.Clamp(size, 1, 100);
        var q = _db.Restaurants
            .Where(r => r.IsApproved && !r.IsSuspended)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(city))
            q = q.Where(r => r.City.ToLower().Contains(city.ToLower()));
        if (!string.IsNullOrWhiteSpace(cuisine))
            q = q.Where(r => r.CuisineType.ToLower().Contains(cuisine.ToLower()));
        if (!string.IsNullOrWhiteSpace(priceRange))
            q = q.Where(r => r.PriceRange == priceRange);
        if (minRating.HasValue)
            q = q.Where(r => r.Rating >= minRating.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.ToLower();
            q = q.Where(r => r.Name.ToLower().Contains(s)
                           || r.Description.ToLower().Contains(s)
                           || r.CuisineType.ToLower().Contains(s)
                           || r.Address.ToLower().Contains(s));
        }

        q = sort switch
        {
            "rating" => q.OrderByDescending(r => r.Rating),
            "name" => q.OrderBy(r => r.Name),
            "newest" => q.OrderByDescending(r => r.CreatedAt),
            _ => q.OrderByDescending(r => r.Rating)
        };

        // "Shu sana va vaqtda N kishiga joy bor" — ish vaqti, sig'im va stollar bo'yicha
        if (date is DateOnly d && time is TimeOnly tm)
        {
            var all = await q.ToListAsync(ct);
            var available = await _booking.FindAvailableAsync(all.Select(r => r.Id).ToList(), d, tm, Math.Clamp(guests ?? 2, 1, 50), ct);
            var filtered = all.Where(r => available.Contains(r.Id)).ToList();
            return Ok(new { total = filtered.Count, page, size, items = filtered.Skip((page - 1) * size).Take(size).Select(MapRestaurant) });
        }

        var total = await q.CountAsync(ct);
        var restaurants = await q.Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return Ok(new { total, page, size, items = restaurants.Select(MapRestaurant) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var r = await _db.Restaurants
            .Include(x => x.MenuCategories).ThenInclude(c => c.Items)
            .Include(x => x.Tables.Where(t => t.IsActive))
            .FirstOrDefaultAsync(x => x.Id == id && x.IsApproved && !x.IsSuspended, ct);
        if (r is null) return NotFound();
        return Ok(MapRestaurantDetail(r));
    }

    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var list = await _db.Restaurants
            .Where(r => r.OwnerId == userId)
            .ToListAsync(ct);
        return Ok(list.Select(MapRestaurantForOwner));
    }

    [HttpPost]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Create([FromBody] CreateRestaurantRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Restoran nomi kiritilishi shart." });
        var error = ValidateNumbers(req.TotalSlots, req.DepositPerGuest, null, req.Latitude, req.Longitude)
                    ?? ValidateHourlySlots(req.HourlySlots) ?? ValidateOpeningHours(req.OpeningHours);
        if (error is not null) return BadRequest(new { message = error });
        var restaurant = new Restaurant
        {
            OwnerId = userId,
            Name = req.Name, Description = req.Description,
            CuisineType = req.CuisineType, PriceRange = req.PriceRange,
            Address = req.Address, City = req.City,
            Phone = req.Phone, Website = req.Website,
            Latitude = req.Latitude, Longitude = req.Longitude,
            OpeningHours = req.OpeningHours ?? "{}",
            Amenities = req.Amenities ?? [],
            Policies = req.Policies ?? [],
            ImagesUrl = req.ImagesUrl ?? [],
            TotalSlots = req.TotalSlots,
            HourlySlots = req.HourlySlots is > 0 ? req.HourlySlots : null,
            DepositRequired = req.DepositRequired,
            DepositPerGuest = req.DepositPerGuest,
            IsApproved = User.IsSuperadmin() // superadmin o'zi yaratsa — auto-approve
        };
        _db.Restaurants.Add(restaurant);
        _audit.Log("restaurant", restaurant.Id, restaurant.Id, "create", $"Restoran qo'shildi: {restaurant.Name}");
        if (!restaurant.IsApproved)
            await NotifySuperadminsAsync("Yangi restoran tasdiq kutmoqda",
                $"\"{restaurant.Name}\" ({restaurant.City}) qo'shildi — tekshirib tasdiqlang.", ct);
        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurantForOwner(restaurant));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateRestaurantRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        await _access.RequireAsync(r.Id, AccessLevel.Manager, ct);

        if (req.Name is not null && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Restoran nomi bo'sh bo'lmasligi kerak." });
        var error = ValidateNumbers(req.TotalSlots, req.DepositPerGuest, req.Discount, req.Latitude, req.Longitude)
                    ?? ValidateHourlySlots(req.HourlySlots) ?? ValidateOpeningHours(req.OpeningHours)
                    ?? OpeningHours.ValidateSpecial(req.SpecialHours);
        if (error is not null) return BadRequest(new { message = error });

        // Nomi, tavsif, manzil, rasmlar: tasdiqlangan restoranda egasi o'zgartirsa — superadmin
        // tasdiqlamaguncha eski qiymatlar ko'rinadi (moderatsiya). Superadmin yoki hali tasdiqlanmagan
        // restoran uchun darhol qo'llanadi.
        var changes = new RestaurantChangeSet
        {
            Name = req.Name, Description = req.Description, CuisineType = req.CuisineType,
            PriceRange = req.PriceRange, Address = req.Address, City = req.City,
            Latitude = req.Latitude, Longitude = req.Longitude, ImagesUrl = req.ImagesUrl
        }.OnlyDifferentFrom(r);

        var needsModeration = r.IsApproved && !User.IsSuperadmin() && !changes.IsEmpty;
        if (needsModeration)
        {
            r.PendingChanges = changes.MergeOnto(RestaurantChangeSet.FromJson(r.PendingChanges)).ToJson();
            r.PendingChangesSubmittedAt = DateTime.UtcNow;
            await NotifySuperadminsAsync("Restoran o'zgarishi tekshiruvda",
                $"\"{r.Name}\" egasi nomi/manzili/tavsifini o'zgartirdi — tasdiqlash kerak.", ct);
        }
        else
        {
            changes.ApplyTo(r);
        }

        r.Phone = req.Phone ?? r.Phone;
        r.Website = req.Website ?? r.Website;
        r.OpeningHours = req.OpeningHours ?? r.OpeningHours;
        if (req.SpecialHours is not null) r.SpecialHours = string.IsNullOrWhiteSpace(req.SpecialHours) || req.SpecialHours.Trim() == "{}" ? null : req.SpecialHours;
        if (req.Amenities is not null) r.Amenities = req.Amenities;
        if (req.Policies is not null) r.Policies = req.Policies;
        if (req.HourlySlots is not null) r.HourlySlots = req.HourlySlots > 0 ? req.HourlySlots : null;
        r.TotalSlots = req.TotalSlots ?? r.TotalSlots;
        r.DepositRequired = req.DepositRequired ?? r.DepositRequired;
        r.DepositPerGuest = req.DepositPerGuest ?? r.DepositPerGuest;
        if (req.Discount.HasValue)
        {
            // Chegirma formasi foiz va vaqtni birga yuboradi: vaqt bo'sh — kun bo'yi amal qiladi
            r.Discount = req.Discount > 0 ? req.Discount : null;
            r.DiscountTimeStart = req.DiscountTimeStart;
            r.DiscountTimeEnd = req.DiscountTimeEnd;
        }
        else
        {
            if (req.DiscountTimeStart.HasValue) r.DiscountTimeStart = req.DiscountTimeStart;
            if (req.DiscountTimeEnd.HasValue) r.DiscountTimeEnd = req.DiscountTimeEnd;
        }

        // Faoliyat jurnali: barcha o'zgargan maydonlar (eski → yangi)
        var old = (Restaurant)_db.Entry(r).OriginalValues.ToObject();
        var diff = new AuditDiff()
            .Add("Nomi", old.Name, r.Name).Add("Tavsif", old.Description, r.Description)
            .Add("Oshxona", old.CuisineType, r.CuisineType).Add("Narx darajasi", old.PriceRange, r.PriceRange)
            .Add("Manzil", old.Address, r.Address).Add("Shahar", old.City, r.City)
            .Add("Kenglik", old.Latitude, r.Latitude).Add("Uzunlik", old.Longitude, r.Longitude)
            .Add("Rasmlar", old.ImagesUrl, r.ImagesUrl)
            .Add("Telefon", old.Phone, r.Phone).Add("Veb-sayt", old.Website, r.Website)
            .Add("Ish vaqti", old.OpeningHours, r.OpeningHours)
            .Add("Maxsus kunlar", old.SpecialHours, r.SpecialHours)
            .Add("Qulayliklar", old.Amenities, r.Amenities).Add("Qoidalar", old.Policies, r.Policies)
            .Add("Kunlik slotlar", old.TotalSlots, r.TotalSlots).Add("Soatlik slotlar", old.HourlySlots, r.HourlySlots)
            .Add("Depozit talab", old.DepositRequired, r.DepositRequired)
            .Add("Depozit (1 mehmon)", old.DepositPerGuest, r.DepositPerGuest)
            .Add("Chegirma %", old.Discount, r.Discount)
            .Add("Chegirma boshlanishi", old.DiscountTimeStart, r.DiscountTimeStart)
            .Add("Chegirma tugashi", old.DiscountTimeEnd, r.DiscountTimeEnd);
        if (needsModeration)
            diff.Add("Moderatsiyaga yuborildi", null, string.Join(", ", ModerationFieldLabels(changes)));
        if (diff.Any)
            _audit.Log("restaurant", r.Id, r.Id, "update", $"Restoran sozlamalari o'zgartirildi: {diff.Fields}", diff.Changes);

        await _db.SaveChangesAsync(ct);
        return Ok(MapRestaurantForOwner(r));
    }

    private static IEnumerable<string> ModerationFieldLabels(RestaurantChangeSet c)
    {
        if (c.Name is not null) yield return "Nomi";
        if (c.Description is not null) yield return "Tavsif";
        if (c.CuisineType is not null) yield return "Oshxona";
        if (c.PriceRange is not null) yield return "Narx darajasi";
        if (c.Address is not null) yield return "Manzil";
        if (c.City is not null) yield return "Shahar";
        if (c.Latitude is not null || c.Longitude is not null) yield return "Koordinata";
        if (c.ImagesUrl is not null) yield return "Rasmlar";
    }

    private async Task NotifySuperadminsAsync(string title, string body, CancellationToken ct)
    {
        var superadmins = await _db.UserRoles.Where(x => x.Role == AppRole.Superadmin).Select(x => x.UserId).ToListAsync(ct);
        foreach (var id in superadmins)
            _db.Notifications.Add(new Notification
            {
                UserId = id, Type = "moderation", Title = title, Body = body, Link = "/superadmin-dashboard"
            });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,superadmin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var r = await _db.Restaurants.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return NotFound();
        await _access.RequireAsync(r.Id, AccessLevel.Owner, ct);

        // Bronlari bor restoran o'chirilmaydi: cascade barcha bronlar, sharhlar va mijozlar
        // tarixini o'chirib yuborardi (yums ham qaytmasdi). Uni to'xtatib qo'yish kerak.
        if (await _db.Reservations.AnyAsync(x => x.RestaurantId == id, ct))
            return Conflict(new { message = "Restoranda bronlar tarixi bor, uni o'chirib bo'lmaydi. Superadmin orqali to'xtatib qo'ying." });

        _audit.Log("restaurant", r.Id, r.Id, "delete", $"Restoran o'chirildi: {r.Name}");
        _db.Restaurants.Remove(r);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Restoran o'chirildi." });
    }

    private static string? ValidateNumbers(int? totalSlots, decimal? depositPerGuest, int? discount, decimal? lat, decimal? lng)
    {
        if (totalSlots is < 1 or > 1000) return "Kunlik slotlar soni 1 dan 1000 gacha bo'lishi kerak.";
        if (depositPerGuest < 0) return "Depozit manfiy bo'lishi mumkin emas.";
        if (discount is < 0 or > 100) return "Chegirma 0–100% oralig'ida bo'lishi kerak.";
        if (lat is < -90 or > 90 || lng is < -180 or > 180) return "Koordinatalar noto'g'ri.";
        return null;
    }

    private static string? ValidateHourlySlots(int? hourly)
        => hourly is < 0 or > 500 ? "Soatlik slotlar 0 (cheklovsiz) dan 500 gacha bo'lishi kerak." : null;

    private static string? ValidateOpeningHours(string? json)
    {
        if (json is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind == JsonValueKind.Object ? null : "Ish vaqti noto'g'ri formatda.";
        }
        catch (JsonException)
        {
            return "Ish vaqti noto'g'ri formatda.";
        }
    }

    /// <summary>Egasi uchun: moderatsiyadagi o'zgarishlar bilan.</summary>
    private static object MapRestaurantForOwner(Restaurant r) => MapRestaurant(r, includeOwnerData: true);

    // includeOwnerData=false bo'lsa pendingChanges null va JSON'ga chiqmaydi (WhenWritingNull)
    private static object MapRestaurant(Restaurant r) => MapRestaurant(r, includeOwnerData: false);

    private static object MapRestaurant(Restaurant r, bool includeOwnerData) => new
    {
        id = r.Id, name = r.Name, description = r.Description,
        cuisineType = r.CuisineType, priceRange = r.PriceRange,
        address = r.Address, city = r.City, phone = r.Phone, website = r.Website,
        latitude = r.Latitude, longitude = r.Longitude,
        rating = r.Rating, reviewCount = r.ReviewCount,
        foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
        imagesUrl = r.ImagesUrl, amenities = r.Amenities, policies = r.Policies,
        openingHours = r.OpeningHours, specialHours = r.SpecialHours, totalSlots = r.TotalSlots, hourlySlots = r.HourlySlots,
        depositRequired = r.DepositRequired, depositPerGuest = r.DepositPerGuest,
        discount = r.Discount, discountTimeStart = r.DiscountTimeStart, discountTimeEnd = r.DiscountTimeEnd,
        isApproved = r.IsApproved, isSuspended = r.IsSuspended,
        ownerId = r.OwnerId, createdAt = r.CreatedAt,
        pendingChanges = includeOwnerData ? RestaurantChangeSet.FromJson(r.PendingChanges) : null,
        pendingChangesSubmittedAt = includeOwnerData ? r.PendingChangesSubmittedAt : null
    };

    private static object MapRestaurantDetail(Restaurant r) => new
    {
        id = r.Id, name = r.Name, description = r.Description,
        cuisineType = r.CuisineType, priceRange = r.PriceRange,
        address = r.Address, city = r.City, phone = r.Phone, website = r.Website,
        latitude = r.Latitude, longitude = r.Longitude,
        rating = r.Rating, reviewCount = r.ReviewCount,
        foodRating = r.FoodRating, serviceRating = r.ServiceRating, interiorRating = r.InteriorRating,
        imagesUrl = r.ImagesUrl, amenities = r.Amenities, policies = r.Policies,
        openingHours = r.OpeningHours, specialHours = r.SpecialHours, totalSlots = r.TotalSlots, hourlySlots = r.HourlySlots,
        depositRequired = r.DepositRequired, depositPerGuest = r.DepositPerGuest,
        discount = r.Discount, discountTimeStart = r.DiscountTimeStart, discountTimeEnd = r.DiscountTimeEnd,
        isApproved = r.IsApproved, isSuspended = r.IsSuspended,
        ownerId = r.OwnerId, createdAt = r.CreatedAt,
        menu = r.MenuCategories?.Select(c => new
        {
            id = c.Id, name = c.Name, sortOrder = c.SortOrder,
            items = c.Items?.Where(i => i.IsAvailable).Select(i => new
            {
                id = i.Id, name = i.Name, description = i.Description,
                price = i.Price, isPopular = i.IsPopular, imageUrl = i.ImageUrl
            })
        }),
        tables = r.Tables?.Select(t => new
        {
            id = t.Id, label = t.Label, capacity = t.Capacity, location = t.Location
        })
    };
}

public record CreateRestaurantRequest(
    string Name, string Description, string CuisineType, string PriceRange,
    string Address, string City, string Phone, string? Website,
    decimal Latitude, decimal Longitude, string? OpeningHours,
    List<string>? Amenities, List<string>? Policies, List<string>? ImagesUrl,
    int TotalSlots = 30, bool DepositRequired = false, decimal DepositPerGuest = 0, int? HourlySlots = null);

public record UpdateRestaurantRequest(
    string? Name, string? Description, string? CuisineType, string? PriceRange,
    string? Address, string? City, string? Phone, string? Website,
    decimal? Latitude, decimal? Longitude, string? OpeningHours,
    List<string>? Amenities, List<string>? Policies, List<string>? ImagesUrl,
    int? TotalSlots = null, bool? DepositRequired = null, decimal? DepositPerGuest = null,
    int? Discount = null, TimeOnly? DiscountTimeStart = null, TimeOnly? DiscountTimeEnd = null,
    int? HourlySlots = null, string? SpecialHours = null);
