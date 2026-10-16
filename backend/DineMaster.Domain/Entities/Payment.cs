using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

/// <summary>
/// Bron depoziti uchun to'lov tranzaksiyasi (Payme, Click yoki test provayder).
/// Holatlar Payme konvensiyasiga mos: 1 — yaratildi (kutilmoqda), 2 — to'landi,
/// -1 — to'lovdan oldin bekor qilindi, -2 — to'lovdan keyin bekor qilindi (pul qaytarildi).
/// </summary>
[Table("payments")]
public class Payment
{
    public const int StateCreated = 1;
    public const int StatePaid = 2;
    public const int StateCancelled = -1;
    public const int StateRefunded = -2;

    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Raqamli identifikator (Click merchant_prepare_id uchun).</summary>
    [Column("number")] public long Number { get; set; }

    [Column("reservation_id")] public Guid ReservationId { get; set; }
    [Column("user_id")] public Guid UserId { get; set; }
    [Required][MaxLength(20)][Column("provider")] public string Provider { get; set; } = string.Empty;

    /// <summary>Provayderdagi tranzaksiya ID (Payme id, Click click_trans_id).</summary>
    [MaxLength(100)][Column("provider_transaction_id")] public string? ProviderTransactionId { get; set; }

    [Column("amount", TypeName = "numeric(12,2)")] public decimal Amount { get; set; }
    [Column("state")] public int State { get; set; } = StateCreated;
    [Column("cancel_reason")] public int? CancelReason { get; set; }

    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    [Column("performed_at")] public DateTime? PerformedAt { get; set; }
    [Column("cancelled_at")] public DateTime? CancelledAt { get; set; }
    /// <summary>Bron bekor qilinib, pulni qaytarish kerak (provayder kabinetida qaytarilgach — StateRefunded).</summary>
    [Column("refund_requested_at")] public DateTime? RefundRequestedAt { get; set; }

    public Reservation? Reservation { get; set; }
}
