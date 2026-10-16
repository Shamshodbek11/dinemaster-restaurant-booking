using DineMaster.Api.Services;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

/// <summary>
/// Restoran xodimlari: egasi menejer yoki host qo'shadi (foydalanuvchi oldin ro'yxatdan o'tgan bo'lishi kerak).
/// manager — menyu, stollar, bronlar, hisobotlar; host — bronlar, kutish ro'yxati, mijozni kutib olish.
/// </summary>
[ApiController]
[Route("api/restaurants/{restaurantId:guid}/staff")]
[Authorize(Roles = "admin,superadmin")]
public class StaffController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly RestaurantAccess _access;
    private readonly AuditLogger _audit;

    public StaffController(AppDbContext db, RestaurantAccess access, AuditLogger audit)
    {
        _db = db; _access = access; _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid restaurantId, CancellationToken ct)
    {
        await _access.RequireAsync(restaurantId, AccessLevel.Owner, ct);
        var staff = await _db.RestaurantStaff.Where(s => s.RestaurantId == restaurantId)
            .OrderBy(s => s.CreatedAt)
            .Select(s => new
            {
                id = s.Id, userId = s.UserId, role = s.Role, createdAt = s.CreatedAt,
                fullName = s.User!.Profile!.FullName, email = s.User.Email
            }).ToListAsync(ct);
        return Ok(staff);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid restaurantId, [FromBody] AddStaffRequest req, CancellationToken ct)
    {
        await _access.RequireAsync(restaurantId, AccessLevel.Owner, ct);
        if (req.Role is not (RestaurantStaff.Manager or RestaurantStaff.Host))
            return BadRequest(new { message = "Rol: manager yoki host." });

        var email = (req.Email ?? string.Empty).Trim().ToLowerInvariant();
        var user = await _db.Users.Include(u => u.Roles).Include(u => u.Profile).FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null)
            return NotFound(new { message = $"Bu email bilan foydalanuvchi topilmadi. Xodim avval {EmailTemplates.Brand}'da ro'yxatdan o'tishi kerak." });

        var restaurant = await _db.Restaurants.FirstAsync(r => r.Id == restaurantId, ct);
        if (restaurant.OwnerId == user.Id) return BadRequest(new { message = "Bu foydalanuvchi restoran egasi." });

        var existing = await _db.RestaurantStaff.FirstOrDefaultAsync(s => s.RestaurantId == restaurantId && s.UserId == user.Id, ct);
        if (existing is not null)
        {
            existing.Role = req.Role;
        }
        else
        {
            existing = new RestaurantStaff { RestaurantId = restaurantId, UserId = user.Id, Role = req.Role };
            _db.RestaurantStaff.Add(existing);
        }
        if (!user.Roles.Any(r => r.Role == AppRole.Staff))
            _db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, Role = AppRole.Staff });

        _db.Notifications.Add(new Notification
        {
            UserId = user.Id, Type = "staff_added", Title = "Siz restoran xodimisiz",
            Body = $"\"{restaurant.Name}\" sizni {(req.Role == RestaurantStaff.Manager ? "menejer" : "host")} sifatida qo'shdi.",
            Link = "/manager"
        });
        _audit.Log("restaurant", restaurantId, restaurantId, "staff_add", $"Xodim qo'shildi: {user.Email} ({req.Role})");
        await _db.SaveChangesAsync(ct);
        return Ok(new { id = existing.Id, userId = user.Id, role = existing.Role, fullName = user.Profile?.FullName, email = user.Email });
    }

    [HttpDelete("{staffId:guid}")]
    public async Task<IActionResult> Remove(Guid restaurantId, Guid staffId, CancellationToken ct)
    {
        await _access.RequireAsync(restaurantId, AccessLevel.Owner, ct);
        var staff = await _db.RestaurantStaff.Include(s => s.User).FirstOrDefaultAsync(s => s.Id == staffId && s.RestaurantId == restaurantId, ct);
        if (staff is null) return NotFound();

        _db.RestaurantStaff.Remove(staff);
        // Boshqa restoranda xodim bo'lmasa — staff rolini ham olamiz
        var elsewhere = await _db.RestaurantStaff.AnyAsync(s => s.UserId == staff.UserId && s.Id != staffId, ct);
        if (!elsewhere)
        {
            var role = await _db.UserRoles.FirstOrDefaultAsync(r => r.UserId == staff.UserId && r.Role == AppRole.Staff, ct);
            if (role is not null) _db.UserRoles.Remove(role);
        }
        _audit.Log("restaurant", restaurantId, restaurantId, "staff_remove", $"Xodim olib tashlandi: {staff.User?.Email}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Xodim olib tashlandi." });
    }
}

public record AddStaffRequest(string Email, string Role);
