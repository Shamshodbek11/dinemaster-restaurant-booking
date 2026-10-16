using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Services;
using DineMaster.Application.Services.Payments;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using DineMaster.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DineMaster.Tests;

/// <summary>
/// Haqiqiy PostgreSQL (dine_master_test) bilan integratsion testlar.
/// Har bir test boshqa restorandan foydalanadi (admin1..admin10), shuning uchun bir-biriga xalal bermaydi.
/// </summary>
[Collection("api")]
public class ApiTests
{
    private readonly ApiFactory _f;
    public ApiTests(ApiFactory f) { _f = f; }

    private static DateOnly Tomorrow => BusinessTime.Today().AddDays(1);

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient c, string url, object body)
    {
        var r = await c.PostAsJsonAsync(url, body);
        return (r.StatusCode, await Json(r));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Put(HttpClient c, string url, object body)
    {
        var r = await c.PutAsJsonAsync(url, body);
        return (r.StatusCode, await Json(r));
    }

    private static async Task<JsonElement> Get(HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        Assert.True(r.IsSuccessStatusCode, $"{url} → {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        return await Json(r);
    }

    private static async Task<Guid> OwnRestaurantId(HttpClient admin)
        => (await Get(admin, "/api/admin/restaurants"))[0].GetProperty("id").GetGuid();

    private static Task<(HttpStatusCode Status, JsonElement Body)> Book(
        HttpClient c, Guid restaurantId, DateOnly date, string time, int guests = 2, Guid? tableId = null)
        => Post(c, "/api/reservations", new
        {
            restaurantId, date = date.ToString("yyyy-MM-dd"), time, guestsCount = guests, tableId,
            specialRequests = "TEST"
        });

    // ───────────────────────────── Email kod ─────────────────────────────

    [DbFact]
    public async Task Registration_requires_email_code_before_booking()
    {
        var admin = await _f.AdminAsync(1);
        var rid = await OwnRestaurantId(admin);

        var email = $"t{Guid.NewGuid():N}@test.uz";
        var client = _f.CreateClient();
        var (s0, _) = await Post(client, "/api/auth/register", new { email, password = "Parol12345", fullName = "Yangi" });
        Assert.Equal(HttpStatusCode.BadRequest, s0); // shartlarga rozilik majburiy
        var (status, reg) = await Post(client, "/api/auth/register", new { email, password = "Parol12345", fullName = "Yangi", acceptTerms = true });
        Assert.Equal(HttpStatusCode.OK, status);
        Assert.False(reg.GetProperty("user").GetProperty("emailConfirmed").GetBoolean());
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", reg.GetProperty("accessToken").GetString());

        var (s1, _) = await Book(client, rid, Tomorrow, "19:00:00");
        Assert.Equal(HttpStatusCode.Forbidden, s1);

        var code = _f.Emails.LastCodeFor(email);
        Assert.Matches(@"^\d{6}$", code);
        var wrong = code == "000000" ? "111111" : "000000";
        var (s2, _) = await Post(client, "/api/auth/email/verify", new { code = wrong });
        Assert.Equal(HttpStatusCode.BadRequest, s2);
        var (s3, _) = await Post(client, "/api/auth/email/verify", new { code });
        Assert.Equal(HttpStatusCode.OK, s3);

        var me = await Get(client, "/api/auth/me");
        Assert.True(me.GetProperty("emailConfirmed").GetBoolean());
        var (s4, booking) = await Book(client, rid, Tomorrow, "19:00:00");
        Assert.Equal(HttpStatusCode.OK, s4);
        Assert.True(booking.GetProperty("yumsAwardedAfterVisit").GetBoolean());
    }

    [DbFact]
    public async Task Password_reset_with_email_code()
    {
        var (_, email, _) = await _f.RegisterVerifiedUserAsync("EskiParol123");
        var anon = _f.CreateClient();

        var (s1, _) = await Post(anon, "/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, s1);
        // Mavjud bo'lmagan email ham 200 (email mavjudligi oshkor bo'lmaydi)
        var (s2, _) = await Post(anon, "/api/auth/forgot-password", new { email = "yoq@test.uz" });
        Assert.Equal(HttpStatusCode.OK, s2);

        var code = _f.Emails.Sent.Last(m => m.To == email && m.Subject.Contains("tiklash")).TextBody;
        code = System.Text.RegularExpressions.Regex.Match(code, @"Kod: (\d{6})").Groups[1].Value;

        var (s3, _) = await Post(anon, "/api/auth/reset-password", new { email, code = code == "000000" ? "111111" : "000000", newPassword = "YangiParol123" });
        Assert.Equal(HttpStatusCode.BadRequest, s3);
        var (s4, _) = await Post(anon, "/api/auth/reset-password", new { email, code, newPassword = "YangiParol123" });
        Assert.Equal(HttpStatusCode.OK, s4);

        var (s5, _) = await Post(anon, "/api/auth/login", new { email, password = "EskiParol123" });
        Assert.Equal(HttpStatusCode.Unauthorized, s5);
        var (s6, _) = await Post(anon, "/api/auth/login", new { email, password = "YangiParol123" });
        Assert.Equal(HttpStatusCode.OK, s6);
    }

    // ───────────────────────────── Ish vaqti va sig'im ─────────────────────────────

    [DbFact]
    public async Task Opening_hours_limit_booking_times()
    {
        var admin = await _f.AdminAsync(2);
        var rid = await OwnRestaurantId(admin);
        var hours = JsonSerializer.Serialize(new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" }.ToDictionary(d => d, _ => "10:00-22:00"));
        var (sPut, _) = await Put(admin, $"/api/restaurants/{rid}", new { openingHours = hours });
        Assert.Equal(HttpStatusCode.OK, sPut);

        var av = await Get(_f.CreateClient(), $"/api/restaurants/{rid}/availability?date={Tomorrow:yyyy-MM-dd}");
        var times = av.GetProperty("slots").EnumerateArray().Select(s => s.GetProperty("time").GetString()).ToList();
        Assert.Equal("10:00", times.First());
        Assert.Equal("21:00", times.Last()); // 22:00 da yopiladi, oxirgi bron 1 soat oldin

        var user = await _f.UserAsync();
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(user, rid, Tomorrow, "21:30:00")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(user, rid, Tomorrow, "03:00:00")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Book(user, rid, Tomorrow, "21:00:00")).Status);
        Assert.Equal(HttpStatusCode.BadRequest, (await Book(user, rid, BusinessTime.Today(), "00:01:00")).Status);
    }

    [DbFact]
    public async Task Hourly_capacity_is_enforced()
    {
        var admin = await _f.AdminAsync(3);
        var rid = await OwnRestaurantId(admin);
        var (sPut, put) = await Put(admin, $"/api/restaurants/{rid}", new { hourlySlots = 1 });
        Assert.Equal(HttpStatusCode.OK, sPut);
        Assert.Equal(1, put.GetProperty("hourlySlots").GetInt32());

        var user = await _f.UserAsync();
        Assert.Equal(HttpStatusCode.OK, (await Book(user, rid, Tomorrow, "19:00:00")).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Book(user, rid, Tomorrow, "19:30:00")).Status);
        Assert.Equal(HttpStatusCode.OK, (await Book(user, rid, Tomorrow, "20:00:00")).Status);

        var av = await Get(_f.CreateClient(), $"/api/restaurants/{rid}/availability?date={Tomorrow:yyyy-MM-dd}");
        var slot1930 = av.GetProperty("slots").EnumerateArray().First(s => s.GetProperty("time").GetString() == "19:30");
        Assert.False(slot1930.GetProperty("available").GetBoolean());
        Assert.Equal("hour_full", slot1930.GetProperty("reason").GetString());

        var (_, cleared) = await Put(admin, $"/api/restaurants/{rid}", new { hourlySlots = 0 });
        Assert.False(cleared.TryGetProperty("hourlySlots", out var hs) && hs.ValueKind == JsonValueKind.Number);
    }

    [DbFact]
    public async Task Table_overlap_across_midnight_is_rejected()
    {
        var admin = await _f.AdminAsync(9);
        var rid = await OwnRestaurantId(admin);
        var allDay = JsonSerializer.Serialize(new[] { "mon", "tue", "wed", "thu", "fri", "sat", "sun" }.ToDictionary(d => d, _ => "24h"));
        await Put(admin, $"/api/restaurants/{rid}", new { openingHours = allDay });

        var tables = await Get(_f.CreateClient(), $"/api/tables/restaurant/{rid}");
        var tableId = tables.EnumerateArray().OrderByDescending(t => t.GetProperty("capacity").GetInt32()).First().GetProperty("id").GetGuid();

        var user = await _f.UserAsync();
        var (s1, b1) = await Book(user, rid, Tomorrow, "23:30:00", tableId: tableId);
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(HttpStatusCode.Conflict, (await Book(user, rid, Tomorrow.AddDays(1), "00:30:00", tableId: tableId)).Status);

        var cancel = await user.PostAsync($"/api/reservations/{b1.GetProperty("reservationId").GetGuid()}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
    }

    // ───────────────────────────── Kutish ro'yxati ─────────────────────────────

    [DbFact]
    public async Task Cancellation_notifies_first_in_waitlist()
    {
        var admin = await _f.AdminAsync(5);
        var rid = await OwnRestaurantId(admin);
        var (waiter, waiterEmail, _) = await _f.RegisterVerifiedUserAsync();

        var (sJoin, _) = await Post(waiter, "/api/waitlist", new
        {
            restaurantId = rid, date = Tomorrow.ToString("yyyy-MM-dd"), guestsCount = 2, time = "19:00", note = "deraza yonida"
        });
        Assert.Equal(HttpStatusCode.OK, sJoin);

        var user = await _f.UserAsync();
        var (_, booking) = await Book(user, rid, Tomorrow, "19:00:00");
        var cancel = await user.PostAsync($"/api/reservations/{booking.GetProperty("reservationId").GetGuid()}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var mine = await Get(waiter, "/api/waitlist/mine");
        Assert.Equal("notified", mine[0].GetProperty("status").GetString());
        var notes = await Get(waiter, "/api/notifications");
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("type").GetString() == "waitlist_spot");
        Assert.Contains(_f.Emails.Sent, m => m.To == waiterEmail && m.Subject.Contains("joy bo'shadi"));
    }

    // ───────────────────────────── Moderatsiya ─────────────────────────────

    [DbFact]
    public async Task Owner_changes_to_name_wait_for_superadmin()
    {
        var admin = await _f.AdminAsync(4);
        var rid = await OwnRestaurantId(admin);
        var oldName = (await Get(_f.CreateClient(), $"/api/restaurants/{rid}")).GetProperty("name").GetString();

        var (s1, put) = await Put(admin, $"/api/restaurants/{rid}", new { name = "Yangi Nom", totalSlots = 25 });
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(oldName, put.GetProperty("name").GetString());
        Assert.Equal("Yangi Nom", put.GetProperty("pendingChanges").GetProperty("name").GetString());
        Assert.Equal(25, put.GetProperty("totalSlots").GetInt32()); // moderatsiyasiz maydon darhol

        Assert.Equal(oldName, (await Get(_f.CreateClient(), $"/api/restaurants/{rid}")).GetProperty("name").GetString());

        var super = await _f.SuperadminAsync();
        var pending = await Get(super, "/api/superadmin/restaurants/pending-changes");
        Assert.Contains(pending.EnumerateArray(), p => p.GetProperty("id").GetGuid() == rid);
        Assert.Equal(HttpStatusCode.OK, (await super.PostAsync($"/api/superadmin/restaurants/{rid}/changes/approve", null)).StatusCode);
        Assert.Equal("Yangi Nom", (await Get(_f.CreateClient(), $"/api/restaurants/{rid}")).GetProperty("name").GetString());

        await Put(admin, $"/api/restaurants/{rid}", new { name = "Rad Etiladigan" });
        var (s2, _) = await Post(super, $"/api/superadmin/restaurants/{rid}/changes/reject", new { reason = "Nom qoidalarga zid" });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal("Yangi Nom", (await Get(_f.CreateClient(), $"/api/restaurants/{rid}")).GetProperty("name").GetString());
        var notes = await Get(admin, "/api/notifications");
        Assert.Contains(notes.GetProperty("items").EnumerateArray(), n => n.GetProperty("title").GetString() == "O'zgarishlar rad etildi");
    }

    // ───────────────────────────── No-show himoyasi va to'lov ─────────────────────────────

    [DbFact]
    public async Task Frequent_no_show_requires_deposit_and_test_payment_marks_paid()
    {
        var admin = await _f.AdminAsync(6);
        var rid = await OwnRestaurantId(admin);
        // Restoranning o'z depoziti bo'lmasin — depozit faqat kelmaslik tarixi sababli
        await Put(admin, $"/api/restaurants/{rid}", new { depositRequired = false });
        var (client, _, userId) = await _f.RegisterVerifiedUserAsync();

        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            for (var i = 1; i <= 2; i++)
                db.Reservations.Add(new Reservation
                {
                    UserId = userId, RestaurantId = rid, Date = BusinessTime.Today().AddDays(-i * 7),
                    Time = new TimeOnly(19, 0), Status = BookingRules.NoShow, IsNoShow = true, SpecialRequests = "TEST"
                });
            await db.SaveChangesAsync();
        }

        var quote = await Get(client, $"/api/reservations/quote?restaurantId={rid}&guests=2");
        Assert.Equal("no_show", quote.GetProperty("depositReason").GetString());
        Assert.Equal(100_000m, quote.GetProperty("depositAmount").GetDecimal());

        var (s, booking) = await Book(client, rid, Tomorrow, "19:00:00");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal(100_000m, booking.GetProperty("depositDue").GetDecimal());
        var reservationId = booking.GetProperty("reservationId").GetGuid();

        var providers = await Get(_f.CreateClient(), "/api/payments/providers");
        Assert.Contains("test", providers.GetProperty("providers").EnumerateArray().Select(p => p.GetString()));

        var (sPay, pay) = await Post(client, "/api/payments/deposit", new { reservationId, provider = "test" });
        Assert.Equal(HttpStatusCode.OK, sPay);
        Assert.True(pay.GetProperty("paid").GetBoolean());
        Assert.True((await Get(client, $"/api/reservations/{reservationId}")).GetProperty("depositPaid").GetBoolean());
    }

    [DbFact]
    public async Task Payme_merchant_api_full_flow()
    {
        var admin = await _f.AdminAsync(7);
        var rid = await OwnRestaurantId(admin);
        await Put(admin, $"/api/restaurants/{rid}", new { depositRequired = true, depositPerGuest = 30000 });

        var user = await _f.UserAsync();
        var (_, booking) = await Book(user, rid, Tomorrow, "19:00:00");
        var orderId = booking.GetProperty("reservationId").GetString()!;
        const long amount = 60000L * 100; // tiyin

        var payme = _f.CreateClient();
        async Task<JsonElement> Rpc(string method, object @params, string key = ApiFactory.PaymeKey)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, "/api/payments/payme")
            {
                Content = new StringContent(JsonSerializer.Serialize(new { jsonrpc = "2.0", id = 1, method, @params }), Encoding.UTF8, "application/json")
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"Paycom:{key}")));
            var r = await payme.SendAsync(req);
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return await Json(r);
        }
        int ErrorCode(JsonElement e) => e.GetProperty("error").GetProperty("code").GetInt32();
        var account = new Dictionary<string, string> { ["order_id"] = orderId };

        Assert.Equal(-32504, ErrorCode(await Rpc("CheckPerformTransaction", new { amount, account }, key: "wrong")));
        Assert.Equal(-31001, ErrorCode(await Rpc("CheckPerformTransaction", new { amount = amount + 100, account })));
        Assert.True((await Rpc("CheckPerformTransaction", new { amount, account })).GetProperty("result").GetProperty("allow").GetBoolean());

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var created = await Rpc("CreateTransaction", new { id = "payme-tx-1", time = now, amount, account });
        Assert.Equal(1, created.GetProperty("result").GetProperty("state").GetInt32());
        // takroriy so'rov — o'sha tranzaksiya; boshqa tranzaksiya — buyurtma band
        Assert.Equal(1, (await Rpc("CreateTransaction", new { id = "payme-tx-1", time = now, amount, account })).GetProperty("result").GetProperty("state").GetInt32());
        Assert.Equal(-31052, ErrorCode(await Rpc("CreateTransaction", new { id = "payme-tx-2", time = now, amount, account })));

        var performed = await Rpc("PerformTransaction", new { id = "payme-tx-1" });
        Assert.Equal(2, performed.GetProperty("result").GetProperty("state").GetInt32());
        Assert.True((await Get(user, $"/api/reservations/{orderId}")).GetProperty("depositPaid").GetBoolean());

        var check = await Rpc("CheckTransaction", new { id = "payme-tx-1" });
        Assert.True(check.GetProperty("result").GetProperty("perform_time").GetInt64() > 0);
        Assert.Equal(-31003, ErrorCode(await Rpc("CheckTransaction", new { id = "yoq" })));

        var cancelled = await Rpc("CancelTransaction", new { id = "payme-tx-1", reason = 5 });
        Assert.Equal(-2, cancelled.GetProperty("result").GetProperty("state").GetInt32());
        Assert.False((await Get(user, $"/api/reservations/{orderId}")).GetProperty("depositPaid").GetBoolean());

        var statement = await Rpc("GetStatement", new { from = now - 60_000, to = now + 60_000 });
        Assert.Contains(statement.GetProperty("result").GetProperty("transactions").EnumerateArray(),
            t => t.GetProperty("id").GetString() == "payme-tx-1");
    }

    [DbFact]
    public async Task Click_prepare_and_complete()
    {
        var admin = await _f.AdminAsync(8);
        var rid = await OwnRestaurantId(admin);
        await Put(admin, $"/api/restaurants/{rid}", new { depositRequired = true, depositPerGuest = 20000 });

        var user = await _f.UserAsync();
        var (_, booking) = await Book(user, rid, Tomorrow, "19:00:00");
        var orderId = booking.GetProperty("reservationId").GetGuid().ToString("N");
        var click = _f.CreateClient();

        async Task<JsonElement> Send(string url, Dictionary<string, string> form)
        {
            var r = await click.PostAsync(url, new FormUrlEncodedContent(form));
            Assert.Equal(HttpStatusCode.OK, r.StatusCode);
            return await Json(r);
        }
        const string signTime = "2026-10-12 10:00:00";
        Dictionary<string, string> Form(string action, string? prepareId, string amount = "40000.00", string? sign = null)
        {
            var src = "9001" + ApiFactory.ClickServiceId + ApiFactory.ClickSecret + orderId + (prepareId ?? "") + amount + action + signTime;
            var f = new Dictionary<string, string>
            {
                ["click_trans_id"] = "9001", ["service_id"] = ApiFactory.ClickServiceId, ["click_paydoc_id"] = "1",
                ["merchant_trans_id"] = orderId, ["amount"] = amount, ["action"] = action, ["error"] = "0",
                ["error_note"] = "Success", ["sign_time"] = signTime, ["sign_string"] = sign ?? PaymentService.ClickSign(src)
            };
            if (prepareId is not null) f["merchant_prepare_id"] = prepareId;
            return f;
        }

        Assert.Equal(-1, (await Send("/api/payments/click/prepare", Form("0", null, sign: "bad"))).GetProperty("error").GetInt32());
        Assert.Equal(-2, (await Send("/api/payments/click/prepare", Form("0", null, amount: "1000.00"))).GetProperty("error").GetInt32());

        var prepared = await Send("/api/payments/click/prepare", Form("0", null));
        Assert.Equal(0, prepared.GetProperty("error").GetInt32());
        var prepareId = prepared.GetProperty("merchant_prepare_id").GetInt64().ToString();

        var completed = await Send("/api/payments/click/complete", Form("1", prepareId));
        Assert.Equal(0, completed.GetProperty("error").GetInt32());
        Assert.True((await Get(user, $"/api/reservations/{booking.GetProperty("reservationId").GetGuid()}")).GetProperty("depositPaid").GetBoolean());
        Assert.Equal(-4, (await Send("/api/payments/click/complete", Form("1", prepareId))).GetProperty("error").GetInt32());
    }

    [DbFact]
    public async Task Unpaid_deposit_is_cancelled_after_hold_time()
    {
        var admin = await _f.AdminAsync(10);
        var rid = await OwnRestaurantId(admin);
        await Put(admin, $"/api/restaurants/{rid}", new { depositRequired = true, depositPerGuest = 10000 });
        var user = await _f.UserAsync();
        var (_, booking) = await Book(user, rid, Tomorrow, "19:00:00");
        var id = booking.GetProperty("reservationId").GetGuid();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var r = await db.Reservations.FirstAsync(x => x.Id == id);
        r.CreatedAt = DateTime.UtcNow.AddHours(-1);
        await db.SaveChangesAsync();

        var cancelled = await scope.ServiceProvider.GetRequiredService<IBookingService>().CancelUnpaidDepositsAsync(default);
        Assert.True(cancelled >= 1);
        Assert.Equal("cancelled", (await Get(user, $"/api/reservations/{id}")).GetProperty("status").GetString());
    }

    // ───────────────────────────── Yums va tashrif ─────────────────────────────

    [DbFact]
    public async Task Past_booking_auto_completes_and_awards_yums()
    {
        var admin = await _f.AdminAsync(1);
        var rid = await OwnRestaurantId(admin);
        var (client, _, userId) = await _f.RegisterVerifiedUserAsync();

        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Reservations.Add(new Reservation
        {
            UserId = userId, RestaurantId = rid, Date = BusinessTime.Today().AddDays(-1), Time = new TimeOnly(19, 0),
            Status = BookingRules.Confirmed, YumsEarned = 100, DepositPaid = true, SpecialRequests = "TEST"
        });
        await db.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IBookingService>().CompletePastReservationsAsync(default);
        Assert.Equal(100, (await Get(client, "/api/auth/me")).GetProperty("yumsBalance").GetInt32());

        // Tashrifdan keyin sharh yozish mumkin
        var (s, _) = await Post(client, "/api/reviews", new { restaurantId = rid, foodRating = 9, serviceRating = 8, interiorRating = 7, comment = "TEST" });
        Assert.Equal(HttpStatusCode.OK, s);
    }

    // ───────────────────────────── Avvalgi tuzatishlar (regressiya) ─────────────────────────────

    [DbFact]
    public async Task Venue_pages_load_and_same_date_cannot_be_booked_twice()
    {
        var anon = _f.CreateClient();
        var venues = await Get(anon, "/api/venues");
        foreach (var v in venues.EnumerateArray())
            await Get(anon, $"/api/venues/{v.GetProperty("id").GetGuid()}");

        var venue = venues[0];
        var user = await _f.UserAsync();
        var body = new
        {
            eventDate = BusinessTime.Today().AddDays(300).ToString("yyyy-MM-dd"), eventType = "Test",
            guestsCount = venue.GetProperty("capacityMin").GetInt32()
        };
        var url = $"/api/venues/{venue.GetProperty("id").GetGuid()}/reservations";
        Assert.Equal(HttpStatusCode.OK, (await Post(user, url, body)).Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(user, url, body)).Status);
    }

    [DbFact]
    public async Task Validation_and_security_regressions()
    {
        var user = await _f.UserAsync();
        Assert.Equal(HttpStatusCode.OK, (await user.GetAsync("/api/reservations/mine?page=0")).StatusCode);

        var admin = await _f.AdminAsync(2);
        var rid = await OwnRestaurantId(admin);
        Assert.Equal(HttpStatusCode.Forbidden, (await user.GetAsync($"/api/reservations/calendar?restaurantId={rid}&month=2026-10")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync($"/api/reservations/calendar?restaurantId={rid}&month=2026-13")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/tables", new { restaurantId = rid, label = "T", capacity = 0 })).Status);

        // Bloklangan foydalanuvchi tokeni darhol ishlamaydi
        var (victim, _, victimId) = await _f.RegisterVerifiedUserAsync();
        var super = await _f.SuperadminAsync();
        await Post(super, "/api/superadmin/users/ban", new { userId = victimId });
        Assert.Equal(HttpStatusCode.Unauthorized, (await victim.GetAsync("/api/auth/me")).StatusCode);
        await Post(super, "/api/superadmin/users/unban", new { userId = victimId });
        Assert.Equal(HttpStatusCode.OK, (await victim.GetAsync("/api/auth/me")).StatusCode);
    }
}
