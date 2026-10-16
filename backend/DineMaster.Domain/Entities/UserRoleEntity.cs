using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DineMaster.Domain.Enums;

namespace DineMaster.Domain.Entities;

[Table("user_roles")]
public class UserRoleEntity
{
    [Key][Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("role")]
    public AppRole Role { get; set; } = AppRole.User;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
