using DineMaster.Api.Extensions;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Api.Controllers;

/// <summary>
/// "Restoran egasimisiz? → Hoziroq qo'shiling": foydalanuvchi hamkorlik arizasini yuboradi,
/// superadmin tasdiqlasa unga restoran egasi (admin) roli beriladi.
/// </summary>
[ApiController]
[Route("api/partner-applications")]
[Authorize]
public class PartnerApplicationsController : ControllerBase
{
    private readonly AppDbContext _db;
    public PartnerApplicationsController(AppDbContext db) { _db = db; }

    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] PartnerApplicationRequest req, CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        if (User.IsAdmin())
            return BadRequest(new { message = "Siz allaqachon restoran egasisiz — boshqaruv paneliga o'ting." });

        if (string.IsNullOrWhiteSpace(req.RestaurantName) || string.IsNullOrWhiteSpace(req.City) ||
            string.IsNullOrWhiteSpace(req.Phone))
            return BadRequest(new { message = "Restoran nomi, shahar va telefon kiritilishi shart." });
        if (req.RestaurantName.Trim().Length > 200 || req.City.Trim().Length > 100 || req.Phone.Trim().Length > 50)
            return BadRequest(new { message = "Maydonlar juda uzun." });

        if (await _db.PartnerApplications.AnyAsync(a => a.UserId == userId && a.Status == PartnerApplication.Pending, ct))
            return Conflict(new { message = "Arizangiz allaqachon ko'rib chiqilmoqda." });

        var app = new PartnerApplication
        {
            UserId = userId,
            RestaurantName = req.RestaurantName.Trim(),
            City = req.City.Trim(),
            Address = req.Address?.Trim() ?? string.Empty,
            Phone = req.Phone.Trim(),
            Message = string.IsNullOrWhiteSpace(req.Message) ? null : req.Message.Trim()
        };
        _db.PartnerApplications.Add(app);

        var superadmins = await _db.UserRoles.Where(r => r.Role == AppRole.Superadmin).Select(r => r.UserId).ToListAsync(ct);
        foreach (var id in superadmins)
            _db.Notifications.Add(new Notification
            {
                UserId = id, Type = "partner_application", Title = "Yangi hamkorlik arizasi",
                Body = $"{app.RestaurantName} ({app.City}) — restoran egasi bo'lish uchun ariza", Link = "/superadmin-dashboard"
            });

        await _db.SaveChangesAsync(ct);
        return Ok(Map(app));
    }

    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        var userId = User.GetUserIdOrThrow();
        var app = await _db.PartnerApplications.Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt).FirstOrDefaultAsync(ct);
        return app is null ? NoContent() : Ok(Map(app));
    }

    internal static object Map(PartnerApplication a) => new
    {
        id = a.Id, userId = a.UserId, restaurantName = a.RestaurantName, city = a.City, address = a.Address,
        phone = a.Phone, message = a.Message, status = a.Status, rejectionReason = a.RejectionReason,
        createdAt = a.CreatedAt, reviewedAt = a.ReviewedAt,
        userFullName = a.User?.Profile?.FullName, userEmail = a.User?.Email
    };
}

public record PartnerApplicationRequest(string RestaurantName, string City, string? Address, string Phone, string? Message);
