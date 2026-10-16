using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

/// <summary>
/// Restoran xodimi: manager — menyu, stollar, bronlar, hisobotlar; host — bronlar, kutish ro'yxati,
/// mijozni kutib olish (Keldi/Kelmadi). Sozlamalar va xodimlarni faqat egasi boshqaradi.
/// </summary>
[Table("restaurant_staff")]
public class RestaurantStaff
{
    public const string Manager = "manager";
    public const string Host = "host";

    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Required][MaxLength(20)][Column("role")] public string Role { get; set; } = Host;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Restaurant? Restaurant { get; set; }
    public AppUser? User { get; set; }
}
