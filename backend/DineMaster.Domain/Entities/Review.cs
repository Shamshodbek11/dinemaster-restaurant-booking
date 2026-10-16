using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("reviews")]
public class Review
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Column("reservation_id")] public Guid? ReservationId { get; set; }
    [Range(1, 10)][Column("rating")] public int Rating { get; set; }
    [Range(0, 10)][Column("food_rating")] public int FoodRating { get; set; } = 0;
    [Range(0, 10)][Column("service_rating")] public int ServiceRating { get; set; } = 0;
    [Range(0, 10)][Column("interior_rating")] public int InteriorRating { get; set; } = 0;
    [Column("comment")] public string? Comment { get; set; }
    [Column("owner_response")] public string? OwnerResponse { get; set; }
    [Column("owner_responded_at")] public DateTime? OwnerRespondedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public AppUser? User { get; set; }
    public Restaurant? Restaurant { get; set; }
}
