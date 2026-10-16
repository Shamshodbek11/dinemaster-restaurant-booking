using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("waitlist")]
public class Waitlist
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Column("date")] public DateOnly Date { get; set; }
    [Column("time")] public TimeOnly? Time { get; set; }
    [Column("guests_count")] public int GuestsCount { get; set; } = 2;
    [Column("position")] public int Position { get; set; } = 1;
    [MaxLength(20)][Column("status")] public string Status { get; set; } = "waiting";
    [Column("note")] public string? Note { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AppUser? User { get; set; }
    public Restaurant? Restaurant { get; set; }
}
