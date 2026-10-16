using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("app_users")]
public class AppUser
{
    [Key][Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required][MaxLength(255)][Column("email")]
    public string Email { get; set; } = string.Empty;

    [Required][Column("password_hash")]
    public string PasswordHash { get; set; } = string.Empty;

    [Column("email_confirmed")]
    public bool EmailConfirmed { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Profile? Profile { get; set; }
    public ICollection<UserRoleEntity> Roles { get; set; } = new List<UserRoleEntity>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    public ICollection<Review> Reviews { get; set; } = new List<Review>();
    public ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
}
