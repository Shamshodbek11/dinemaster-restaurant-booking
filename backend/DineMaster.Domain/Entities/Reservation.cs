using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DineMaster.Domain.Entities;

[Table("reservations")]
public class Reservation
{
    [Key][Column("id")] public Guid Id { get; set; } = Guid.NewGuid();
    [Column("user_id")] public Guid UserId { get; set; }
    [Column("restaurant_id")] public Guid RestaurantId { get; set; }
    [Column("table_id")] public Guid? TableId { get; set; }
    [Column("date")] public DateOnly Date { get; set; }
    [Column("time")] public TimeOnly Time { get; set; }
    [Column("guests_count")] public int GuestsCount { get; set; } = 2;
    [MaxLength(20)][Column("status")] public string Status { get; set; } = "pending";
    [Column("special_requests")] public string? SpecialRequests { get; set; }
    [Column("yums_used")] public int YumsUsed { get; set; } = 0;
    [Column("yums_earned")] public int YumsEarned { get; set; } = 0;
    /// <summary>Jami chegirma, % (restoran + yums, ko'pi bilan 50).</summary>
    [Column("total_discount")] public int TotalDiscount { get; set; } = 0;
    /// <summary>Restoranning vaqtga bog'liq chegirmasi, %.</summary>
    [Column("restaurant_discount")] public int RestaurantDiscount { get; set; } = 0;
    /// <summary>Oldindan buyurtmadan ayiriladigan chegirma summasi.</summary>
    [Column("pre_order_discount", TypeName = "numeric(12,2)")] public decimal PreOrderDiscount { get; set; } = 0;
    /// <summary>Bron boshlanishiga oz qolganda bekor qilingan (kelmaslikka tenglashtiriladi).</summary>
    [Column("is_late_cancellation")] public bool IsLateCancellation { get; set; } = false;
    /// <summary>user | restaurant | system</summary>
    [MaxLength(20)][Column("cancelled_by")] public string? CancelledBy { get; set; }
    [Column("cancel_reason")] public string? CancelReason { get; set; }
    /// <summary>To'langan depozit taqdiri: refund_requested | refunded | forfeited.</summary>
    [MaxLength(20)][Column("deposit_outcome")] public string? DepositOutcome { get; set; }
    [Column("deposit_amount", TypeName = "numeric(12,2)")] public decimal DepositAmount { get; set; } = 0;
    [Column("deposit_paid")] public bool DepositPaid { get; set; } = false;
    [Column("is_no_show")] public bool IsNoShow { get; set; } = false;
    [Column("pre_order_total", TypeName = "numeric(12,2)")] public decimal PreOrderTotal { get; set; } = 0;
    [Column("created_at")] public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [Column("updated_at")] public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
    public Restaurant? Restaurant { get; set; }
    public RestaurantTable? Table { get; set; }
    public ICollection<ReservationItem> Items { get; set; } = new List<ReservationItem>();
}
