using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

/// <summary>
/// Emailga yuborilgan bir martalik kod (emailni tasdiqlash, parolni tiklash).
/// Kodning o'zi saqlanmaydi — faqat hash.
/// </summary>
[Table("email_codes")]
public class EmailCode
{
    public const string PurposeVerifyEmail = "verify_email";
    public const string PurposeResetPassword = "reset_password";

    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Required][MaxLength(255)][Column("email")] public string Email { get; set; } = string.Empty;
    [Required][MaxLength(30)][Column("purpose")] public string Purpose { get; set; } = PurposeVerifyEmail;
    [Required][MaxLength(64)][Column("code_hash")] public string CodeHash { get; set; } = string.Empty;
    [Column("attempts")] public int Attempts { get; set; }
    [Column("expires_at")] public DateTime ExpiresAt { get; set; }
    [Column("consumed_at")] public DateTime? ConsumedAt { get; set; }
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
