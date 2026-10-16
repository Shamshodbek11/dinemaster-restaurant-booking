using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("venue_reservations")]
public class VenueReservation
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("venue_id")] public Guid VenueId { get; set; }
    [Column("event_date")] public DateOnly EventDate { get; set; }
    [Column("date")] public DateOnly Date { get; set; }
    [MaxLength(50)][Column("event_type")] public string EventType { get; set; } = "To'y";
    [Column("guests_count")] public int GuestsCount { get; set; } = 100;
    [Column("special_requests")] public string? SpecialRequests { get; set; }
    [Column("notes")] public string? Notes { get; set; }
    [Column("total_price")] public int TotalPrice { get; set; } = 0;
    [MaxLength(20)][Column("status")] public string Status { get; set; } = "pending";
    [Column("yums_used")] public int YumsUsed { get; set; } = 0;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public AppUser? User { get; set; }
    public Venue? Venue { get; set; }
}
