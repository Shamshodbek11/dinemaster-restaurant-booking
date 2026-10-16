using System.ComponentModel.DataAnnotations;

namespace DineMaster.Application.DTOs.Reservations;

public class ReservationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? UserFullName { get; set; }
    public string? UserPhone { get; set; }
    public Guid RestaurantId { get; set; }
    public string? RestaurantName { get; set; }
    public Guid? TableId { get; set; }
    public string? TableLabel { get; set; }
    public DateOnly Date { get; set; }
    public TimeOnly Time { get; set; }
    public int GuestsCount { get; set; }
    public string Status { get; set; } = "pending";
    public string? SpecialRequests { get; set; }
    public int YumsUsed { get; set; }
    public int YumsEarned { get; set; }
    public int TotalDiscount { get; set; }
    public decimal DepositAmount { get; set; }
    public bool DepositPaid { get; set; }
    public bool IsNoShow { get; set; }
    public decimal PreOrderTotal { get; set; }
    public List<ReservationItemDto> Items { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class ReservationItemDto
{
    public Guid MenuItemId { get; set; }
    public string? MenuItemName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class ReservationItemInput
{
    [Required] public Guid MenuItemId { get; set; }
    [Range(1, 100)] public int Quantity { get; set; } = 1;
}

public class CreateReservationRequest
{
    [Required] public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }
    [Required] public DateOnly Date { get; set; }
    [Required] public TimeOnly Time { get; set; }
    [Range(1, 50)] public int GuestsCount { get; set; } = 2;
    public string? SpecialRequests { get; set; }
    public bool UseYums { get; set; } = false;
    public List<ReservationItemInput> Items { get; set; } = new();
}

public class ProcessBookingResult
{
    public Guid ReservationId { get; set; }
    public int YumsEarned { get; set; }
    /// <summary>true — YumsEarned tashrif yakunlangach hisobga qo'shiladi.</summary>
    public bool YumsAwardedAfterVisit { get; set; }
    public int YumsUsed { get; set; }
    public int SlotsRemaining { get; set; }
    public decimal DepositDue { get; set; }
    /// <summary>"restaurant" — restoran talabi, "no_show" — kelmaslik tarixi sababli.</summary>
    public string? DepositReason { get; set; }
    public decimal PreOrderTotal { get; set; }
    /// <summary>Jami chegirma % (restoran + yums).</summary>
    public int TotalDiscount { get; set; }
    public int RestaurantDiscount { get; set; }
    /// <summary>Oldindan buyurtmadan ayiriladigan summa.</summary>
    public decimal PreOrderDiscount { get; set; }
    /// <summary>Shuncha soat oldin bekor qilinsa — depozit qaytariladi.</summary>
    public int FreeCancellationHours { get; set; }
}

public class ModifyReservationRequest
{
    [Required] public DateOnly Date { get; set; }
    [Required] public TimeOnly Time { get; set; }
    [Range(1, 50)] public int GuestsCount { get; set; } = 2;
    /// <summary>Yangi stol (null va KeepTable=false — stolsiz).</summary>
    public Guid? TableId { get; set; }
    public bool KeepTable { get; set; } = true;
}

public class CancelReservationRequest
{
    [MaxLength(500)] public string? Reason { get; set; }
}

public class CancelResult
{
    /// <summary>refund_requested | refunded | forfeited | null (depozit yo'q edi)</summary>
    public string? DepositOutcome { get; set; }
    public bool IsLateCancellation { get; set; }
}

public class AssignTableRequest
{
    public Guid? TableId { get; set; }
}

public class UpdateReservationStatusRequest
{
    [Required][RegularExpression("^(pending|confirmed|cancelled|completed|no_show)$")]
    public string Status { get; set; } = "pending";
}

public class MarkNoShowRequest { public bool IsNoShow { get; set; } = true; }
public class MarkDepositPaidRequest { public bool DepositPaid { get; set; } = true; }

public class BookingQuote
{
    public decimal DepositPerGuest { get; set; }
    public decimal DepositAmount { get; set; }
    public string? DepositReason { get; set; }
    public int NoShowCount { get; set; }
    public bool EmailConfirmed { get; set; }
    public int FreeCancellationHours { get; set; }
}

public class AvailabilitySlot
{
    public string Time { get; set; } = string.Empty;
    public bool Available { get; set; }
    /// <summary>past | day_full | hour_full</summary>
    public string? Reason { get; set; }
    /// <summary>Shu vaqtdagi restoran chegirmasi, %.</summary>
    public int Discount { get; set; }
}

public class AvailabilityResult
{
    public DateOnly Date { get; set; }
    public string OpeningHours { get; set; } = string.Empty;
    public int TotalSlots { get; set; }
    public int? HourlySlots { get; set; }
    public int Booked { get; set; }
    public List<AvailabilitySlot> Slots { get; set; } = new();
}
