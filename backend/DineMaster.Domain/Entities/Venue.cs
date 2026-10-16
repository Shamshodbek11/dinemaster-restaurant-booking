using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("venues")]
public class Venue
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("owner_id")] public Guid? OwnerId { get; set; }
    [Required][MaxLength(255)][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("description")] public string Description { get; set; } = string.Empty;
    [MaxLength(50)][Column("venue_type")] public string VenueType { get; set; } = "To'yxona";
    [Column("address")] public string Address { get; set; } = string.Empty;
    [MaxLength(100)][Column("city")] public string City { get; set; } = "Toshkent";
    [MaxLength(50)][Column("phone")] public string? Phone { get; set; }
    [Column("capacity_min")] public int CapacityMin { get; set; } = 50;
    [Column("capacity_max")] public int CapacityMax { get; set; } = 1000;
    [Column("price_per_head")] public int PricePerHead { get; set; } = 0;
    [Column("price_per_hour")] public int PricePerHour { get; set; } = 0;
    [Column("images_url")] public List<string> ImagesUrl { get; set; } = new();
    [Column("rating", TypeName = "numeric(3,1)")] public decimal Rating { get; set; } = 0;
    [Column("review_count")] public int ReviewCount { get; set; } = 0;
    [Column("is_approved")] public bool IsApproved { get; set; } = false;
    [Column("is_suspended")] public bool IsSuspended { get; set; } = false;
    [Column("rejection_reason")] public string? RejectionReason { get; set; }
    [Column("opening_hours", TypeName = "jsonb")] public string OpeningHours { get; set; } = "{}";
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public AppUser? Owner { get; set; }
    public ICollection<VenueReservation> Reservations { get; set; } = new List<VenueReservation>();
}
