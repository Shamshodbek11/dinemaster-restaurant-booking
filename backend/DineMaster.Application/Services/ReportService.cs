using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace DineMaster.Application.Services;

public class ReportTotals
{
    public int Reservations { get; set; }
    public int Active { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int NoShow { get; set; }
    public int Guests { get; set; }
    public decimal AvgGuests { get; set; }
    /// <summary>Bekor qilinganlar ulushi, %.</summary>
    public decimal CancelRate { get; set; }
    /// <summary>Kelmaganlar ulushi (yakunlangan + kelmagan ichida), %.</summary>
    public decimal NoShowRate { get; set; }
    public decimal DepositsPaid { get; set; }
    public decimal DepositsUnpaid { get; set; }
    public decimal PreOrderTotal { get; set; }
    public int YumsDiscounts { get; set; }
    public int UniqueGuests { get; set; }
    public int ReturningGuests { get; set; }
    public int Reviews { get; set; }
    public decimal AvgRating { get; set; }
    public int WaitlistEntries { get; set; }
}

public class ReportRow
{
    public string Label { get; set; } = string.Empty;
    public int Reservations { get; set; }
    public int Guests { get; set; }
    public int Completed { get; set; }
    public int Cancelled { get; set; }
    public int NoShow { get; set; }
}

public class ReportMenuItem
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}

public class RestaurantReport
{
    public Guid RestaurantId { get; set; }
    public string RestaurantName { get; set; } = string.Empty;
    /// <summary>day | week | month | year</summary>
    public string Period { get; set; } = "day";
    public string Label { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public DateTime GeneratedAt { get; set; }
    public ReportTotals Totals { get; set; } = new();
    /// <summary>Kunlik — soatlar, haftalik/oylik — kunlar, yillik — oylar bo'yicha.</summary>
    public List<ReportRow> Breakdown { get; set; } = new();
    public List<ReportRow> TopHours { get; set; } = new();
    public List<ReportMenuItem> TopMenuItems { get; set; } = new();
}

/// <summary>Restoran egasi uchun davr bo'yicha hisobot (ekranda va PDF'da bir xil ma'lumot).</summary>
public class ReportService
{
    private static readonly string[] MonthNames =
        { "Yanvar", "Fevral", "Mart", "Aprel", "May", "Iyun", "Iyul", "Avgust", "Sentabr", "Oktabr", "Noyabr", "Dekabr" };

    private static readonly string[] DayNames = { "Ya", "Du", "Se", "Ch", "Pa", "Ju", "Sh" };

    private readonly IAppDbContext _db;
    public ReportService(IAppDbContext db) { _db = db; }

