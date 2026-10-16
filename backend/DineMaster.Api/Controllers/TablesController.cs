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
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AuditLogger _audit;
    private readonly RestaurantAccess _access;
    public TablesController(AppDbContext db, AuditLogger audit, RestaurantAccess access) { _db = db; _audit = audit; _access = access; }

    [HttpGet("restaurant/{restaurantId:guid}")]
    public async Task<IActionResult> ByRestaurant(Guid restaurantId, CancellationToken ct)
    {
        var tables = await _db.RestaurantTables
            .Where(t => t.RestaurantId == restaurantId && t.IsActive)
            .OrderBy(t => t.Capacity).ThenBy(t => t.Label)
            .ToListAsync(ct);
        return Ok(tables.Select(t => new { t.Id, t.Label, t.Capacity, t.Location, t.IsActive }));
    }

    [HttpGet("restaurant/{restaurantId:guid}/availability")]
    public async Task<IActionResult> Availability(
        Guid restaurantId,
        [FromQuery] DateOnly date,
        [FromQuery] TimeOnly time,
        [FromQuery] int guests = 2,
        CancellationToken ct = default)
    {
        // Oldingi/keyingi kun ham olinadi: yarim tun atrofidagi bronlar ham hisobga kiradi
        var dayBefore = date.AddDays(-1);
        var dayAfter = date.AddDays(1);
        var nearby = await _db.Reservations
            .Where(r => r.RestaurantId == restaurantId
                        && r.TableId != null
                        && r.Date >= dayBefore && r.Date <= dayAfter
                        && (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending))
            .Select(r => new { TableId = r.TableId!.Value, r.Date, r.Time })
            .ToListAsync(ct);

        var busyTableIds = nearby
            .Where(r => BookingRules.Overlaps(r.Date, r.Time, date, time))
            .Select(r => r.TableId)
            .ToHashSet();

        var tables = await _db.RestaurantTables
            .Where(t => t.RestaurantId == restaurantId && t.IsActive)
            .OrderBy(t => t.Capacity).ThenBy(t => t.Label)
            .ToListAsync(ct);

        return Ok(tables.Select(t => new
        {
            tableId = t.Id,
            t.Label,
            t.Capacity,
            t.Location,
            isAvailable = t.Capacity >= guests && !busyTableIds.Contains(t.Id)
        }));
    }

    [HttpPost]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Create([FromBody] CreateTableRequest req, CancellationToken ct)
    {
        var error = ValidateTable(req);
        if (error is not null) return BadRequest(new { message = error });
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Id == req.RestaurantId, ct);
        if (restaurant is null) return NotFound();
        await _access.RequireAsync(restaurant.Id, AccessLevel.Manager, ct);
        var table = new RestaurantTable {
            RestaurantId = req.RestaurantId, Label = req.Label.Trim(), Capacity = req.Capacity, Location = req.Location, IsActive = true
        };
        _db.RestaurantTables.Add(table);
        _audit.Log("table", table.Id, table.RestaurantId, "create", $"Stol qo'shildi: {table.Label} ({table.Capacity} kishi)");
        await _db.SaveChangesAsync(ct);
        return Ok(new { table.Id, table.Label, table.Capacity, table.Location });
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateTableRequest req, CancellationToken ct)
    {
        var error = ValidateTable(req);
        if (error is not null) return BadRequest(new { message = error });
        var t = await _db.RestaurantTables.Include(x => x.Restaurant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        await _access.RequireAsync(t.RestaurantId, AccessLevel.Manager, ct);
        t.Label = req.Label.Trim(); t.Capacity = req.Capacity; t.Location = req.Location; t.IsActive = req.IsActive;
        var te = _db.Entry(t);
        var tDiff = new AuditDiff()
            .Add("Nomi", te.Property(x => x.Label).OriginalValue, t.Label)
            .Add("Sig'im", te.Property(x => x.Capacity).OriginalValue, t.Capacity)
            .Add("Joylashuv", te.Property(x => x.Location).OriginalValue, t.Location)
            .Add("Faol", te.Property(x => x.IsActive).OriginalValue, t.IsActive);
        if (tDiff.Any)
            _audit.Log("table", t.Id, t.RestaurantId, "update", $"Stol o'zgartirildi: {t.Label} ({tDiff.Fields})", tDiff.Changes);
        await _db.SaveChangesAsync(ct);
        return Ok(new { t.Id, t.Label, t.Capacity, t.Location });
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,superadmin,staff")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var t = await _db.RestaurantTables.Include(x => x.Restaurant).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (t is null) return NotFound();
        await _access.RequireAsync(t.RestaurantId, AccessLevel.Manager, ct);
        // Kelgusi faol bronlari bor stolni o'chirib bo'lmaydi — mehmonlar stolsiz qolmasin
        var today = BusinessTime.Today();
        var hasUpcoming = await _db.Reservations.AnyAsync(r =>
            r.TableId == id && r.Date >= today &&
            (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending), ct);
        if (hasUpcoming)
            return Conflict(new { message = "Bu stolda kelgusi bronlar bor. Avval ularni boshqa stolga o'tkazing yoki bekor qiling." });

        t.IsActive = false;
        _audit.Log("table", t.Id, t.RestaurantId, "delete", $"Stol o'chirildi: {t.Label}");
        await _db.SaveChangesAsync(ct);
        return Ok(new { message = "Stol o'chirildi." });
    }

    private static string? ValidateTable(CreateTableRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Label)) return "Stol nomi kiritilishi shart.";
        if (req.Capacity is < 1 or > 50) return "Stol sig'imi 1 dan 50 gacha bo'lishi kerak.";
        return null;
    }
}
public record CreateTableRequest(Guid RestaurantId, string Label, int Capacity, string Location = "hall", bool IsActive = true);
