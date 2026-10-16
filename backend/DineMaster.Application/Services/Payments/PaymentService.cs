using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using DineMaster.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineMaster.Application.Services.Payments;

public record CheckoutResult(string Provider, string RedirectUrl, bool Paid);

/// <summary>
/// Bron depozitini onlayn to'lash.
///  - Payme: Merchant API (JSON-RPC) — CheckPerformTransaction, CreateTransaction, PerformTransaction,
///    CancelTransaction, CheckTransaction, GetStatement
///  - Click: SHOP API — Prepare (action=0), Complete (action=1), MD5 imzo
///  - test: faqat lokal sinov uchun, darhol "to'landi"
/// </summary>
public class PaymentService
{
    private const long PaymeTimeoutMs = 12 * 60 * 60 * 1000; // 12 soat
    private const string Payme = "payme";
    private const string Click = "click";
    private const string Test = "test";

    private readonly IAppDbContext _db;
    private readonly PaymentOptions _options;
    private readonly ILogger<PaymentService> _log;

    public PaymentService(IAppDbContext db, IOptions<PaymentOptions> options, ILogger<PaymentService> log)
    {
        _db = db;
        _options = options.Value;
        _log = log;
    }

    // ───────────────────────────── Checkout ─────────────────────────────

    public async Task<CheckoutResult> CreateDepositCheckoutAsync(Guid userId, Guid reservationId, string provider, CancellationToken ct)
    {
        provider = (provider ?? string.Empty).Trim().ToLowerInvariant();
        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId && r.UserId == userId, ct)
            ?? throw new NotFoundException("Bron topilmadi.");

        if (!BookingRules.IsActive(reservation.Status))
            throw new BusinessRuleException("Bron faol emas.");
        if (reservation.DepositAmount <= 0 || reservation.DepositPaid)
            throw new BusinessRuleException("Bu bron uchun to'lanadigan depozit yo'q.");

