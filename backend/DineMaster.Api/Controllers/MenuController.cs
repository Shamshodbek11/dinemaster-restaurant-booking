using DineMaster.Api.Services;
using DineMaster.Api.Extensions;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;
    private readonly RestaurantAccess _access;
    public MenuController(AppDbContext db, AuditLogger audit, RestaurantAccess access) { _db = db; _audit = audit; _access = access; }

    [HttpGet("restaurant/{restaurantId:guid}")]
    [HttpGet("/api/restaurants/{restaurantId:guid}/menu")]
    public async Task<IActionResult> GetMenu(Guid restaurantId, CancellationToken ct)
    {
        var cats = await _db.MenuCategories
            .Where(c => c.RestaurantId == restaurantId)
            .Include(c => c.Items.Where(i => i.IsAvailable))
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
        return Ok(cats.Select(MapCategory));
    }

    [HttpGet("/api/restaurants/{restaurantId:guid}/menu/all")]
    public async Task<IActionResult> GetMenuAll(Guid restaurantId, CancellationToken ct)
    {
        var cats = await _db.MenuCategories
            .Where(c => c.RestaurantId == restaurantId)
            .Include(c => c.Items)
            .OrderBy(c => c.SortOrder)
            .ToListAsync(ct);
        return Ok(cats.Select(MapCategory));
    }

    [HttpPost("categories")]
    [HttpPost("/api/restaurants/{restaurantId:guid}/menu/categories")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> CreateCategory([FromRoute] Guid? restaurantId, [FromBody] CreateCategoryRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Kategoriya nomi kiritilishi shart." });
        var targetRestaurantId = restaurantId.GetValueOrDefault(req.RestaurantId);
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Id == targetRestaurantId, ct);
        if (restaurant is null) return NotFound();
        await _access.RequireAsync(restaurant.Id, AccessLevel.Manager, ct);
        var cat = new MenuCategory { RestaurantId = targetRestaurantId, Name = req.Name.Trim(), SortOrder = req.SortOrder };
        _db.MenuCategories.Add(cat);
        _audit.Log("menu_category", cat.Id, targetRestaurantId, "create", $"Menyu kategoriyasi qo'shildi: {cat.Name}");
        await _db.SaveChangesAsync(ct);
        return Ok(MapCategory(cat));
    }

    [HttpPut("/api/restaurants/{restaurantId:guid}/menu/categories/{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> UpdateCategory(Guid restaurantId, Guid id, [FromBody] UpdateCategoryRequest req, CancellationToken ct)
    {
        var cat = await _db.MenuCategories.Include(c => c.Restaurant).Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == restaurantId, ct);
        if (cat is null) return NotFound();
        await _access.RequireAsync(cat.RestaurantId, AccessLevel.Manager, ct);

        if (req.Name is not null && string.IsNullOrWhiteSpace(req.Name))
            return BadRequest(new { message = "Kategoriya nomi bo'sh bo'lmasligi kerak." });
        cat.Name = req.Name?.Trim() ?? cat.Name;
        cat.SortOrder = req.SortOrder ?? cat.SortOrder;
        var catDiff = new AuditDiff()
            .Add("Nomi", _db.Entry(cat).Property(c => c.Name).OriginalValue, cat.Name)
            .Add("Tartib", _db.Entry(cat).Property(c => c.SortOrder).OriginalValue, cat.SortOrder);
        if (catDiff.Any)
            _audit.Log("menu_category", cat.Id, cat.RestaurantId, "update", $"Menyu kategoriyasi o'zgartirildi: {cat.Name}", catDiff.Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(MapCategory(cat));
    }

    [HttpDelete("/api/restaurants/{restaurantId:guid}/menu/categories/{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> DeleteCategory(Guid restaurantId, Guid id, CancellationToken ct)
    {
        var cat = await _db.MenuCategories.Include(c => c.Restaurant)
            .FirstOrDefaultAsync(c => c.Id == id && c.RestaurantId == restaurantId, ct);
        if (cat is null) return NotFound();
        await _access.RequireAsync(cat.RestaurantId, AccessLevel.Manager, ct);

        // Kategoriyadagi taomlar oldindan buyurtmalarda bo'lsa, o'chirish buyurtma tarixini buzadi
        var usedInOrders = await _db.ReservationItems.AnyAsync(ri => ri.MenuItem!.CategoryId == id, ct);
        if (usedInOrders)
            return Conflict(new { message = "Bu kategoriyadagi taomlar bronlarda buyurtma qilingan. Taomlarni \"mavjud emas\" qilib qo'ying." });

        _audit.Log("menu_category", cat.Id, cat.RestaurantId, "delete", $"Menyu kategoriyasi o'chirildi: {cat.Name}");
        _db.MenuCategories.Remove(cat);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "O'chirildi." });
    }

    [HttpPost("items")]
    [HttpPost("/api/restaurants/{restaurantId:guid}/menu/items")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> CreateItem([FromRoute] Guid? restaurantId, [FromBody] CreateMenuItemRequest req, CancellationToken ct)
    {
        var itemError = ValidateItem(req);
        if (itemError is not null) return BadRequest(new { message = itemError });
        var cat = await _db.MenuCategories.Include(c => c.Restaurant).FirstOrDefaultAsync(c => c.Id == req.CategoryId, ct);
        if (cat is null) return NotFound();
        if (restaurantId.HasValue && cat.RestaurantId != restaurantId.Value) return BadRequest(new { message = "Kategoriya boshqa restoranga tegishli." });
        await _access.RequireAsync(cat.RestaurantId, AccessLevel.Manager, ct);
        var item = new MenuItem {
            CategoryId = req.CategoryId, RestaurantId = cat.RestaurantId,
            Name = req.Name.Trim(), Description = req.Description ?? string.Empty, Price = req.Price,
            IsPopular = req.IsPopular, IsAvailable = req.IsAvailable, ImageUrl = req.ImageUrl,
            SortOrder = req.SortOrder
        };
        _db.MenuItems.Add(item);
        _audit.Log("menu_item", item.Id, item.RestaurantId, "create", $"Taom qo'shildi: {item.Name} — {item.Price:N0} so'm");
        await _db.SaveChangesAsync(ct);
        return Ok(MapItem(item));
    }

    [HttpPut("items/{id:guid}")]
    [HttpPut("/api/restaurants/{restaurantId:guid}/menu/items/{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> UpdateItem([FromRoute] Guid? restaurantId, Guid id, [FromBody] CreateMenuItemRequest req, CancellationToken ct)
    {
        var itemError = ValidateItem(req);
        if (itemError is not null) return BadRequest(new { message = itemError });
        var item = await _db.MenuItems.Include(i => i.Restaurant).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return NotFound();
        if (restaurantId.HasValue && item.RestaurantId != restaurantId.Value) return BadRequest(new { message = "Taom boshqa restoranga tegishli." });
        await _access.RequireAsync(item.RestaurantId, AccessLevel.Manager, ct);
        var categoryOk = await _db.MenuCategories.AnyAsync(c => c.Id == req.CategoryId && c.RestaurantId == item.RestaurantId, ct);
        if (!categoryOk) return BadRequest(new { message = "Kategoriya shu restoranga tegishli emas." });
        item.CategoryId = req.CategoryId;
        item.Name = req.Name.Trim(); item.Description = req.Description ?? string.Empty; item.Price = req.Price;
        item.IsPopular = req.IsPopular; item.IsAvailable = req.IsAvailable; item.ImageUrl = req.ImageUrl;
        item.SortOrder = req.SortOrder;
        var e = _db.Entry(item);
        var itemDiff = new AuditDiff()
            .Add("Nomi", e.Property(i => i.Name).OriginalValue, item.Name)
            .Add("Narx", e.Property(i => i.Price).OriginalValue, item.Price)
            .Add("Tavsif", e.Property(i => i.Description).OriginalValue, item.Description)
            .Add("Kategoriya", e.Property(i => i.CategoryId).OriginalValue, item.CategoryId)
            .Add("Mavjud", e.Property(i => i.IsAvailable).OriginalValue, item.IsAvailable)
            .Add("Mashhur", e.Property(i => i.IsPopular).OriginalValue, item.IsPopular)
            .Add("Rasm", e.Property(i => i.ImageUrl).OriginalValue, item.ImageUrl);
        if (itemDiff.Any)
            _audit.Log("menu_item", item.Id, item.RestaurantId, "update", $"Taom o'zgartirildi: {item.Name} ({itemDiff.Fields})", itemDiff.Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(MapItem(item));
    }

    [HttpDelete("items/{id:guid}")]
    [HttpDelete("/api/restaurants/{restaurantId:guid}/menu/items/{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> DeleteItem([FromRoute] Guid? restaurantId, Guid id, CancellationToken ct)
    {
        var item = await _db.MenuItems.Include(i => i.Restaurant).FirstOrDefaultAsync(i => i.Id == id, ct);
        if (item is null) return NotFound();
        if (restaurantId.HasValue && item.RestaurantId != restaurantId.Value) return BadRequest(new { message = "Taom boshqa restoranga tegishli." });
        await _access.RequireAsync(item.RestaurantId, AccessLevel.Manager, ct);

        // Taom bronlarda oldindan buyurtma qilingan bo'lsa, o'chirilmaydi (FK + buyurtma tarixi),
        // faqat menyudan yashiriladi
        if (await _db.ReservationItems.AnyAsync(ri => ri.MenuItemId == id, ct))
        {
            item.IsAvailable = false;
            _audit.Log("menu_item", item.Id, item.RestaurantId, "hide", $"Taom menyudan yashirildi: {item.Name}");
            await _db.SaveChangesAsync(ct);
            return Ok(new { message = "Taom buyurtmalarda ishlatilgan, shuning uchun menyudan yashirildi.", hidden = true });
        }

        _audit.Log("menu_item", item.Id, item.RestaurantId, "delete", $"Taom o'chirildi: {item.Name} — {item.Price:N0} so'm");
        _db.MenuItems.Remove(item);
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "O'chirildi." });
    }

    private static string? ValidateItem(CreateMenuItemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return "Taom nomi kiritilishi shart.";
        if (req.Name.Trim().Length > 200) return "Taom nomi 200 belgidan oshmasligi kerak.";
        if (req.Price < 0) return "Narx manfiy bo'lishi mumkin emas.";
        return null;
    }

    private static object MapCategory(MenuCategory c) => new
    {
        id = c.Id,
        restaurantId = c.RestaurantId,
        name = c.Name,
        sortOrder = c.SortOrder,
        createdAt = c.CreatedAt,
        items = (c.Items ?? new List<MenuItem>()).OrderBy(i => i.SortOrder).Select(MapItem)
    };

    private static object MapItem(MenuItem i) => new
    {
        id = i.Id,
        categoryId = i.CategoryId,
        restaurantId = i.RestaurantId,
        name = i.Name,
        description = i.Description,
        price = i.Price,
        imageUrl = i.ImageUrl,
        isAvailable = i.IsAvailable,
        isPopular = i.IsPopular,
        sortOrder = i.SortOrder,
        createdAt = i.CreatedAt
    };
}

public record CreateCategoryRequest(Guid RestaurantId, string Name, int SortOrder = 0);
public record UpdateCategoryRequest(string? Name, int? SortOrder = null);
public record CreateMenuItemRequest(
    Guid CategoryId,
    string Name,
    string? Description,
    decimal Price,
    bool IsPopular = false,
    string? ImageUrl = null,
    bool IsAvailable = true,
    int SortOrder = 0);
