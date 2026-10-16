using System.Data;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using DineMaster.Application.DTOs.Reservations;
using DineMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineMaster.Application.Services;

/// <summary>
/// Bron biznes mantig'i.
///  - Overbooking: Serializable tranzaksiya + retry (har urinishda ChangeTracker tozalanadi)
///  - Ish vaqti (maxsus kunlar bilan), kunlik va soatlik sig'im, stol to'qnashuvi
///  - Chegirma: restoranning vaqtga bog'liq chegirmasi + yums (jami ko'pi bilan 50%)
///  - Yums tashrif yakunlangach beriladi; taklif qilingan do'stning birinchi tashrifi — taklif qilganga bonus
///  - Bekor qilish siyosati: N soat oldin — depozit qaytariladi, keyin — qaytarilmaydi;
///    juda kech bekor qilish "kelmaslik"ka tenglashtiriladi
///  - Barcha status o'zgarishlari (foydalanuvchi, restoran, tizim) bitta joydan o'tadi va mijozga xabar boradi
/// </summary>
public class BookingService : IBookingService
{
    private const int YumsEarnedPerBooking = 100;
    private const int YumsNeededForDiscount = 1000;
    private const int YumsDiscountPercent = 20;

    private readonly IAppDbContext _db;
    private readonly IEmailService _email;
    private readonly ILogger<BookingService> _log;
    private readonly BookingOptions _options;

    // Tranzaksiya muvaffaqiyatli yakunlangach bajariladigan ishlar (email) — retry'da takrorlanmasin
    private readonly List<Func<Task>> _afterCommit = new();

    public BookingService(IAppDbContext db, IEmailService email, ILogger<BookingService> log, IOptions<BookingOptions> options)
    {
        _db = db;
        _email = email;
        _log = log;
        _options = options.Value;
    }

    private TimeSpan LastBookingGap => TimeSpan.FromMinutes(_options.LastBookingBeforeCloseMinutes);

    // ───────────────────────────── Yangi bron ─────────────────────────────

    public Task<ProcessBookingResult> ProcessBookingAsync(Guid userId, CreateReservationRequest req, CancellationToken ct)
        => InSerializableTransactionAsync(() => ProcessBookingInternalAsync(userId, req, ct), ct);

