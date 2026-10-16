using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("notifications")]
public class Notification
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [MaxLength(30)][Column("type")] public string Type { get; set; } = "general";
    [Required][MaxLength(200)][Column("title")] public string Title { get; set; } = string.Empty;
    [Column("body")] public string Body { get; set; } = string.Empty;
    [Column("link")] public string? Link { get; set; }
    [Column("is_read")] public bool IsRead { get; set; } = false;
    [Column("reservation_id")] public Guid? ReservationId { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AppUser? User { get; set; }
}