    public static (DateOnly From, DateOnly To, string Label) ResolveRange(string period, DateOnly anchor) => period switch
    {
        "day" => (anchor, anchor, anchor.ToString("dd.MM.yyyy")),
        "week" => WeekOf(anchor),
        "month" => (new DateOnly(anchor.Year, anchor.Month, 1),
                    new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month)),
                    $"{MonthNames[anchor.Month - 1]} {anchor.Year}"),
        "year" => (new DateOnly(anchor.Year, 1, 1), new DateOnly(anchor.Year, 12, 31), $"{anchor.Year}-yil"),
        _ => throw new BusinessRuleException("Davr noto'g'ri: day, week, month yoki year bo'lishi kerak.")
    };

    private static (DateOnly, DateOnly, string) WeekOf(DateOnly d)
    {
        var from = d.AddDays(-(((int)d.DayOfWeek + 6) % 7)); // dushanbadan
        var to = from.AddDays(6);
        return (from, to, $"{from:dd.MM} – {to:dd.MM.yyyy}");
    }

    public async Task<RestaurantReport> BuildAsync(Guid restaurantId, string period, DateOnly anchor, CancellationToken ct)
    {
        var restaurant = await _db.Restaurants.Where(r => r.Id == restaurantId)
            .Select(r => new { r.Id, r.Name }).FirstOrDefaultAsync(ct)
            ?? throw new NotFoundException("Restoran topilmadi.");
        var (from, to, label) = ResolveRange(period, anchor);

        var rows = await _db.Reservations
            .Where(r => r.RestaurantId == restaurantId && r.Date >= from && r.Date <= to)
            .Select(r => new
            {
                r.Id, r.UserId, r.Date, r.Time, r.Status, r.GuestsCount,
                r.DepositAmount, r.DepositPaid, r.DepositOutcome, r.PreOrderTotal, r.YumsUsed
            })
            .ToListAsync(ct);

        bool IsCancelled(string s) => s == BookingRules.Cancelled;
        var notCancelled = rows.Where(r => !IsCancelled(r.Status)).ToList();
        var completed = rows.Count(r => r.Status == BookingRules.Completed);
        var noShow = rows.Count(r => r.Status == BookingRules.NoShow);

        // Qaytib kelgan mijozlar: shu davrdan oldin ham bu restoranga kelgan (yakunlangan bron)
        var guestIds = notCancelled.Select(r => r.UserId).Distinct().ToList();
        var returning = await _db.Reservations
            .Where(r => r.RestaurantId == restaurantId && r.Date < from && r.Status == BookingRules.Completed &&
                        guestIds.Contains(r.UserId))
            .Select(r => r.UserId).Distinct().CountAsync(ct);

        var utcFrom = BusinessTime.ToUtc(from, TimeOnly.MinValue);
        var utcTo = BusinessTime.ToUtc(to.AddDays(1), TimeOnly.MinValue);
        var reviews = await _db.Reviews
            .Where(r => r.RestaurantId == restaurantId && r.CreatedAt >= utcFrom && r.CreatedAt < utcTo)
            .Select(r => (r.FoodRating + r.ServiceRating + r.InteriorRating) / 3.0m)
            .ToListAsync(ct);
        var waitlist = await _db.Waitlist.CountAsync(w => w.RestaurantId == restaurantId && w.Date >= from && w.Date <= to, ct);

        var reservationIds = notCancelled.Select(r => r.Id).ToList();
        var topItems = await _db.ReservationItems
            .Where(i => reservationIds.Contains(i.ReservationId))
            .GroupBy(i => i.MenuItem!.Name)
            .Select(g => new ReportMenuItem
            {
                Name = g.Key,
                Quantity = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.Quantity * i.UnitPrice)
            })
            .OrderByDescending(x => x.Quantity).Take(10)
            .ToListAsync(ct);

        var report = new RestaurantReport
        {
            RestaurantId = restaurant.Id,
            RestaurantName = restaurant.Name,
            Period = period,
            Label = label,
            From = from,
            To = to,
            GeneratedAt = BusinessTime.Now(),
            Totals = new ReportTotals
            {
                Reservations = rows.Count,
                Active = rows.Count(r => BookingRules.IsActive(r.Status)),
                Completed = completed,
                Cancelled = rows.Count(r => IsCancelled(r.Status)),
                NoShow = noShow,
                Guests = notCancelled.Sum(r => r.GuestsCount),
                AvgGuests = notCancelled.Count == 0 ? 0 : Math.Round((decimal)notCancelled.Average(r => r.GuestsCount), 1),
                CancelRate = Percent(rows.Count(r => IsCancelled(r.Status)), rows.Count),
                NoShowRate = Percent(noShow, completed + noShow),
                DepositsPaid = rows.Where(r => r.DepositPaid && r.DepositAmount > 0 && r.DepositOutcome != "refunded").Sum(r => r.DepositAmount),
                DepositsUnpaid = rows.Where(r => BookingRules.IsActive(r.Status) && !r.DepositPaid && r.DepositAmount > 0).Sum(r => r.DepositAmount),
                PreOrderTotal = notCancelled.Sum(r => r.PreOrderTotal),
                YumsDiscounts = notCancelled.Count(r => r.YumsUsed > 0),
                UniqueGuests = guestIds.Count,
                ReturningGuests = returning,
                Reviews = reviews.Count,
                AvgRating = reviews.Count == 0 ? 0 : Math.Round(reviews.Average(), 1),
                WaitlistEntries = waitlist
            },
            TopMenuItems = topItems,
            TopHours = notCancelled.GroupBy(r => r.Time.Hour)
                .Select(g => new ReportRow { Label = $"{g.Key:00}:00", Reservations = g.Count(), Guests = g.Sum(r => r.GuestsCount) })
                .OrderByDescending(r => r.Reservations).Take(5).ToList()
        };

        // Taqsimot: kunlik — soatlar, haftalik/oylik — kunlar, yillik — oylar
        IEnumerable<(string Label, Func<DateOnly, TimeOnly, bool> Match)> buckets = period switch
        {
            "day" => Enumerable.Range(0, 24)
                .Where(h => rows.Any(r => r.Time.Hour == h))
                .Select(h => ($"{h:00}:00", (Func<DateOnly, TimeOnly, bool>)((_, t) => t.Hour == h))),
            "year" => Enumerable.Range(1, 12)
                .Select(m => (MonthNames[m - 1], (Func<DateOnly, TimeOnly, bool>)((d, _) => d.Month == m))),
            _ => Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(i => from.AddDays(i))
                .Select(day => ($"{day:dd.MM} {DayNames[(int)day.DayOfWeek]}",
                                (Func<DateOnly, TimeOnly, bool>)((d, _) => d == day)))
        };
        foreach (var (bucketLabel, match) in buckets)
        {
            var b = rows.Where(r => match(r.Date, r.Time)).ToList();
            report.Breakdown.Add(new ReportRow
            {
                Label = bucketLabel,
                Reservations = b.Count,
                Guests = b.Where(r => !IsCancelled(r.Status)).Sum(r => r.GuestsCount),
                Completed = b.Count(r => r.Status == BookingRules.Completed),
                Cancelled = b.Count(r => IsCancelled(r.Status)),
                NoShow = b.Count(r => r.Status == BookingRules.NoShow)
            });
        }

        return report;
    }

    private static decimal Percent(int part, int total) => total == 0 ? 0 : Math.Round(part * 100m / total, 1);
}
