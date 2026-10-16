using DineMaster.Application.DTOs.Reservations;

namespace DineMaster.Application.Abstractions;

public interface IBookingService
{
    Task<ProcessBookingResult> ProcessBookingAsync(Guid userId, CreateReservationRequest req, CancellationToken ct);

    /// <summary>Mijoz bron sanasi/vaqti/mehmonlar sonini o'zgartiradi (bron boshlanishidan oldin).</summary>
    Task<ProcessBookingResult> ModifyBookingAsync(Guid userId, Guid reservationId, ModifyReservationRequest req, CancellationToken ct);

    /// <summary>Mijoz bekor qiladi: depozit siyosati qo'llanadi.</summary>
    Task<CancelResult> CancelBookingAsync(Guid userId, Guid reservationId, string? reason, CancellationToken ct);

    /// <summary>Restoran xodimi / superadmin status o'zgartiradi (ruxsat chaqiruvchida tekshiriladi).</summary>
    Task<string> ChangeStatusAsync(Guid reservationId, string newStatus, Guid actorId, string? reason, CancellationToken ct);

    /// <summary>Bronga stol biriktirish (null — olib tashlash). Stol nomini qaytaradi.</summary>
    Task<string?> AssignTableAsync(Guid reservationId, Guid? tableId, CancellationToken ct);

    /// <summary>Vaqti o'tgan tasdiqlangan bronlarni "completed" qiladi va tashrif bonusini beradi.</summary>
    Task<int> CompletePastReservationsAsync(CancellationToken ct);

    /// <summary>Depozit belgilangan vaqtda to'lanmagan bronlarni bekor qiladi.</summary>
    Task<int> CancelUnpaidDepositsAsync(CancellationToken ct);

    /// <summary>Bron oldidan: depozit kerakmi (restoran talabi / kelmaslik tarixi), email tasdiqlanganmi.</summary>
    Task<BookingQuote> GetQuoteAsync(Guid userId, Guid restaurantId, int guests, CancellationToken ct);

    /// <summary>Kun bo'yicha bron qilish mumkin bo'lgan vaqtlar (ish vaqti, kunlik/soatlik sig'im).</summary>
    Task<AvailabilityResult> GetAvailabilityAsync(Guid restaurantId, DateOnly date, CancellationToken ct);

    /// <summary>Berilgan restoranlardan qaysilarida shu sana/vaqtda N kishiga joy bor.</summary>
    Task<HashSet<Guid>> FindAvailableAsync(IReadOnlyCollection<Guid> restaurantIds, DateOnly date, TimeOnly time, int guests, CancellationToken ct);
}