        switch (provider)
        {
            case Payme when _options.Payme.Enabled:
            {
                var tiyin = ToTiyin(reservation.DepositAmount);
                var raw = $"m={_options.Payme.MerchantId};ac.{_options.Payme.AccountField}={reservation.Id};a={tiyin};c={_options.ReturnUrl}";
                var url = $"{_options.Payme.CheckoutBaseUrl}/{Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))}";
                return new CheckoutResult(Payme, url, false);
            }
            case Click when _options.Click.Enabled:
            {
                var url = "https://my.click.uz/services/pay" +
                          $"?service_id={Uri.EscapeDataString(_options.Click.ServiceId)}" +
                          $"&merchant_id={Uri.EscapeDataString(_options.Click.MerchantId)}" +
                          $"&amount={reservation.DepositAmount.ToString("0.00", CultureInfo.InvariantCulture)}" +
                          $"&transaction_param={reservation.Id:N}" +
                          $"&return_url={Uri.EscapeDataString(_options.ReturnUrl)}";
                return new CheckoutResult(Click, url, false);
            }
            case Test when _options.EnableTestProvider:
            {
                _db.Payments.Add(new Payment
                {
                    ReservationId = reservation.Id,
                    UserId = userId,
                    Provider = Test,
                    ProviderTransactionId = $"test-{Guid.NewGuid():N}",
                    Amount = reservation.DepositAmount,
                    State = Payment.StatePaid,
                    PerformedAt = DateTime.UtcNow
                });
                reservation.DepositPaid = true;
                await _db.SaveChangesAsync(ct);
                return new CheckoutResult(Test, _options.ReturnUrl, true);
            }
            default:
                throw new BusinessRuleException("Bu to'lov usuli hozir mavjud emas.");
        }
    }

    // ───────────────────────────── Payme ─────────────────────────────

    /// <summary>Payme Merchant API so'rovini qayta ishlaydi. Javob har doim HTTP 200 bilan qaytariladi.</summary>
    public async Task<Dictionary<string, object?>> HandlePaymeAsync(string? authorization, string body, CancellationToken ct)
    {
        object? requestId = null;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            requestId = root.TryGetProperty("id", out var idEl) ? JsonSerializer.Deserialize<object>(idEl.GetRawText()) : null;

            if (!PaymeAuthorized(authorization))
                return PaymeError(requestId, -32504, "Недостаточно привилегий", "Ruxsat yo'q", "Insufficient privileges");

            var method = root.GetProperty("method").GetString();
            var p = root.TryGetProperty("params", out var pe) ? pe : default;

            return method switch
            {
                "CheckPerformTransaction" => await PaymeCheckPerformAsync(requestId, p, ct),
                "CreateTransaction" => await InTransactionAsync(() => PaymeCreateAsync(requestId, p, ct), ct),
                "PerformTransaction" => await InTransactionAsync(() => PaymePerformAsync(requestId, p, ct), ct),
                "CancelTransaction" => await InTransactionAsync(() => PaymeCancelAsync(requestId, p, ct), ct),
                "CheckTransaction" => await PaymeCheckAsync(requestId, p, ct),
                "GetStatement" => await PaymeStatementAsync(requestId, p, ct),
                _ => PaymeError(requestId, -32601, "Метод не найден", "Metod topilmadi", "Method not found")
            };
        }
        catch (JsonException)
        {
            return PaymeError(requestId, -32700, "Ошибка парсинга JSON", "JSON xato", "Parse error");
        }
        catch (KeyNotFoundException)
        {
            return PaymeError(requestId, -32600, "Неверный запрос", "So'rov noto'g'ri", "Invalid request");
        }
        catch (InvalidOperationException)
        {
            return PaymeError(requestId, -32600, "Неверный запрос", "So'rov noto'g'ri", "Invalid request");
        }
    }

    private bool PaymeAuthorized(string? authorization)
    {
        if (!_options.Payme.Enabled || string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            return false;
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization[6..].Trim()));
            var expected = $"Paycom:{_options.Payme.Key}";
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(decoded), Encoding.UTF8.GetBytes(expected));
        }
        catch (FormatException) { return false; }
    }

    /// <summary>Hisob (bron) va summani tekshiradi. null — hammasi joyida, aks holda Payme xatosi.</summary>
    private async Task<(Reservation? Reservation, Dictionary<string, object?>? Error)> PaymeValidateOrderAsync(
        object? requestId, JsonElement p, bool checkBusy, string? exceptPaymeId, CancellationToken ct)
    {
        var field = _options.Payme.AccountField;
        var amount = p.GetProperty("amount").GetInt64();
        var account = p.GetProperty("account");
        var orderRaw = account.TryGetProperty(field, out var o) ? o.ToString() : null;

        if (!Guid.TryParse(orderRaw, out var reservationId))
            return (null, PaymeError(requestId, -31050, "Заказ не найден", "Buyurtma topilmadi", "Order not found", field));

        var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, ct);
        if (reservation is null)
            return (null, PaymeError(requestId, -31050, "Заказ не найден", "Buyurtma topilmadi", "Order not found", field));
        if (!BookingRules.IsActive(reservation.Status) || reservation.DepositPaid || reservation.DepositAmount <= 0)
            return (null, PaymeError(requestId, -31051, "Заказ недоступен для оплаты", "Buyurtmani to'lab bo'lmaydi", "Order is not payable", field));
        if (amount != ToTiyin(reservation.DepositAmount))
            return (null, PaymeError(requestId, -31001, "Неверная сумма", "Summa noto'g'ri", "Invalid amount"));

        if (checkBusy)
        {
            var busy = await _db.Payments.AnyAsync(x =>
                x.ReservationId == reservation.Id && x.Provider == Payme && x.State == Payment.StateCreated &&
                x.ProviderTransactionId != exceptPaymeId, ct);
            if (busy)
                return (null, PaymeError(requestId, -31052, "Заказ ожидает оплаты", "Buyurtma to'lov kutmoqda", "Order is awaiting payment", field));
        }
        return (reservation, null);
    }

    private async Task<Dictionary<string, object?>> PaymeCheckPerformAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var (_, error) = await PaymeValidateOrderAsync(id, p, checkBusy: true, exceptPaymeId: null, ct);
        return error ?? PaymeResult(id, new Dictionary<string, object?> { ["allow"] = true });
    }

    private async Task<Dictionary<string, object?>> PaymeCreateAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var paymeId = p.GetProperty("id").GetString()!;
        var existing = await _db.Payments.FirstOrDefaultAsync(x => x.Provider == Payme && x.ProviderTransactionId == paymeId, ct);
        if (existing is not null)
        {
            if (existing.State != Payment.StateCreated)
                return PaymeError(id, -31008, "Невозможно выполнить операцию", "Amalni bajarib bo'lmaydi", "Unable to perform operation");
            if (IsPaymeTimedOut(existing))
            {
                Cancel(existing, Payment.StateCancelled, reason: 4);
                await _db.SaveChangesAsync(ct);
                return PaymeError(id, -31008, "Таймаут транзакции", "Tranzaksiya muddati o'tdi", "Transaction timed out");
            }
            return PaymeResult(id, PaymeTxInfo(existing, includeState: true, short_: true));
        }

        var (reservation, error) = await PaymeValidateOrderAsync(id, p, checkBusy: true, exceptPaymeId: paymeId, ct);
        if (error is not null) return error;

        var payment = new Payment
        {
            ReservationId = reservation!.Id,
            UserId = reservation.UserId,
            Provider = Payme,
            ProviderTransactionId = paymeId,
            Amount = reservation.DepositAmount,
            State = Payment.StateCreated
        };
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(ct);
        return PaymeResult(id, PaymeTxInfo(payment, includeState: true, short_: true));
    }

    private async Task<Dictionary<string, object?>> PaymePerformAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var paymeId = p.GetProperty("id").GetString();
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Provider == Payme && x.ProviderTransactionId == paymeId, ct);
        if (payment is null)
            return PaymeError(id, -31003, "Транзакция не найдена", "Tranzaksiya topilmadi", "Transaction not found");

        if (payment.State == Payment.StateCreated)
        {
            if (IsPaymeTimedOut(payment))
            {
                Cancel(payment, Payment.StateCancelled, reason: 4);
                await _db.SaveChangesAsync(ct);
                return PaymeError(id, -31008, "Таймаут транзакции", "Tranzaksiya muddati o'tdi", "Transaction timed out");
            }
            await MarkPaidAsync(payment, ct);
        }
        else if (payment.State != Payment.StatePaid)
        {
            return PaymeError(id, -31008, "Невозможно выполнить операцию", "Amalni bajarib bo'lmaydi", "Unable to perform operation");
        }

        return PaymeResult(id, new Dictionary<string, object?>
        {
            ["transaction"] = payment.Id.ToString(),
            ["perform_time"] = Ms(payment.PerformedAt),
            ["state"] = payment.State
        });
    }

    private async Task<Dictionary<string, object?>> PaymeCancelAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var paymeId = p.GetProperty("id").GetString();
        var reason = p.TryGetProperty("reason", out var re) && re.ValueKind == JsonValueKind.Number ? re.GetInt32() : (int?)null;
        var payment = await _db.Payments.Include(x => x.Reservation)
            .FirstOrDefaultAsync(x => x.Provider == Payme && x.ProviderTransactionId == paymeId, ct);
        if (payment is null)
            return PaymeError(id, -31003, "Транзакция не найдена", "Tranzaksiya topilmadi", "Transaction not found");

        if (payment.State == Payment.StateCreated)
        {
            Cancel(payment, Payment.StateCancelled, reason);
            await _db.SaveChangesAsync(ct);
        }
        else if (payment.State == Payment.StatePaid)
        {
            // Tashrif bo'lib o'tgan bo'lsa (xizmat ko'rsatilgan) — pulni qaytarib bo'lmaydi
            if (payment.Reservation?.Status == BookingRules.Completed)
                return PaymeError(id, -31007, "Невозможно отменить: услуга оказана", "Bekor qilib bo'lmaydi: xizmat ko'rsatilgan", "Unable to cancel: service rendered");
            Cancel(payment, Payment.StateRefunded, reason);
            if (payment.Reservation is Reservation r)
            {
                if (BookingRules.IsActive(r.Status)) r.DepositPaid = false; // bron faol — depozit qayta to'lanishi kerak
                else r.DepositOutcome = "refunded";
                _db.Notifications.Add(new Notification
                {
                    UserId = r.UserId, Type = "deposit_refunded", Title = "Depozit qaytarildi",
                    Body = $"{payment.Amount:N0} so'm Payme orqali qaytarildi.", Link = "/profile", ReservationId = r.Id
                });
            }
            await _db.SaveChangesAsync(ct);
        }

        return PaymeResult(id, new Dictionary<string, object?>
        {
            ["transaction"] = payment.Id.ToString(),
            ["cancel_time"] = Ms(payment.CancelledAt),
            ["state"] = payment.State
        });
    }

    private async Task<Dictionary<string, object?>> PaymeCheckAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var paymeId = p.GetProperty("id").GetString();
        var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Provider == Payme && x.ProviderTransactionId == paymeId, ct);
        if (payment is null)
            return PaymeError(id, -31003, "Транзакция не найдена", "Tranzaksiya topilmadi", "Transaction not found");
        return PaymeResult(id, PaymeTxInfo(payment, includeState: true, short_: false));
    }

    private async Task<Dictionary<string, object?>> PaymeStatementAsync(object? id, JsonElement p, CancellationToken ct)
    {
        var from = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty("from").GetInt64()).UtcDateTime;
        var to = DateTimeOffset.FromUnixTimeMilliseconds(p.GetProperty("to").GetInt64()).UtcDateTime;
        var list = await _db.Payments
            .Where(x => x.Provider == Payme && x.CreatedAt >= from && x.CreatedAt <= to)
            .OrderBy(x => x.CreatedAt)
            .ToListAsync(ct);

        var field = _options.Payme.AccountField;
        var transactions = list.Select(x =>
        {
            var info = PaymeTxInfo(x, includeState: true, short_: false);
            info["id"] = x.ProviderTransactionId;
            info["time"] = Ms(x.CreatedAt);
            info["amount"] = ToTiyin(x.Amount);
            info["account"] = new Dictionary<string, object?> { [field] = x.ReservationId.ToString() };
            info["receivers"] = null;
            return info;
        }).ToList();
        return PaymeResult(id, new Dictionary<string, object?> { ["transactions"] = transactions });
    }

    private static bool IsPaymeTimedOut(Payment payment)
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Ms(payment.CreatedAt) > PaymeTimeoutMs;

    private static Dictionary<string, object?> PaymeTxInfo(Payment x, bool includeState, bool short_)
    {
        var d = new Dictionary<string, object?>
        {
            ["create_time"] = Ms(x.CreatedAt),
            ["transaction"] = x.Id.ToString()
        };
        if (includeState) d["state"] = x.State;
        if (!short_)
        {
            d["perform_time"] = Ms(x.PerformedAt);
            d["cancel_time"] = Ms(x.CancelledAt);
            d["reason"] = x.CancelReason;
        }
        return d;
    }

    private static Dictionary<string, object?> PaymeResult(object? id, object result)
        => new() { ["result"] = result, ["id"] = id };

    private static Dictionary<string, object?> PaymeError(object? id, int code, string ru, string uz, string en, string? data = null)
        => new()
        {
            ["error"] = new Dictionary<string, object?>
            {
                ["code"] = code,
                ["message"] = new Dictionary<string, string> { ["ru"] = ru, ["uz"] = uz, ["en"] = en },
                ["data"] = data
            },
            ["id"] = id
        };

    // ───────────────────────────── Click ─────────────────────────────

    public Task<Dictionary<string, object?>> HandleClickPrepareAsync(IReadOnlyDictionary<string, string> f, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            string Get(string k) => f.TryGetValue(k, out var v) ? v : string.Empty;
            var resp = new Dictionary<string, object?>
            {
                ["click_trans_id"] = Get("click_trans_id"),
                ["merchant_trans_id"] = Get("merchant_trans_id"),
                ["merchant_prepare_id"] = null
            };

            var signSource = Get("click_trans_id") + Get("service_id") + _options.Click.SecretKey +
                             Get("merchant_trans_id") + Get("amount") + Get("action") + Get("sign_time");
            if (!_options.Click.Enabled || Get("service_id") != _options.Click.ServiceId || !ClickSignOk(signSource, Get("sign_string")))
                return ClickError(resp, -1, "SIGN CHECK FAILED!");
            if (Get("action") != "0") return ClickError(resp, -3, "Action not found");

            if (!Guid.TryParse(Get("merchant_trans_id"), out var reservationId))
                return ClickError(resp, -5, "User does not exist");
            var reservation = await _db.Reservations.FirstOrDefaultAsync(r => r.Id == reservationId, ct);
            if (reservation is null) return ClickError(resp, -5, "User does not exist");
            if (reservation.DepositPaid) return ClickError(resp, -4, "Already paid");
            if (!BookingRules.IsActive(reservation.Status) || reservation.DepositAmount <= 0)
                return ClickError(resp, -9, "Transaction cancelled");
            if (!decimal.TryParse(Get("amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
                amount != reservation.DepositAmount)
                return ClickError(resp, -2, "Incorrect parameter amount");

            var clickTransId = Get("click_trans_id");
            var payment = await _db.Payments.FirstOrDefaultAsync(x => x.Provider == Click && x.ProviderTransactionId == clickTransId, ct);
            if (payment is null)
            {
                payment = new Payment
                {
                    ReservationId = reservation.Id,
                    UserId = reservation.UserId,
                    Provider = Click,
                    ProviderTransactionId = clickTransId,
                    Amount = reservation.DepositAmount,
                    State = Payment.StateCreated
                };
                _db.Payments.Add(payment);
                await _db.SaveChangesAsync(ct);
            }

            resp["merchant_prepare_id"] = payment.Number;
            resp["error"] = 0;
            resp["error_note"] = "Success";
            return resp;
        }, ct);

    public Task<Dictionary<string, object?>> HandleClickCompleteAsync(IReadOnlyDictionary<string, string> f, CancellationToken ct)
        => InTransactionAsync(async () =>
        {
            string Get(string k) => f.TryGetValue(k, out var v) ? v : string.Empty;
            var resp = new Dictionary<string, object?>
            {
                ["click_trans_id"] = Get("click_trans_id"),
                ["merchant_trans_id"] = Get("merchant_trans_id"),
                ["merchant_confirm_id"] = null
            };

            var signSource = Get("click_trans_id") + Get("service_id") + _options.Click.SecretKey +
                             Get("merchant_trans_id") + Get("merchant_prepare_id") + Get("amount") +
                             Get("action") + Get("sign_time");
            if (!_options.Click.Enabled || Get("service_id") != _options.Click.ServiceId || !ClickSignOk(signSource, Get("sign_string")))
                return ClickError(resp, -1, "SIGN CHECK FAILED!");
            if (Get("action") != "1") return ClickError(resp, -3, "Action not found");

            if (!long.TryParse(Get("merchant_prepare_id"), out var prepareId))
                return ClickError(resp, -6, "Transaction does not exist");
            var payment = await _db.Payments.Include(x => x.Reservation)
                .FirstOrDefaultAsync(x => x.Provider == Click && x.Number == prepareId, ct);
            if (payment is null || payment.ProviderTransactionId != Get("click_trans_id"))
                return ClickError(resp, -6, "Transaction does not exist");

            if (payment.State == Payment.StatePaid) return ClickError(resp, -4, "Already paid");
            if (payment.State != Payment.StateCreated) return ClickError(resp, -9, "Transaction cancelled");

            // Click tomonida to'lov amalga oshmagan (error < 0) — tranzaksiyani bekor qilamiz
            if (int.TryParse(Get("error"), out var clickError) && clickError < 0)
            {
                Cancel(payment, Payment.StateCancelled, reason: null);
                await _db.SaveChangesAsync(ct);
                return ClickError(resp, -9, "Transaction cancelled");
            }

            if (!decimal.TryParse(Get("amount"), NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ||
                amount != payment.Amount)
                return ClickError(resp, -2, "Incorrect parameter amount");

            await MarkPaidAsync(payment, ct);
            resp["merchant_confirm_id"] = payment.Number;
            resp["error"] = 0;
            resp["error_note"] = "Success";
            return resp;
        }, ct);

    public static string ClickSign(string source)
        => Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();

    private static bool ClickSignOk(string source, string sign)
        => !string.IsNullOrEmpty(sign) &&
           CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(ClickSign(source)), Encoding.ASCII.GetBytes(sign.ToLowerInvariant()));

    private static Dictionary<string, object?> ClickError(Dictionary<string, object?> resp, int code, string note)
    {
        resp["error"] = code;
        resp["error_note"] = note;
        return resp;
    }

    // ───────────────────────────── Umumiy ─────────────────────────────

    private async Task MarkPaidAsync(Payment payment, CancellationToken ct)
    {
        payment.State = Payment.StatePaid;
        payment.PerformedAt = DateTime.UtcNow;
        var reservation = payment.Reservation
            ?? await _db.Reservations.FirstOrDefaultAsync(r => r.Id == payment.ReservationId, ct);
        if (reservation is not null)
        {
            reservation.DepositPaid = true;
            if (!BookingRules.IsActive(reservation.Status))
                _log.LogWarning("Faol bo'lmagan bron uchun depozit to'landi — qaytarish kerak: {ReservationId}", reservation.Id);
            _db.Notifications.Add(new Notification
            {
                UserId = reservation.UserId,
                Type = "deposit_paid",
                Title = "Depozit to'landi",
                Body = $"{payment.Amount:N0} so'm qabul qilindi ({payment.Provider}). Bron tasdiqlangan.",
                Link = "/profile",
                ReservationId = reservation.Id
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static void Cancel(Payment payment, int state, int? reason)
    {
        payment.State = state;
        payment.CancelReason = reason;
        payment.CancelledAt = DateTime.UtcNow;
    }

    private static long ToTiyin(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);

    private static long Ms(DateTime? utc)
        => utc is null ? 0 : new DateTimeOffset(DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    private async Task<T> InTransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        var strategy = _db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            _db.ChangeTracker.Clear();
            _db.DiscardPendingNotifications();
            await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var r = await action();
            await tx.CommitAsync(ct);
            return r;
        });
        _db.FlushNotifications();
        return result;
    }
}
