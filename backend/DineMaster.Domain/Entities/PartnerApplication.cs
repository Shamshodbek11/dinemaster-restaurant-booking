using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

/// <summary>
/// "Restoran egasimisiz?" — hamkorlik arizasi. Superadmin tasdiqlasa, foydalanuvchiga admin
/// (restoran egasi) roli beriladi va u /manager orqali restoranini qo'sha oladi.
/// </summary>
[Table("partner_applications")]
public class PartnerApplication
{
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";

    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Required][MaxLength(200)][Column("restaurant_name")] public string RestaurantName { get; set; } = string.Empty;
    [Required][MaxLength(100)][Column("city")] public string City { get; set; } = string.Empty;
    [Column("address")] public string Address { get; set; } = string.Empty;
    [Required][MaxLength(50)][Column("phone")] public string Phone { get; set; } = string.Empty;
    [Column("message")] public string? Message { get; set; }
    [Required][MaxLength(20)][Column("status")] public string Status { get; set; } = Pending;
    [Column("rejection_reason")] public string? RejectionReason { get; set; }
    [Column("reviewed_by")] public Guid? ReviewedBy { get; set; }
    [Column("reviewed_at")] public DateTime? ReviewedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
