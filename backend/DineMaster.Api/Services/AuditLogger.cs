using System.Security.Claims;
using System.Text.Json;
using DineMaster.Domain.Entities;
using DineMaster.Domain.Enums;
using DineMaster.Infrastructure.Data;

namespace DineMaster.Api.Services;

/// <summary>
/// Faoliyat jurnaliga yozadi. Yozuv DbContext'ga qo'shiladi va chaqiruvchining SaveChanges'i bilan
/// birga saqlanadi (amal bajarilmasa, jurnalga ham tushmaydi).
/// </summary>
public class AuditLogger
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _http;

    public AuditLogger(AppDbContext db, IHttpContextAccessor http)
    {
        _db = db;
        _http = http;
    }

    public void Log(string entityType, Guid? entityId, Guid? restaurantId, string action, string summary,
        IReadOnlyDictionary<string, AuditChange>? changes = null)
    {
        var user = _http.HttpContext?.User;
        Guid? actorId = Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
        var role = user is null || actorId is null ? "system"
            : user.IsInRole(Roles.Superadmin) ? Roles.Superadmin
            : user.IsInRole(Roles.Admin) ? Roles.Admin
            : Roles.User;

        _db.AuditLogs.Add(new AuditLog
        {
            ActorId = actorId,
            ActorRole = role,
            EntityType = entityType,
            EntityId = entityId,
            RestaurantId = restaurantId,
            Action = action,
            Summary = summary,
            Changes = changes is { Count: > 0 } ? JsonSerializer.Serialize(changes, Json) : null
        });
    }
}

public record AuditChange(object? Old, object? New);

/// <summary>Faqat haqiqatda o'zgargan maydonlarni yig'adi.</summary>
public sealed class AuditDiff
{
    private readonly Dictionary<string, AuditChange> _changes = new();

    public AuditDiff Add(string field, object? oldValue, object? newValue)
    {
        if (!Same(oldValue, newValue)) _changes[field] = new AuditChange(oldValue, newValue);
        return this;
    }

    public IReadOnlyDictionary<string, AuditChange> Changes => _changes;
    public bool Any => _changes.Count > 0;
    public string Fields => string.Join(", ", _changes.Keys);

    private static bool Same(object? a, object? b)
    {
        if (a is null || b is null) return a is null && b is null;
        if (a is System.Collections.IEnumerable ea && a is not string &&
            b is System.Collections.IEnumerable eb && b is not string)
            return ea.Cast<object?>().SequenceEqual(eb.Cast<object?>());
        return a.Equals(b);
    }
}
