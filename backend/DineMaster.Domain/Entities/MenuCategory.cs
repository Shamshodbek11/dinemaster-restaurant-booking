using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("menu_categories")]
public class MenuCategory
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Required][MaxLength(100)][Column("name")] public string Name { get; set; } = string.Empty;
    [Column("sort_order")] public int SortOrder { get; set; } = 0;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Restaurant? Restaurant { get; set; }
    public ICollection<MenuItem> Items { get; set; } = new List<MenuItem>();
}