    private async Task<ProcessBookingResult> ProcessBookingInternalAsync(Guid userId, CreateReservationRequest req, CancellationToken ct)
    {
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId, ct)
            ?? throw new NotFoundException("Profil topilmadi.");
        if (profile.IsBanned)
            throw new ForbiddenAccessException("Hisobingiz bloklangan.");

        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == req.RestaurantId && r.IsApproved && !r.IsSuspended, ct)
            ?? throw new NotFoundException("Restoran topilmadi yoki faol emas.");

        if (_options.RequireConfirmedEmail && !await _db.Users.AnyAsync(u => u.Id == userId && u.EmailConfirmed, ct))
            throw new ForbiddenAccessException("Bron qilish uchun avval emailingizni tasdiqlang.");

        var bookedCount = await ValidateSlotAsync(restaurant, req.Date, req.Time, req.GuestsCount, req.TableId, excludeReservationId: null, ct);

        // Yums chegirma — faqat bron uchun, sotilmaydi
        var yumsUsed = 0;
        var yumsDiscount = 0;
        if (req.UseYums)
        {
            if (profile.YumsBalance < YumsNeededForDiscount)
                throw new BusinessRuleException($"Chegirma uchun kamida {YumsNeededForDiscount} yums kerak.");
            yumsUsed = YumsNeededForDiscount;
            yumsDiscount = YumsDiscountPercent;
        }
        var (restaurantDiscount, totalDiscount) = Discounts(restaurant, req.Time, yumsDiscount);

        // Pre-order: bir xil taom bir necha marta kelsa, miqdori qo'shiladi
        decimal preOrderTotal = 0;
        var resItems = new List<ReservationItem>();
        var requestedItems = (req.Items ?? new List<ReservationItemInput>())
            .GroupBy(i => i.MenuItemId)
            .Select(g => new { MenuItemId = g.Key, Quantity = Math.Clamp(g.Sum(i => i.Quantity), 1, 100) })
            .ToList();
        if (requestedItems.Count > 0)
        {
            var itemIds = requestedItems.Select(i => i.MenuItemId).ToList();
            var menuItems = await _db.MenuItems
                .Where(m => itemIds.Contains(m.Id) && m.RestaurantId == req.RestaurantId && m.IsAvailable)
                .ToDictionaryAsync(m => m.Id, ct);
            foreach (var inp in requestedItems)
            {
                if (!menuItems.TryGetValue(inp.MenuItemId, out var mi))
                    throw new BusinessRuleException("Tanlangan taomlardan biri hozir mavjud emas.");
                preOrderTotal += mi.Price * inp.Quantity;
                resItems.Add(new ReservationItem { MenuItemId = mi.Id, Quantity = inp.Quantity, UnitPrice = mi.Price });
            }
        }
        var preOrderDiscount = Math.Round(preOrderTotal * totalDiscount / 100m, 0);

        // Depozit: restoran talabi yoki mijozning kelmaslik tarixi ("no-show" himoyasi)
        var deposit = await GetDepositRuleAsync(userId, restaurant, ct);
        var depositDue = deposit.PerGuest * req.GuestsCount;

        var reservation = new Reservation
        {
            UserId = userId,
            RestaurantId = req.RestaurantId,
            TableId = req.TableId,
            Date = req.Date,
            Time = req.Time,
            GuestsCount = req.GuestsCount,
            SpecialRequests = req.SpecialRequests,
            YumsUsed = yumsUsed,
            YumsEarned = YumsEarnedPerBooking, // tashrif yakunlangach beriladi
            TotalDiscount = totalDiscount,
            RestaurantDiscount = restaurantDiscount,
            PreOrderDiscount = preOrderDiscount,
            DepositAmount = depositDue,
            DepositPaid = depositDue == 0,
            PreOrderTotal = preOrderTotal,
            Status = BookingRules.Confirmed,
            Items = resItems
        };
        _db.Reservations.Add(reservation);

        _db.Notifications.Add(new Notification
        {
            UserId = userId,
            Type = "booking_confirmed",
            Title = "Bron tasdiqlandi",
            Body = $"{restaurant.Name} — {req.Date:dd.MM.yyyy} {req.Time:HH:mm} • {req.GuestsCount} kishi. " +
                   (totalDiscount > 0 ? $"Chegirma: {totalDiscount}%. " : string.Empty) +
                   (depositDue > 0
                       ? $"Depozit: {depositDue:N0} so'm — {_options.DepositHoldMinutes} daqiqa ichida to'lang. " +
                         $"Bekor qilish {_options.FreeCancellationHours} soat oldin bepul. "
                       : string.Empty) +
                   $"Tashrifdan so'ng +{YumsEarnedPerBooking} yums.",
            Link = "/profile",
            ReservationId = reservation.Id
        });
        await NotifyRestaurantTeamAsync(restaurant, "new_booking", "Yangi bron",
            $"{profile.FullName} — {req.Date:dd.MM.yyyy} {req.Time:HH:mm}, {req.GuestsCount} kishi" +
            (depositDue > 0 ? $" (depozit {depositDue:N0} so'm)" : string.Empty) +
            (totalDiscount > 0 ? $", chegirma {totalDiscount}%" : string.Empty), reservation.Id, ct);

        if (yumsUsed > 0)
        {
            profile.YumsBalance -= yumsUsed;
            _db.YumsAudits.Add(new YumsAudit
            {
                UserId = userId,
                Amount = -yumsUsed,
                Reason = $"Chegirma ({yumsDiscount}%): {restaurant.Name} — {req.Date:yyyy-MM-dd}"
            });
        }

        await _db.SaveChangesAsync(ct);

        return new ProcessBookingResult
        {
            ReservationId = reservation.Id,
            YumsEarned = YumsEarnedPerBooking,
            YumsAwardedAfterVisit = true,
            YumsUsed = yumsUsed,
            SlotsRemaining = Math.Max(0, restaurant.TotalSlots - bookedCount - 1),
            DepositDue = depositDue,
            DepositReason = depositDue > 0 ? deposit.Reason : null,
            PreOrderTotal = preOrderTotal,
            TotalDiscount = totalDiscount,
            RestaurantDiscount = restaurantDiscount,
            PreOrderDiscount = preOrderDiscount,
            FreeCancellationHours = _options.FreeCancellationHours
        };
    }

    // ───────────────────────────── Bronni o'zgartirish ─────────────────────────────

    public Task<ProcessBookingResult> ModifyBookingAsync(Guid userId, Guid reservationId, ModifyReservationRequest req, CancellationToken ct)
        => InSerializableTransactionAsync(async () =>
        {
            var reservation = await _db.Reservations.Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct)
                ?? throw new NotFoundException("Bron topilmadi.");
            var restaurant = reservation.Restaurant!;

            if (!BookingRules.IsActive(reservation.Status))
                throw new BusinessRuleException("Faqat faol bronni o'zgartirish mumkin.");
            var startsAt = reservation.Date.ToDateTime(reservation.Time);
            if (BusinessTime.Now() > startsAt.AddMinutes(-_options.LateCancellationMinutes))
                throw new BusinessRuleException(
                    $"Bron boshlanishiga {_options.LateCancellationMinutes / 60.0:0.#} soatdan kam qoldi — o'zgartirib bo'lmaydi.");

            var tableId = req.TableId ?? (req.KeepTable ? reservation.TableId : null);
            var bookedCount = await ValidateSlotAsync(restaurant, req.Date, req.Time, req.GuestsCount, tableId, reservation.Id, ct);

            // Depozit: mehmonlar ko'paysa va depozit allaqachon to'langan bo'lsa — qayta bron kerak
            var deposit = await GetDepositRuleAsync(userId, restaurant, ct);
            var newDeposit = deposit.PerGuest * req.GuestsCount;
            if (reservation.DepositPaid && reservation.DepositAmount > 0 && newDeposit > reservation.DepositAmount)
                throw new BusinessRuleException(
                    "Mehmonlar sonini oshirish uchun qo'shimcha depozit kerak. Bronni bekor qilib, qaytadan bron qiling.");
            if (!reservation.DepositPaid)
            {
                reservation.DepositAmount = newDeposit;
                reservation.DepositPaid = newDeposit == 0;
            }

            var before = $"{reservation.Date:dd.MM} {reservation.Time:HH:mm}, {reservation.GuestsCount} kishi";
            reservation.Date = req.Date;
            reservation.Time = req.Time;
            reservation.GuestsCount = req.GuestsCount;
            reservation.TableId = tableId;
            var (restaurantDiscount, totalDiscount) = Discounts(restaurant, req.Time, reservation.YumsUsed > 0 ? YumsDiscountPercent : 0);
            reservation.RestaurantDiscount = restaurantDiscount;
            reservation.TotalDiscount = totalDiscount;
            reservation.PreOrderDiscount = Math.Round(reservation.PreOrderTotal * totalDiscount / 100m, 0);

            var guest = await _db.Profiles.Where(p => p.Id == userId).Select(p => p.FullName).FirstOrDefaultAsync(ct);
            await NotifyRestaurantTeamAsync(restaurant, "booking_modified", "Bron o'zgartirildi",
                $"{guest}: {before} → {req.Date:dd.MM} {req.Time:HH:mm}, {req.GuestsCount} kishi", reservation.Id, ct);

            await _db.SaveChangesAsync(ct);
            return new ProcessBookingResult
            {
                ReservationId = reservation.Id,
                YumsEarned = reservation.YumsEarned,
                YumsAwardedAfterVisit = true,
                YumsUsed = reservation.YumsUsed,
                SlotsRemaining = Math.Max(0, restaurant.TotalSlots - bookedCount - 1),
                DepositDue = reservation.DepositPaid ? 0 : reservation.DepositAmount,
                DepositReason = !reservation.DepositPaid && reservation.DepositAmount > 0 ? deposit.Reason : null,
                PreOrderTotal = reservation.PreOrderTotal,
                TotalDiscount = totalDiscount,
                RestaurantDiscount = restaurantDiscount,
                PreOrderDiscount = reservation.PreOrderDiscount,
                FreeCancellationHours = _options.FreeCancellationHours
            };
        }, ct);

    /// <summary>
    /// Ish vaqti, kunlik/soatlik sig'im va stol bandligini tekshiradi. O'zgartirishda bronning o'zi
    /// hisobga olinmaydi (<paramref name="excludeReservationId"/>). Kunlik band slotlar sonini qaytaradi.
    /// </summary>
    private async Task<int> ValidateSlotAsync(Restaurant restaurant, DateOnly date, TimeOnly time, int guests,
        Guid? tableId, Guid? excludeReservationId, CancellationToken ct)
    {
        if (date.ToDateTime(time) <= BusinessTime.Now())
            throw new BusinessRuleException("O'tgan vaqtga bron qilib bo'lmaydi.");
        if (_options.MaxDaysAhead > 0 && date > BusinessTime.Today().AddDays(_options.MaxDaysAhead))
            throw new BusinessRuleException($"Ko'pi bilan {_options.MaxDaysAhead} kun oldindan bron qilish mumkin.");

        var hours = OpeningHours.Parse(restaurant.OpeningHours, restaurant.SpecialHours);
        if (!hours.CanStartBookingAt(date, time, LastBookingGap))
            throw new BusinessRuleException(
                $"Restoran bu vaqtda bron qabul qilmaydi. {date:dd.MM.yyyy} ish vaqti: {hours.Describe(date)} " +
                $"(oxirgi bron yopilishdan {_options.LastBookingBeforeCloseMinutes} daqiqa oldin).");

        var active = _db.Reservations.Where(r =>
            r.RestaurantId == restaurant.Id && r.Date == date &&
            (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending) &&
            (excludeReservationId == null || r.Id != excludeReservationId));

        if (restaurant.HourlySlots is int hourlySlots)
        {
            var from = new TimeOnly(time.Hour, 0);
            var to = new TimeOnly(time.Hour, 59, 59, 999);
            if (await active.CountAsync(r => r.Time >= from && r.Time <= to, ct) >= hourlySlots)
                throw new ConflictException($"{time.Hour:00}:00–{time.Hour:00}:59 oralig'ida bo'sh joy qolmadi. Boshqa vaqtni tanlang.");
        }

        var bookedCount = await active.CountAsync(ct);
        if (bookedCount >= restaurant.TotalSlots)
            throw new ConflictException("Kechirasiz, bu kunda barcha slotlar band.");

        if (tableId is Guid tid)
            await EnsureTableFreeAsync(restaurant.Id, tid, date, time, guests, excludeReservationId, ct);

        return bookedCount;
    }

    private async Task<RestaurantTable> EnsureTableFreeAsync(Guid restaurantId, Guid tableId, DateOnly date, TimeOnly time,
        int guests, Guid? excludeReservationId, CancellationToken ct)
    {
        var table = await _db.RestaurantTables.FirstOrDefaultAsync(
            t => t.Id == tableId && t.RestaurantId == restaurantId && t.IsActive, ct)
            ?? throw new BusinessRuleException("Stol topilmadi.");
        if (table.Capacity < guests)
            throw new BusinessRuleException($"Stol sig'imi yetarli emas ({table.Capacity} kishilik).");

        // Oldingi/keyingi kun ham olinadi: 23:30 dagi bron ertasi 00:30 bilan to'qnashadi
        var dayBefore = date.AddDays(-1);
        var dayAfter = date.AddDays(1);
        var tableBookings = await _db.Reservations
            .Where(r => r.TableId == tableId && r.Date >= dayBefore && r.Date <= dayAfter &&
                        (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending) &&
                        (excludeReservationId == null || r.Id != excludeReservationId))
            .Select(r => new { r.Date, r.Time })
            .ToListAsync(ct);
        if (tableBookings.Any(r => BookingRules.Overlaps(r.Date, r.Time, date, time)))
            throw new ConflictException($"\"{table.Label}\" stoli tanlangan vaqtda band.");
        return table;
    }

    /// <summary>Restoran chegirmasi (vaqt oralig'ida bo'lsa) + yums; jami cheklangan.</summary>
    private (int Restaurant, int Total) Discounts(Restaurant r, TimeOnly time, int yumsDiscount)
    {
        var rd = RestaurantDiscountAt(r, time);
        return (rd, Math.Min(_options.MaxTotalDiscountPercent, rd + yumsDiscount));
    }

    public static int RestaurantDiscountAt(Restaurant r, TimeOnly time)
    {
        if (r.Discount is not int d || d <= 0) return 0;
        if (r.DiscountTimeStart is TimeOnly from && r.DiscountTimeEnd is TimeOnly to)
        {
            var inside = from <= to ? time >= from && time < to : time >= from || time < to;
            if (!inside) return 0;
        }
        return Math.Clamp(d, 0, 100);
    }

    // ───────────────────────────── Depozit va xulq ─────────────────────────────

    private sealed record DepositRule(decimal PerGuest, string? Reason, int NoShowCount);

    private async Task<DepositRule> GetDepositRuleAsync(Guid userId, Restaurant restaurant, CancellationToken ct)
    {
        var perGuest = restaurant.DepositRequired ? Math.Max(0, restaurant.DepositPerGuest) : 0;
        string? reason = perGuest > 0 ? "restaurant" : null;

        // Kelmaganlar + juda kech bekor qilinganlar
        var since = BusinessTime.Today().AddDays(-_options.NoShowLookbackDays);
        var noShows = await _db.Reservations.CountAsync(r =>
            r.UserId == userId && r.Date >= since &&
            (r.Status == BookingRules.NoShow || (r.Status == BookingRules.Cancelled && r.IsLateCancellation)), ct);

        if (_options.NoShowThreshold > 0 && noShows >= _options.NoShowThreshold && _options.NoShowDepositPerGuest > perGuest)
        {
            perGuest = _options.NoShowDepositPerGuest;
            reason = "no_show";
        }
        return new DepositRule(perGuest, reason, noShows);
    }

    public async Task<BookingQuote> GetQuoteAsync(Guid userId, Guid restaurantId, int guests, CancellationToken ct)
    {
        var restaurant = await _db.Restaurants.FirstOrDefaultAsync(r => r.Id == restaurantId, ct)
            ?? throw new NotFoundException("Restoran topilmadi.");
        var rule = await GetDepositRuleAsync(userId, restaurant, ct);
        var emailConfirmed = await _db.Users.AnyAsync(u => u.Id == userId && u.EmailConfirmed, ct);
        guests = Math.Clamp(guests, 1, 50);
        return new BookingQuote
        {
            DepositPerGuest = rule.PerGuest,
            DepositAmount = rule.PerGuest * guests,
            DepositReason = rule.PerGuest > 0 ? rule.Reason : null,
            NoShowCount = rule.NoShowCount,
            EmailConfirmed = emailConfirmed || !_options.RequireConfirmedEmail,
            FreeCancellationHours = _options.FreeCancellationHours
        };
    }

    // ───────────────────────────── Mavjud vaqtlar va qidiruv ─────────────────────────────

    public async Task<AvailabilityResult> GetAvailabilityAsync(Guid restaurantId, DateOnly date, CancellationToken ct)
    {
        var restaurant = await _db.Restaurants
            .FirstOrDefaultAsync(r => r.Id == restaurantId && r.IsApproved && !r.IsSuspended, ct)
            ?? throw new NotFoundException("Restoran topilmadi yoki faol emas.");

        var hours = OpeningHours.Parse(restaurant.OpeningHours, restaurant.SpecialHours);
        var active = await _db.Reservations
            .Where(r => r.RestaurantId == restaurantId && r.Date == date &&
                        (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending))
            .Select(r => r.Time)
            .ToListAsync(ct);
        var perHour = active.GroupBy(t => t.Hour).ToDictionary(g => g.Key, g => g.Count());
        var dayFull = active.Count >= restaurant.TotalSlots;
        var now = BusinessTime.Now();
        var step = Math.Clamp(_options.SlotStepMinutes, 5, 120);
        var tooFar = _options.MaxDaysAhead > 0 && date > BusinessTime.Today().AddDays(_options.MaxDaysAhead);

        var slots = new List<AvailabilitySlot>();
        for (var minutes = 0; minutes < 24 * 60; minutes += step)
        {
            var time = TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(minutes));
            if (!hours.CanStartBookingAt(date, time, LastBookingGap)) continue;

            string? reason = null;
            if (date.ToDateTime(time) <= now) reason = "past";
            else if (tooFar) reason = "too_far";
            else if (dayFull) reason = "day_full";
            else if (restaurant.HourlySlots is int hs && perHour.GetValueOrDefault(time.Hour) >= hs) reason = "hour_full";

            slots.Add(new AvailabilitySlot
            {
                Time = time.ToString("HH:mm"),
                Available = reason is null,
                Reason = reason,
                Discount = RestaurantDiscountAt(restaurant, time)
            });
        }

        return new AvailabilityResult
        {
            Date = date,
            OpeningHours = hours.Describe(date),
            TotalSlots = restaurant.TotalSlots,
            HourlySlots = restaurant.HourlySlots,
            Booked = active.Count,
            Slots = slots
        };
    }

    /// <summary>"Bugun 19:00 da 4 kishiga joy bor restoranlar": ish vaqti, sig'im va stollar hisobga olinadi.</summary>
    public async Task<HashSet<Guid>> FindAvailableAsync(IReadOnlyCollection<Guid> restaurantIds, DateOnly date, TimeOnly time,
        int guests, CancellationToken ct)
    {
        var restaurants = await _db.Restaurants.Where(r => restaurantIds.Contains(r.Id)).ToListAsync(ct);
        var dayBefore = date.AddDays(-1);
        var dayAfter = date.AddDays(1);
        var reservations = await _db.Reservations
            .Where(r => restaurantIds.Contains(r.RestaurantId) && r.Date >= dayBefore && r.Date <= dayAfter &&
                        (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending))
            .Select(r => new { r.RestaurantId, r.TableId, r.Date, r.Time })
            .ToListAsync(ct);
        var tables = await _db.RestaurantTables.Where(t => restaurantIds.Contains(t.RestaurantId) && t.IsActive)
            .Select(t => new { t.Id, t.RestaurantId, t.Capacity }).ToListAsync(ct);

        var result = new HashSet<Guid>();
        if (date.ToDateTime(time) <= BusinessTime.Now()) return result;
        foreach (var r in restaurants)
        {
            if (!OpeningHours.Parse(r.OpeningHours, r.SpecialHours).CanStartBookingAt(date, time, LastBookingGap)) continue;
            var sameDay = reservations.Where(x => x.RestaurantId == r.Id && x.Date == date).ToList();
            if (sameDay.Count >= r.TotalSlots) continue;
            if (r.HourlySlots is int hs && sameDay.Count(x => x.Time.Hour == time.Hour) >= hs) continue;

            var rTables = tables.Where(t => t.RestaurantId == r.Id && t.Capacity >= guests).ToList();
            if (tables.Any(t => t.RestaurantId == r.Id))
            {
                // Stollar kiritilgan restoran: mos sig'imli kamida bitta bo'sh stol kerak
                var free = rTables.Any(t => !reservations.Any(x =>
                    x.TableId == t.Id && BookingRules.Overlaps(x.Date, x.Time, date, time)));
                if (!free) continue;
            }
            result.Add(r.Id);
        }
        return result;
    }

    // ───────────────────────────── Bekor qilish va statuslar ─────────────────────────────

    public Task<CancelResult> CancelBookingAsync(Guid userId, Guid reservationId, string? reason, CancellationToken ct)
        => InSerializableTransactionAsync(async () =>
        {
            var reservation = await _db.Reservations.Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct)
                ?? throw new NotFoundException("Bron topilmadi.");

            if (reservation.Status == BookingRules.Cancelled)
                return new CancelResult { DepositOutcome = reservation.DepositOutcome };
            if (!BookingRules.IsActive(reservation.Status))
                throw new BusinessRuleException("Faqat faol bronni bekor qilish mumkin.");

            var startsAt = reservation.Date.ToDateTime(reservation.Time);
            var now = BusinessTime.Now();
            if (startsAt <= now)
                throw new BusinessRuleException("Vaqti o'tgan bronni bekor qilib bo'lmaydi.");

            var hoursBefore = (startsAt - now).TotalHours;
            reservation.IsLateCancellation = hoursBefore * 60 < _options.LateCancellationMinutes;
            reservation.CancelledBy = "user";
            reservation.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            var refundable = hoursBefore >= _options.FreeCancellationHours;

            await ApplyStatusAsync(reservation, BookingRules.Cancelled, changedBy: null, ct);
            await ResolveDepositAsync(reservation, refundable, ct);

            var guest = await _db.Profiles.Where(p => p.Id == userId).Select(p => p.FullName).FirstOrDefaultAsync(ct);
            await NotifyRestaurantTeamAsync(reservation.Restaurant!, "booking_cancelled", "Bron bekor qilindi",
                $"{guest} — {reservation.Date:dd.MM.yyyy} {reservation.Time:HH:mm}, {reservation.GuestsCount} kishi" +
                (reservation.CancelReason is null ? "" : $". Sabab: {reservation.CancelReason}"), reservation.Id, ct);

            await _db.SaveChangesAsync(ct);
            return new CancelResult { DepositOutcome = reservation.DepositOutcome, IsLateCancellation = reservation.IsLateCancellation };
        }, ct);

    public async Task<string> ChangeStatusAsync(Guid reservationId, string newStatus, Guid actorId, string? reason, CancellationToken ct)
    {
        if (!BookingRules.IsKnownStatus(newStatus))
            throw new BusinessRuleException("Noto'g'ri status.");

        return await InSerializableTransactionAsync(async () =>
        {
            var reservation = await _db.Reservations.Include(r => r.Restaurant)
                .FirstOrDefaultAsync(r => r.Id == reservationId, ct)
                ?? throw new NotFoundException("Bron topilmadi.");

            var startsAt = reservation.Date.ToDateTime(reservation.Time);
            var now = BusinessTime.Now();
            if (newStatus == BookingRules.NoShow && now < startsAt)
                throw new BusinessRuleException("Bron vaqti hali kelmagan — 'kelmadi' deb belgilab bo'lmaydi.");
            if (newStatus == BookingRules.Completed && now < startsAt.AddHours(-1))
                throw new BusinessRuleException("Bron vaqti hali kelmagan — 'yakunlandi' deb belgilab bo'lmaydi.");

            var wasActive = BookingRules.IsActive(reservation.Status);
            if (newStatus == BookingRules.Cancelled)
            {
                reservation.CancelledBy = "restaurant";
                reservation.CancelReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            }
            await ApplyStatusAsync(reservation, newStatus, actorId, ct);

            // Restoran tomonidan bekor qilinsa — depozit to'liq qaytariladi; kelmasa — qaytarilmaydi
            if (newStatus == BookingRules.Cancelled && wasActive) await ResolveDepositAsync(reservation, refundable: true, ct);
            if (newStatus == BookingRules.NoShow) await ResolveDepositAsync(reservation, refundable: false, ct);

            var place = $"{reservation.Restaurant?.Name} — {reservation.Date:dd.MM.yyyy} {reservation.Time:HH:mm}";
            if (newStatus == BookingRules.Cancelled)
                Notify(reservation.UserId, "booking_cancelled", "Restoran broningizni bekor qildi",
                    place + (reservation.CancelReason is null ? "" : $". Sabab: {reservation.CancelReason}") +
                    DepositNote(reservation), reservation.Id);
            else if (newStatus == BookingRules.NoShow)
                Notify(reservation.UserId, "booking_no_show", "Siz kelmadingiz deb belgilandi",
                    $"{place}. Tez-tez kelmaslik keyingi bronlarda depozit talab qilinishiga olib keladi." + DepositNote(reservation),
                    reservation.Id);

            await _db.SaveChangesAsync(ct);
            return reservation.Status;
        }, ct);
    }

    /// <summary>Restoran xodimi bronga stol biriktiradi yoki olib tashlaydi.</summary>
    public Task<string?> AssignTableAsync(Guid reservationId, Guid? tableId, CancellationToken ct)
        => InSerializableTransactionAsync(async () =>
        {
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, ct)
                ?? throw new NotFoundException("Bron topilmadi.");
            if (!BookingRules.IsActive(reservation.Status))
                throw new BusinessRuleException("Faqat faol bronga stol biriktiriladi.");

            string? label = null;
            if (tableId is Guid tid)
                label = (await EnsureTableFreeAsync(reservation.RestaurantId, tid, reservation.Date, reservation.Time,
                    reservation.GuestsCount, reservation.Id, ct)).Label;
            reservation.TableId = tableId;
            await _db.SaveChangesAsync(ct);
            return label;
        }, ct);

    public Task<int> CompletePastReservationsAsync(CancellationToken ct)
        => InSerializableTransactionAsync(async () =>
        {
            var now = BusinessTime.Now();
            var today = DateOnly.FromDateTime(now);
            var candidates = await _db.Reservations
                .Where(r => r.Status == BookingRules.Confirmed && r.Date <= today)
                .ToListAsync(ct);
            var due = candidates.Where(r => r.Date.ToDateTime(r.Time) + BookingRules.AutoCompleteAfter <= now).ToList();
            foreach (var r in due)
                await ApplyStatusAsync(r, BookingRules.Completed, changedBy: null, ct);
            if (due.Count > 0) await _db.SaveChangesAsync(ct);
            return due.Count;
        }, ct);

    public Task<int> CancelUnpaidDepositsAsync(CancellationToken ct)
    {
        if (_options.DepositHoldMinutes <= 0) return Task.FromResult(0);
        return InSerializableTransactionAsync(async () =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-_options.DepositHoldMinutes);
            var due = await _db.Reservations
                .Where(r => (r.Status == BookingRules.Confirmed || r.Status == BookingRules.Pending) &&
                            r.DepositAmount > 0 && !r.DepositPaid && r.CreatedAt < cutoff &&
                            // to'lov jarayonda bo'lsa (provayder tranzaksiyani yaratgan) — kutamiz
                            !_db.Payments.Any(p => p.ReservationId == r.Id && p.State == Payment.StateCreated))
                .Include(r => r.Restaurant)
                .ToListAsync(ct);
            foreach (var r in due)
            {
                r.CancelledBy = "system";
                r.CancelReason = "Depozit o'z vaqtida to'lanmadi";
                await ApplyStatusAsync(r, BookingRules.Cancelled, changedBy: null, ct);
                Notify(r.UserId, "booking_cancelled", "Bron bekor qilindi",
                    $"{r.Restaurant?.Name} — {r.Date:yyyy-MM-dd} {r.Time:HH:mm}: depozit {_options.DepositHoldMinutes} daqiqa ichida to'lanmadi.", r.Id);
            }
            if (due.Count > 0) await _db.SaveChangesAsync(ct);
            return due.Count;
        }, ct);
    }

    /// <summary>
    /// Status o'zgarishi va unga bog'liq yums harakatlari (taklif bonusi ham). Chaqiruvchi SaveChanges qiladi.
    /// </summary>
    private async Task ApplyStatusAsync(Reservation reservation, string newStatus, Guid? changedBy, CancellationToken ct)
    {
        var oldStatus = reservation.Status;
        if (oldStatus == newStatus) return;
        if (!BookingRules.CanTransition(oldStatus, newStatus))
            throw new BusinessRuleException($"Statusni '{oldStatus}' dan '{newStatus}' ga o'zgartirib bo'lmaydi.");

        var yumsDelta = 0;
        string? reason = null;
        if (newStatus == BookingRules.Cancelled && reservation.YumsUsed > 0)
        {
            yumsDelta = reservation.YumsUsed;
            reason = "Bron bekor qilindi: chegirma yumsi qaytarildi";
        }
        else if (newStatus == BookingRules.Completed)
        {
            yumsDelta = reservation.YumsEarned;
            reason = "Tashrif uchun bonus";
        }
        else if (oldStatus == BookingRules.Completed && newStatus == BookingRules.NoShow)
        {
            yumsDelta = -reservation.YumsEarned;
            reason = "Kelmadi: tashrif bonusi bekor qilindi";
        }

        reservation.Status = newStatus;
        reservation.IsNoShow = newStatus == BookingRules.NoShow;

        if (newStatus == BookingRules.Cancelled && reservation.Date >= BusinessTime.Today())
            await NotifyWaitlistAsync(reservation.RestaurantId, reservation.Date, ct);

        if (newStatus == BookingRules.Completed)
            await RewardReferrerAsync(reservation.UserId, ct);

        if (yumsDelta == 0) return;
        var profile = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == reservation.UserId, ct);
        if (profile is null) return;

        var applied = yumsDelta < 0 ? -Math.Min(profile.YumsBalance, -yumsDelta) : yumsDelta;
        if (applied == 0) return;
        profile.YumsBalance += applied;
        _db.YumsAudits.Add(new YumsAudit
        {
            UserId = reservation.UserId,
            Amount = applied,
            Reason = $"{reason} ({reservation.Date:yyyy-MM-dd} {reservation.Time:HH:mm})",
            ChangedBy = changedBy
        });
        if (newStatus == BookingRules.Completed)
            Notify(reservation.UserId, "yums_earned", $"+{applied} yums",
                "Tashrifingiz uchun rahmat! Bonus hisobingizga qo'shildi.", reservation.Id, "/yums");
    }

    /// <summary>Taklif qilingan do'stning birinchi yakunlangan tashrifi — taklif qilganga bonus (bir marta).</summary>
    private async Task RewardReferrerAsync(Guid userId, CancellationToken ct)
    {
        if (_options.ReferralBonus <= 0) return;
        var friend = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == userId, ct);
        if (friend?.ReferredBy is not Guid referrerId || friend.ReferralRewarded) return;
        var referrer = await _db.Profiles.FirstOrDefaultAsync(p => p.Id == referrerId && !p.IsBanned, ct);
        friend.ReferralRewarded = true;
        if (referrer is null) return;

        referrer.YumsBalance += _options.ReferralBonus;
        _db.YumsAudits.Add(new YumsAudit
        {
            UserId = referrerId,
            Amount = _options.ReferralBonus,
            Reason = $"Do'st taklifi: {friend.FullName} birinchi tashrifini yakunladi"
        });
        Notify(referrerId, "referral_bonus", $"+{_options.ReferralBonus} yums",
            $"Siz taklif qilgan {friend.FullName} birinchi tashrifini yakunladi.", null, "/yums");
    }

    /// <summary>
    /// Bekor qilingan / kelinmagan bronning to'langan depoziti: qaytariladigan bo'lsa — qaytarish so'rovi
    /// (test provayderda darhol qaytariladi), aks holda restoranda qoladi.
    /// </summary>
    private async Task ResolveDepositAsync(Reservation reservation, bool refundable, CancellationToken ct)
    {
        if (!reservation.DepositPaid || reservation.DepositAmount <= 0 || reservation.DepositOutcome is not null) return;

        if (!refundable)
        {
            reservation.DepositOutcome = "forfeited";
            return;
        }

        var payment = await _db.Payments.Where(p => p.ReservationId == reservation.Id && p.State == Payment.StatePaid)
            .OrderByDescending(p => p.PerformedAt).FirstOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        if (payment?.Provider == "test")
        {
            payment.State = Payment.StateRefunded;
            payment.RefundRequestedAt = now;
            payment.CancelledAt = now;
            reservation.DepositOutcome = "refunded";
        }
        else
        {
            // Payme/Click: qaytarish provayder kabinetida qilinadi; naqd to'langan bo'lsa — restoran qaytaradi
            if (payment is not null) payment.RefundRequestedAt = now;
            reservation.DepositOutcome = "refund_requested";
        }
    }

    private static string DepositNote(Reservation r) => r.DepositOutcome switch
    {
        "refunded" => $" Depozit ({r.DepositAmount:N0} so'm) qaytarildi.",
        "refund_requested" => $" Depozit ({r.DepositAmount:N0} so'm) qaytariladi.",
        "forfeited" => $" Depozit ({r.DepositAmount:N0} so'm) qaytarilmaydi.",
        _ => string.Empty
    };

    // ───────────────────────────── Bildirishnomalar ─────────────────────────────

    private void Notify(Guid userId, string type, string title, string body, Guid? reservationId, string link = "/profile")
        => _db.Notifications.Add(new Notification
        {
            UserId = userId, Type = type, Title = title, Body = body, Link = link, ReservationId = reservationId
        });

    /// <summary>Restoran egasi va xodimlariga xabar.</summary>
    private async Task NotifyRestaurantTeamAsync(Restaurant restaurant, string type, string title, string body, Guid? reservationId, CancellationToken ct)
    {
        var ids = await _db.RestaurantStaff.Where(s => s.RestaurantId == restaurant.Id).Select(s => s.UserId).ToListAsync(ct);
        if (restaurant.OwnerId is Guid ownerId) ids.Add(ownerId);
        foreach (var id in ids.Distinct())
            Notify(id, type, title, $"{restaurant.Name}: {body}", reservationId, "/manager");
    }

    /// <summary>Kutish ro'yxatidagi navbatdagi mijozga ilova ichida va email orqali xabar beradi.</summary>
    private async Task NotifyWaitlistAsync(Guid restaurantId, DateOnly date, CancellationToken ct)
    {
        var entry = await _db.Waitlist
            .Where(w => w.RestaurantId == restaurantId && w.Date == date && w.Status == "waiting")
            .OrderBy(w => w.Position).ThenBy(w => w.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (entry is null) return;

        var restaurantName = await _db.Restaurants.Where(r => r.Id == restaurantId).Select(r => r.Name).FirstAsync(ct);
        var contact = await _db.Profiles.Where(p => p.Id == entry.UserId)
            .Select(p => new { p.Email, p.FullName }).FirstOrDefaultAsync(ct);

        entry.Status = "notified";
        Notify(entry.UserId, "waitlist_spot", "Joy bo'shadi!",
            $"{restaurantName} — {date:dd.MM.yyyy}: kutish ro'yxatida birinchisiz. Tezroq bron qiling.", null, $"/restaurant/{restaurantId}");

        if (contact is not null && !string.IsNullOrWhiteSpace(contact.Email))
        {
            var link = $"{_options.FrontendBaseUrl.TrimEnd('/')}/restaurant/{restaurantId}";
            var message = EmailTemplates.WaitlistSpotOpened(contact.Email, contact.FullName, restaurantName, date, link);
            _afterCommit.Add(() => _email.SendAsync(message, CancellationToken.None));
        }
    }

    /// <summary>
    /// Serializable tranzaksiya + EF retry strategiyasi. Har bir urinishdan oldin ChangeTracker
    /// tozalanadi — aks holda serializatsiya xatosidan keyingi qayta urinishda oldingi
    /// urinishning o'zgarishlari (bron, yums) ikki marta yozilardi.
    /// </summary>
    private async Task<T> InSerializableTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            _db.DiscardPendingNotifications();
            _afterCommit.Clear();
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var r = await action();
            await tx.CommitAsync(ct);
            return r;
        });

        _db.FlushNotifications();
        foreach (var job in _afterCommit.ToList())
        {
            try { await job(); }
            catch (Exception ex) { _log.LogWarning(ex, "Commit'dan keyingi ish bajarilmadi (email)"); }
        }
        _afterCommit.Clear();
        return result;
    }
}
