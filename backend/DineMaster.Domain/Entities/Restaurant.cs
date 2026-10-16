using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("restaurants")]
public class Restaurant
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("owner_id")] public Guid? OwnerId { get; set; }
    [Required][MaxLength(255)][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("description")] public string Description { get; set; } = string.Empty;
    [MaxLength(100)][Column("cuisine_type")] public string CuisineType { get; set; } = string.Empty;
    [MaxLength(20)][Column("price_range")] public string PriceRange { get; set; } = "$$";
    [Column("address")] public string Address { get; set; } = string.Empty;
    [MaxLength(100)][Column("city")] public string City { get; set; } = string.Empty;
    [Column("latitude", TypeName = "numeric(10,7)")] public decimal? Latitude { get; set; }
    [Column("longitude", TypeName = "numeric(10,7)")] public decimal? Longitude { get; set; }
    [MaxLength(50)][Column("phone")] public string? Phone { get; set; }
    [MaxLength(255)][Column("website")] public string? Website { get; set; }
    [Column("opening_hours", TypeName = "jsonb")] public string OpeningHours { get; set; } = "{}";
    /// <summary>Bayram/maxsus kunlar: {"2026-12-31":"closed","2027-01-01":"12:00-20:00"} — oddiy jadvaldan ustun.</summary>
    [Column("special_hours", TypeName = "jsonb")] public string? SpecialHours { get; set; }
    [Column("total_slots")] public int TotalSlots { get; set; } = 20;
    /// <summary>Bir soat ichida boshlanadigan bronlar soni chegarasi (null — cheklanmagan).</summary>
    [Column("hourly_slots")] public int? HourlySlots { get; set; }
    /// <summary>Egasining superadmin tasdig'ini kutayotgan o'zgarishlari (JSON), null — yo'q.</summary>
    [Column("pending_changes", TypeName = "jsonb")] public string? PendingChanges { get; set; }
    [Column("pending_changes_submitted_at")] public DateTime? PendingChangesSubmittedAt { get; set; }
    [Column("images_url")] public List<string> ImagesUrl { get; set; } = new();
    [Column("rating", TypeName = "numeric(3,1)")] public decimal Rating { get; set; } = 0;
    [Column("review_count")] public int ReviewCount { get; set; } = 0;
    [Column("discount")] public int? Discount { get; set; }
    [Column("discount_time_start")] public TimeOnly? DiscountTimeStart { get; set; }
    [Column("discount_time_end")] public TimeOnly? DiscountTimeEnd { get; set; }
    [Column("is_approved")] public bool IsApproved { get; set; } = false;
    [Column("is_suspended")] public bool IsSuspended { get; set; } = false;
    [Column("rejection_reason")] public string? RejectionReason { get; set; }
    [Column("amenities")] public List<string> Amenities { get; set; } = new();
    [Column("policies")] public List<string> Policies { get; set; } = new();
    [Column("deposit_required")] public bool DepositRequired { get; set; } = false;
    [Column("deposit_per_guest", TypeName = "numeric(12,2)")] public decimal DepositPerGuest { get; set; } = 0;
    [Column("food_rating", TypeName = "numeric(3,1)")] public decimal FoodRating { get; set; } = 0;
    [Column("service_rating", TypeName = "numeric(3,1)")] public decimal ServiceRating { get; set; } = 0;
    [Column("interior_rating", TypeName = "numeric(3,1)")] public decimal InteriorRating { get; set; } = 0;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? Owner { get; set; }
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public ICollection<MenuCategory> MenuCategories { get; set; } = new List<MenuCategory>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
    public ICollection<RestaurantTable> Tables { get; set; } = new List<RestaurantTable>();
    public ICollection<Waitlist> WaitlistEntries { get; set; } = new List<Waitlist>();
    public ICollection<RestaurantStaff> Staff { get; set; } = new List<RestaurantStaff>();
}
