using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("restaurant_tables")]
public class RestaurantTable
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Required][MaxLength(50)][Column("label")] public string Label { get; set; } = string.Empty;
    [Column("capacity")] public int Capacity { get; set; } = 2;
    [MaxLength(30)][Column("location")] public string Location { get; set; } = "hall";
    [Column("is_active")] public bool IsActive { get; set; } = true;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Restaurant? Restaurant { get; set; }
}
