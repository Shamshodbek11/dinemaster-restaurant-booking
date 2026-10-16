using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

/// <summary>
/// Faoliyat jurnali: kim, qachon, nimani o'zgartirdi (restoran sozlamalari, menyu narxi, stollar,
/// bron statuslari, superadmin amallari). Superadmin "Faoliyat jurnali" bo'limida ko'radi.
/// </summary>
[Table("audit_logs")]
public class AuditLog
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("actor_id")] public Guid? ActorId { get; set; }
    [MaxLength(20)][Column("actor_role")] public string ActorRole { get; set; } = "system";
    /// <summary>restaurant, menu_item, menu_category, table, reservation, venue, venue_reservation, user, review, partner_application, notification</summary>
    [Required][MaxLength(40)][Column("entity_type")] public string EntityType { get; set; } = string.Empty;
    [Column("entity_id")] public Guid? EntityId { get; set; }
    [Column("restaurant_id")] public Guid? RestaurantId { get; set; }
    /// <summary>create, update, delete, status, approve, reject ...</summary>
    [Required][MaxLength(30)][Column("action")] public string Action { get; set; } = string.Empty;
    [Required][Column("summary")] public string Summary { get; set; } = string.Empty;
    /// <summary>{"maydon": {"old": ..., "new": ...}}</summary>
    [Column("changes", TypeName = "jsonb")] public string? Changes { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
