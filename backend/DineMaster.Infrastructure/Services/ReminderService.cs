using DineMaster.Application.Abstractions;
using DineMaster.Application.Services;
using DineMaster.Application.Services.Payments;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineMaster.Infrastructure.Services;

public class ReminderService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly ILogger<ReminderService> _log;
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    public ReminderService(IServiceProvider sp, ILogger<ReminderService> log)
    {
        _sp = sp; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); } catch { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await RunOnceAsync(db, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "ReminderService xatosi");
            }

            // Vaqti o'tgan tasdiqlangan bronlar → "completed" + tashrif bonusi (alohida scope/kontekst)
            try
            {
                using var scope = _sp.CreateScope();
                var booking = scope.ServiceProvider.GetRequiredService<IBookingService>();
                var completed = await booking.CompletePastReservationsAsync(stoppingToken);
                if (completed > 0) _log.LogInformation("{Count} ta bron avtomatik yakunlandi.", completed);

                // Depozit onlayn to'lanmagan bronlar — faqat onlayn to'lov yoqilgan bo'lsa
                // (aks holda depozit restoranda to'lanadi va admin qo'lda belgilaydi)
                var payments = scope.ServiceProvider.GetRequiredService<IOptions<PaymentOptions>>().Value;
                if (payments.AnyEnabled)
                {
                    var cancelled = await booking.CancelUnpaidDepositsAsync(stoppingToken);
                    if (cancelled > 0) _log.LogInformation("{Count} ta bron depozit to'lanmagani uchun bekor qilindi.", cancelled);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogError(ex, "Bronlarni avtomatik yakunlashda xato");
            }

            try { await Task.Delay(Interval, stoppingToken); } catch { break; }
        }
    }

    internal static async Task RunOnceAsync(AppDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = BusinessTime.Today();
        var tomorrow = today.AddDays(1);

        var upcoming = await db.Reservations
            .Where(r => (r.Status == "confirmed" || r.Status == "pending")
                        && r.Date >= today && r.Date <= tomorrow)
            .Include(r => r.Restaurant)
            .ToListAsync(ct);

        foreach (var r in upcoming)
        {
            if (r.Restaurant is null) continue;
            // Bron vaqti biznes zonasida (Asia/Tashkent) saqlanadi — UTC ga aylantiramiz
            var when = BusinessTime.ToUtc(r.Date, r.Time);
            var delta = when - now;
            if (delta.TotalMinutes < 0) continue;

            // Oynalar sikl intervalidan (10 daqiqa) ancha keng — kechikish bo'lsa ham eslatma tushib qolmaydi.
            // Takrorlanish MaybeCreate ichida tekshiriladi.
            if (delta <= TimeSpan.FromHours(24) && delta > TimeSpan.FromHours(20))
                await MaybeCreate(db, r, "24h", $"Ertaga bron: {r.Restaurant.Name} — {r.Time:HH:mm}", ct);

            if (delta <= TimeSpan.FromHours(2) && delta > TimeSpan.FromMinutes(30))
                await MaybeCreate(db, r, "2h", $"Tez orada bron: {r.Restaurant.Name} — {r.Time:HH:mm}", ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private static async Task MaybeCreate(AppDbContext db, Reservation r, string tag, string title, CancellationToken ct)
    {
        var already = await db.Notifications.AnyAsync(n =>
            n.ReservationId == r.Id && n.Type == "booking_reminder" && n.Title.StartsWith("[" + tag + "]"), ct);
        if (already) return;

        db.Notifications.Add(new Notification
        {
            UserId = r.UserId,
            Type = "booking_reminder",
            Title = $"[{tag}] {title}",
            Body = $"{r.Restaurant!.Name} • {r.Date:yyyy-MM-dd} {r.Time:HH:mm} • {r.GuestsCount} kishi",
            Link = "/profile",
            ReservationId = r.Id
        });
    }
}
