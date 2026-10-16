using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("menu_items")]
public class MenuItem
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("category_id")] public Guid CategoryId { get; set; }
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Required][MaxLength(200)][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("description")] public string Description { get; set; } = string.Empty;
    [Column("price", TypeName = "numeric(12,2)")] public decimal Price { get; set; }
    [Column("image_url")] public string? ImageUrl { get; set; }
    [Column("is_available")] public bool IsAvailable { get; set; } = true;
    [Column("is_popular")] public bool IsPopular { get; set; } = false;
    [Column("sort_order")] public int SortOrder { get; set; } = 0;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public MenuCategory? Category { get; set; }
    public Restaurant? Restaurant { get; set; }
}
