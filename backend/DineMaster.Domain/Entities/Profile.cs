using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("profiles")]
public class Profile
{
    [Key][Column("id")]
    public Guid Id { get; set; }

    [Required][MaxLength(255)][Column("email")]
    public string Email { get; set; } = string.Empty;

    [MaxLength(255)][Column("full_name")]
    public string FullName { get; set; } = string.Empty;

    [Column("avatar_url")]
    public string? AvatarUrl { get; set; }

    [MaxLength(50)][Column("phone")]
    public string? Phone { get; set; }

    [Column("yums_balance")]
    public int YumsBalance { get; set; } = 0;

    [MaxLength(10)][Column("preferred_language")]
    public string PreferredLanguage { get; set; } = "uz";

    [Column("bio")]
    public string? Bio { get; set; }

    [Column("dietary_preferences")]
    public List<string> DietaryPreferences { get; set; } = new();

    [Column("is_banned")]
    public bool IsBanned { get; set; } = false;

    /// <summary>Do'st taklif qilish kodi (havola: /signup?ref=KOD).</summary>
    [MaxLength(12)][Column("referral_code")]
    public string? ReferralCode { get; set; }

    /// <summary>Kim taklif qilgan (taklif qilgan foydalanuvchi ID).</summary>
    [Column("referred_by")]
    public Guid? ReferredBy { get; set; }

    /// <summary>Taklif qilganga birinchi tashrif uchun bonus berilganmi.</summary>
    [Column("referral_rewarded")]
    public bool ReferralRewarded { get; set; }

    /// <summary>Telegram bot orqali bildirishnoma (chat ID).</summary>
    [Column("telegram_chat_id")]
    public long? TelegramChatId { get; set; }

    /// <summary>Telegram'ni ulash uchun bir martalik kod (/start KOD).</summary>
    [MaxLength(20)][Column("telegram_link_code")]
    public string? TelegramLinkCode { get; set; }

    /// <summary>Foydalanish shartlari va maxfiylik siyosatiga rozilik vaqti.</summary>
    [Column("terms_accepted_at")]
    public DateTime? TermsAcceptedAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
}
