using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DineMaster.Application.Services;
using DineMaster.Domain.Entities;
using DineMaster.Infrastructure.Data;
using DineMaster.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace DineMaster.Tests;

/// <summary>
/// v8: restoran xodimlari, bronni o'zgartirish, bekor qilish siyosati va depozit qaytarish, chegirmalar,
/// do'st taklifi, maxsus kunlar, bo'sh joy bo'yicha qidiruv, stol biriktirish, Telegram, umumiy statistika.
/// </summary>
[Collection("api")]
public class ApiTestsV8
{
    private readonly ApiFactory _f;
    public ApiTestsV8(ApiFactory f) { _f = f; }

    private static DateOnly Day(int offset) => BusinessTime.Today().AddDays(offset);
    private static string D(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        var text = await r.Content.ReadAsStringAsync();
        return string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();
    }

    private static async Task<JsonElement> Get(HttpClient c, string url)
    {
        var r = await c.GetAsync(url);
        Assert.True(r.IsSuccessStatusCode, $"{url} → {(int)r.StatusCode} {await r.Content.ReadAsStringAsync()}");
        return await Json(r);
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Post(HttpClient c, string url, object? body = null)
    {
        var r = await c.PostAsJsonAsync(url, body ?? new { });
        return (r.StatusCode, await Json(r));
    }

    private static async Task<(HttpStatusCode Status, JsonElement Body)> Put(HttpClient c, string url, object body)
    {
        var r = await c.PutAsJsonAsync(url, body);
        return (r.StatusCode, await Json(r));
    }

    private static async Task<Guid> OwnRestaurantId(HttpClient admin)
        => (await Get(admin, "/api/admin/restaurants"))[0].GetProperty("id").GetGuid();

    private static async Task<Guid> Book(HttpClient c, Guid restaurantId, DateOnly date, string time, int guests = 2)
    {
        var (s, b) = await Post(c, "/api/reservations",
            new { restaurantId, date = D(date), time, guestsCount = guests, specialRequests = "TEST" });
        Assert.True(s == HttpStatusCode.OK, $"bron → {(int)s} {b}");
        return b.GetProperty("reservationId").GetGuid();
    }

    private static bool NullOrMissing(JsonElement e, string prop)
        => !e.TryGetProperty(prop, out var v) || v.ValueKind == JsonValueKind.Null;

    private static IEnumerable<string?> NotificationTypes(JsonElement page)
        => page.GetProperty("items").EnumerateArray().Select(n => n.GetProperty("type").GetString());

    /// <summary>Bron vaqtini bazada to'g'ridan-to'g'ri siljitish (masalan "1 soatdan keyin" yoki "kecha").</summary>
    private async Task MoveReservationAsync(Guid id, DateTime startsAt)
    {
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var r = await db.Reservations.FirstAsync(x => x.Id == id);
        r.Date = DateOnly.FromDateTime(startsAt);
        r.Time = TimeOnly.FromDateTime(startsAt);
        await db.SaveChangesAsync();
    }

    // ───────────────────────────── Xodimlar ─────────────────────────────

    [DbFact]
    public async Task Owner_adds_host_who_gets_limited_access_then_removes()
    {
        var owner = await _f.AdminAsync(8);
        var rid = await OwnRestaurantId(owner);
        var (host, hostEmail, _) = await _f.RegisterVerifiedUserAsync();

        // Oddiy foydalanuvchi boshqaruv paneliga kira olmaydi
        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync("/api/admin/restaurants")).StatusCode);

        var (sAdd, staff) = await Post(owner, $"/api/restaurants/{rid}/staff", new { email = hostEmail, role = "host" });
        Assert.Equal(HttpStatusCode.OK, sAdd);
        var (sBad, _) = await Post(owner, $"/api/restaurants/{rid}/staff", new { email = "yoq@test.uz", role = "host" });
        Assert.Equal(HttpStatusCode.NotFound, sBad);

        // Rol bazadan olinadi — eski token bilan ham darhol xodim
        var me = await Get(host, "/api/auth/me");
        Assert.Contains("staff", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        var mine = await Get(host, "/api/admin/restaurants");
        Assert.Single(mine.EnumerateArray());
        Assert.Equal("host", mine[0].GetProperty("accessLevel").GetString());
        Assert.Equal("owner", (await Get(owner, "/api/admin/restaurants"))[0].GetProperty("accessLevel").GetString());

        // Host: bronlarni boshqaradi, lekin hisobot, sozlama va xodimlar — yo'q
        var (guest, _, _) = await _f.RegisterVerifiedUserAsync();
        var id = await Book(guest, rid, Day(2), "19:30:00");
        var (sCancel, _) = await Put(host, $"/api/admin/reservations/{id}/status", new { status = "cancelled", reason = "Zal ta'mirda" });
        Assert.Equal(HttpStatusCode.OK, sCancel);
        var res = await Get(guest, $"/api/reservations/{id}");
        Assert.Equal("restaurant", res.GetProperty("cancelledBy").GetString());
        Assert.Equal("Zal ta'mirda", res.GetProperty("cancelReason").GetString());
        Assert.Contains("booking_cancelled", NotificationTypes(await Get(guest, "/api/notifications")));

        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync($"/api/admin/reports?restaurantId={rid}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(host, $"/api/restaurants/{rid}", new { totalSlots = 5 })).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync($"/api/restaurants/{rid}/staff")).StatusCode);

        // Menejerga ko'tarilsa — hisobot ochiladi
        await Post(owner, $"/api/restaurants/{rid}/staff", new { email = hostEmail, role = "manager" });
        Assert.Equal(HttpStatusCode.OK, (await host.GetAsync($"/api/admin/reports?restaurantId={rid}")).StatusCode);

        // Olib tashlansa — kirish yopiladi
        var del = await owner.DeleteAsync($"/api/restaurants/{rid}/staff/{staff.GetProperty("id").GetGuid()}");
        Assert.Equal(HttpStatusCode.OK, del.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await host.GetAsync("/api/admin/restaurants")).StatusCode);
    }

    // ───────────────────────────── Bronni o'zgartirish ─────────────────────────────

    [DbFact]
    public async Task User_modifies_booking_and_restaurant_is_notified()
    {
        var owner = await _f.AdminAsync(9);
        var rid = await OwnRestaurantId(owner);
        var (guest, _, _) = await _f.RegisterVerifiedUserAsync();
        var id = await Book(guest, rid, Day(3), "19:00:00");

        var (s, body) = await Put(guest, $"/api/reservations/{id}", new { date = D(Day(4)), time = "20:00:00", guestsCount = 3 });
        Assert.True(s == HttpStatusCode.OK, body.ToString());
        var res = await Get(guest, $"/api/reservations/{id}");
        Assert.Equal(D(Day(4)), res.GetProperty("date").GetString());
        Assert.StartsWith("20:00", res.GetProperty("time").GetString());
        Assert.Equal(3, res.GetProperty("guestsCount").GetInt32());
        Assert.Contains("booking_modified", NotificationTypes(await Get(owner, "/api/notifications?size=100")));

        // Yopiq vaqtga o'zgartirib bo'lmaydi
        var (sClosed, _) = await Put(guest, $"/api/reservations/{id}", new { date = D(Day(4)), time = "04:00:00", guestsCount = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, sClosed);

        // Boshqaning broni — topilmaydi
        var (other, _, _) = await _f.RegisterVerifiedUserAsync();
        var (sOther, _) = await Put(other, $"/api/reservations/{id}", new { date = D(Day(4)), time = "18:00:00", guestsCount = 2 });
        Assert.Equal(HttpStatusCode.NotFound, sOther);

        // Boshlanishiga oz qolganda — o'zgartirib bo'lmaydi
        await MoveReservationAsync(id, BusinessTime.Now().AddMinutes(60));
        var (sLate, _) = await Put(guest, $"/api/reservations/{id}", new { date = D(Day(5)), time = "19:00:00", guestsCount = 2 });
        Assert.Equal(HttpStatusCode.BadRequest, sLate);
    }

    // ───────────────────────────── Bekor qilish siyosati ─────────────────────────────

    [DbFact]
    public async Task Early_cancel_refunds_deposit_late_cancel_forfeits_it()
    {
        var owner = await _f.AdminAsync(10);
        var rid = await OwnRestaurantId(owner);
        await Put(owner, $"/api/restaurants/{rid}", new { depositRequired = true, depositPerGuest = 20000 });
        try
        {
            var (guest, _, _) = await _f.RegisterVerifiedUserAsync();

            var quote = await Get(guest, $"/api/reservations/quote?restaurantId={rid}&guests=2");
            Assert.Equal(24, quote.GetProperty("freeCancellationHours").GetInt32());

            // 1) Erta bekor qilish — test provayderda depozit darhol qaytariladi
            var early = await Book(guest, rid, Day(3), "19:00:00");
            Assert.Equal(HttpStatusCode.OK, (await Post(guest, "/api/payments/deposit", new { reservationId = early, provider = "test" })).Status);
            var (s1, c1) = await Post(guest, $"/api/reservations/{early}/cancel", new { reason = "Rejalar o'zgardi" });
            Assert.Equal(HttpStatusCode.OK, s1);
            Assert.Equal("refunded", c1.GetProperty("depositOutcome").GetString());
            Assert.False(c1.GetProperty("isLateCancellation").GetBoolean());
            var r1 = await Get(guest, $"/api/reservations/{early}");
            Assert.Equal("user", r1.GetProperty("cancelledBy").GetString());
            Assert.Equal("Rejalar o'zgardi", r1.GetProperty("cancelReason").GetString());

            // 2) Kech bekor qilish (1 soat qoldi) — depozit restoranda qoladi
            var late = await Book(guest, rid, Day(3), "18:00:00");
            Assert.Equal(HttpStatusCode.OK, (await Post(guest, "/api/payments/deposit", new { reservationId = late, provider = "test" })).Status);
            await MoveReservationAsync(late, BusinessTime.Now().AddMinutes(60));
            var cancel = await guest.PostAsync($"/api/reservations/{late}/cancel", null); // tanasiz ham ishlaydi
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            var c2 = await Json(cancel);
            Assert.Equal("forfeited", c2.GetProperty("depositOutcome").GetString());
            Assert.True(c2.GetProperty("isLateCancellation").GetBoolean());

            // 3) Restoran bekor qilsa — vaqtidan qat'i nazar to'liq qaytariladi
            var byRestaurant = await Book(guest, rid, Day(3), "17:00:00");
            Assert.Equal(HttpStatusCode.OK, (await Post(guest, "/api/payments/deposit", new { reservationId = byRestaurant, provider = "test" })).Status);
            await MoveReservationAsync(byRestaurant, BusinessTime.Now().AddMinutes(30));
            Assert.Equal(HttpStatusCode.OK, (await Put(owner, $"/api/admin/reservations/{byRestaurant}/status", new { status = "cancelled" })).Status);
            Assert.Equal("refunded", (await Get(guest, $"/api/reservations/{byRestaurant}")).GetProperty("depositOutcome").GetString());
        }
        finally
        {
            await Put(owner, $"/api/restaurants/{rid}", new { depositRequired = false });
        }
    }

    [DbFact]
    public async Task Click_refund_request_is_completed_by_superadmin()
    {
        var owner = await _f.AdminAsync(9);
        var rid = await OwnRestaurantId(owner);
        var (guest, _, guestId) = await _f.RegisterVerifiedUserAsync();
        var id = await Book(guest, rid, Day(5), "13:00:00");

        // Click orqali to'langan depozit (provayder chaqiruvini bazada taqlid qilamiz)
        Guid paymentId;
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var r = await db.Reservations.FirstAsync(x => x.Id == id);
            r.DepositAmount = 50000;
            r.DepositPaid = true;
            var p = new Payment
            {
                ReservationId = id, UserId = guestId, Provider = "click", Amount = 50000,
                State = Payment.StatePaid, PerformedAt = DateTime.UtcNow
            };
            db.Payments.Add(p);
            await db.SaveChangesAsync();
            paymentId = p.Id;
        }

        var (s, c) = await Post(guest, $"/api/reservations/{id}/cancel");
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.Equal("refund_requested", c.GetProperty("depositOutcome").GetString());
        Assert.True((await Get(owner, "/api/admin/stats")).GetProperty("refundsPending").GetInt32() >= 1);

        var super = await _f.SuperadminAsync();
        var pending = await Get(super, "/api/superadmin/payments?refundPending=true&size=100");
        Assert.Contains(pending.GetProperty("items").EnumerateArray(), x => x.GetProperty("id").GetGuid() == paymentId);
        Assert.True(pending.GetProperty("totals").GetProperty("refundsPending").GetInt32() >= 1);

        Assert.Equal(HttpStatusCode.OK, (await Post(super, $"/api/superadmin/payments/{paymentId}/refunded")).Status);
        Assert.Equal("refunded", (await Get(guest, $"/api/reservations/{id}")).GetProperty("depositOutcome").GetString());
        Assert.Contains("deposit_refunded", NotificationTypes(await Get(guest, "/api/notifications")));
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(super, $"/api/superadmin/payments/{paymentId}/refunded")).Status);
    }

    // ───────────────────────────── Chegirma ─────────────────────────────

    [DbFact]
    public async Task Restaurant_discount_applies_only_inside_time_window()
    {
        // Seed: Afsona (admin1) — 12:00–16:00 oralig'ida 15%
        var owner = await _f.AdminAsync(1);
        var rid = await OwnRestaurantId(owner);
        var (guest, _, _) = await _f.RegisterVerifiedUserAsync();

        var (s1, inside) = await Post(guest, "/api/reservations",
            new { restaurantId = rid, date = D(Day(6)), time = "13:00:00", guestsCount = 2 });
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Equal(15, inside.GetProperty("restaurantDiscount").GetInt32());
        Assert.Equal(15, inside.GetProperty("totalDiscount").GetInt32());

        var (s2, outside) = await Post(guest, "/api/reservations",
            new { restaurantId = rid, date = D(Day(6)), time = "19:00:00", guestsCount = 2 });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.Equal(0, outside.GetProperty("totalDiscount").GetInt32());

        var res = await Get(guest, $"/api/reservations/{inside.GetProperty("reservationId").GetGuid()}");
        Assert.Equal(15, res.GetProperty("restaurantDiscount").GetInt32());

        var slots = (await Get(guest, $"/api/restaurants/{rid}/availability?date={D(Day(6))}")).GetProperty("slots").EnumerateArray().ToList();
        Assert.Equal(15, slots.First(x => x.GetProperty("time").GetString() == "13:00").GetProperty("discount").GetInt32());
        Assert.Equal(0, slots.First(x => x.GetProperty("time").GetString() == "19:00").GetProperty("discount").GetInt32());
    }

    [DbFact]
    public async Task All_day_discount_and_clearing_it()
    {
        var owner = await _f.AdminAsync(7);
        var rid = await OwnRestaurantId(owner);
        // Vaqtsiz chegirma — kun bo'yi
        var (s1, r1) = await Put(owner, $"/api/restaurants/{rid}", new { discount = 10, discountTimeStart = (string?)null, discountTimeEnd = (string?)null });
        Assert.True(s1 == HttpStatusCode.OK, r1.ToString());
        Assert.True(NullOrMissing(r1, "discountTimeStart")); // API null maydonlarni yubormaydi
        try
        {
            var (guest, _, _) = await _f.RegisterVerifiedUserAsync();
            var (_, b) = await Post(guest, "/api/reservations", new { restaurantId = rid, date = D(Day(6)), time = "20:30:00", guestsCount = 2 });
            Assert.Equal(10, b.GetProperty("totalDiscount").GetInt32());
        }
        finally
        {
            // 0% — chegirma o'chadi
            var (_, r2) = await Put(owner, $"/api/restaurants/{rid}", new { discount = 0 });
            Assert.True(NullOrMissing(r2, "discount"));
        }
    }

    [DbFact]
    public async Task My_reviews_endpoint_returns_only_own_reviews()
    {
        var (user, _, userId) = await _f.RegisterVerifiedUserAsync();
        var rid = await OwnRestaurantId(await _f.AdminAsync(6));
        using (var scope = _f.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Reviews.Add(new Review { UserId = userId, RestaurantId = rid, Rating = 8, FoodRating = 9, ServiceRating = 8, InteriorRating = 7, Comment = "Mazali" });
            await db.SaveChangesAsync();
        }
        var mine = await Get(user, "/api/reviews/mine");
        var item = Assert.Single(mine.EnumerateArray());
        Assert.Equal("Mazali", item.GetProperty("comment").GetString());
        Assert.False(string.IsNullOrEmpty(item.GetProperty("restaurantName").GetString()));
        var (other, _, _) = await _f.RegisterVerifiedUserAsync();
        Assert.Empty((await Get(other, "/api/reviews/mine")).EnumerateArray());
    }

    // ───────────────────────────── Do'st taklifi ─────────────────────────────

    [DbFact]
    public async Task Referrer_gets_bonus_after_friends_first_completed_visit()
    {
        var (referrer, _, _) = await _f.RegisterVerifiedUserAsync();
        var info = await Get(referrer, "/api/profiles/referral");
        var code = info.GetProperty("code").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(code));
        Assert.Equal(0, info.GetProperty("invited").GetInt32());
        var before = (await Get(referrer, "/api/profiles/me")).GetProperty("yumsBalance").GetInt32();

        var (friend, _, _) = await _f.RegisterVerifiedUserAsync(referralCode: code);
        Assert.Equal(1, (await Get(referrer, "/api/profiles/referral")).GetProperty("invited").GetInt32());

        var owner = await _f.AdminAsync(2);
        var rid = await OwnRestaurantId(owner);
        var id = await Book(friend, rid, Day(2), "12:30:00");
        await MoveReservationAsync(id, BusinessTime.Now().AddHours(-3));
        Assert.Equal(HttpStatusCode.OK, (await Put(owner, $"/api/admin/reservations/{id}/status", new { status = "completed" })).Status);

        var after = await Get(referrer, "/api/profiles/referral");
        Assert.Equal(1, after.GetProperty("rewarded").GetInt32());
        Assert.Equal(500, after.GetProperty("earned").GetInt32());
        Assert.Equal(before + 500, (await Get(referrer, "/api/profiles/me")).GetProperty("yumsBalance").GetInt32());
        Assert.Contains("referral_bonus", NotificationTypes(await Get(referrer, "/api/notifications")));

        // Ikkinchi tashrif — bonus qayta berilmaydi
        var id2 = await Book(friend, rid, Day(2), "13:30:00");
        await MoveReservationAsync(id2, BusinessTime.Now().AddHours(-2));
        await Put(owner, $"/api/admin/reservations/{id2}/status", new { status = "completed" });
        Assert.Equal(before + 500, (await Get(referrer, "/api/profiles/me")).GetProperty("yumsBalance").GetInt32());

        // Noto'g'ri kod ro'yxatdan o'tishga xalal bermaydi
        await _f.RegisterVerifiedUserAsync(referralCode: "NOTEXIST");
    }

    // ───────────────────────────── Maxsus kunlar va qidiruv ─────────────────────────────

    [DbFact]
    public async Task Special_days_close_or_shorten_hours_and_search_respects_them()
    {
        var owner = await _f.AdminAsync(5);
        var rid = await OwnRestaurantId(owner);
        var closed = Day(8);
        var shortDay = Day(9);

        var (sBad, _) = await Put(owner, $"/api/restaurants/{rid}", new { specialHours = "{\"bugun\":\"closed\"}" });
        Assert.Equal(HttpStatusCode.BadRequest, sBad);
        var (sBadTime, _) = await Put(owner, $"/api/restaurants/{rid}", new { specialHours = $"{{\"{D(closed)}\":\"ertalab\"}}" });
        Assert.Equal(HttpStatusCode.BadRequest, sBadTime);

        var special = $"{{\"{D(closed)}\":\"closed\",\"{D(shortDay)}\":\"12:00-16:00\"}}";
        var (sOk, body) = await Put(owner, $"/api/restaurants/{rid}", new { specialHours = special });
        Assert.True(sOk == HttpStatusCode.OK, body.ToString());
        try
        {
            var (guest, _, _) = await _f.RegisterVerifiedUserAsync();
            var (s1, _) = await Post(guest, "/api/reservations", new { restaurantId = rid, date = D(closed), time = "19:00:00", guestsCount = 2 });
            Assert.Equal(HttpStatusCode.BadRequest, s1);
            Assert.Empty((await Get(guest, $"/api/restaurants/{rid}/availability?date={D(closed)}")).GetProperty("slots").EnumerateArray());

            var (s2, _) = await Post(guest, "/api/reservations", new { restaurantId = rid, date = D(shortDay), time = "19:00:00", guestsCount = 2 });
            Assert.Equal(HttpStatusCode.BadRequest, s2);
            await Book(guest, rid, shortDay, "13:00:00");

            // "Shu kuni 19:00 da 2 kishiga joy bor" qidiruvi
            var anon = _f.CreateClient();
            var onClosed = await Get(anon, $"/api/restaurants?date={D(closed)}&time=19:00&guests=2&size=100");
            Assert.DoesNotContain(onClosed.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == rid);
            var nextDay = await Get(anon, $"/api/restaurants?date={D(Day(10))}&time=19:00&guests=2&size=100");
            Assert.Contains(nextDay.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == rid);

            // 50 kishilik stol yo'q (stollar kiritilgan restoranlar chiqmaydi)
            var huge = await Get(anon, $"/api/restaurants?date={D(Day(10))}&time=19:00&guests=50&size=100");
            Assert.True(huge.GetProperty("total").GetInt32() < nextDay.GetProperty("total").GetInt32());
        }
        finally
        {
            await Put(owner, $"/api/restaurants/{rid}", new { specialHours = "{}" });
        }
    }

    // ───────────────────────────── Stol biriktirish ─────────────────────────────

    [DbFact]
    public async Task Owner_assigns_table_and_conflicts_are_rejected()
    {
        var owner = await _f.AdminAsync(4);
        var rid = await OwnRestaurantId(owner);
        var tables = await Get(owner, $"/api/tables/restaurant/{rid}");
        var table = tables.EnumerateArray().First(t => t.GetProperty("capacity").GetInt32() >= 2);
        var tableId = table.GetProperty("id").GetGuid();

        var (g1, _, _) = await _f.RegisterVerifiedUserAsync();
        var (g2, _, _) = await _f.RegisterVerifiedUserAsync();
        var a = await Book(g1, rid, Day(7), "20:00:00");
        var b = await Book(g2, rid, Day(7), "20:30:00");

        var (s1, r1) = await Put(owner, $"/api/admin/reservations/{a}/table", new { tableId });
        Assert.True(s1 == HttpStatusCode.OK, r1.ToString());
        Assert.Equal(table.GetProperty("label").GetString(), r1.GetProperty("tableLabel").GetString());
        Assert.Equal(tableId, (await Get(g1, $"/api/reservations/{a}")).GetProperty("tableId").GetGuid());

        var (s2, _) = await Put(owner, $"/api/admin/reservations/{b}/table", new { tableId });
        Assert.Equal(HttpStatusCode.Conflict, s2);

        // Boshqa restoran egasi bu bronga tegolmaydi
        var stranger = await _f.AdminAsync(3);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(stranger, $"/api/admin/reservations/{b}/table", new { tableId })).Status);

        var (s3, _) = await Put(owner, $"/api/admin/reservations/{a}/table", new { tableId = (Guid?)null });
        Assert.Equal(HttpStatusCode.OK, s3);
        Assert.Equal(HttpStatusCode.OK, (await Put(owner, $"/api/admin/reservations/{b}/table", new { tableId })).Status);
    }

    // ───────────────────────────── Telegram, rozilik, statistika ─────────────────────────────

    [DbFact]
    public async Task Telegram_link_needs_configured_bot_and_profile_shows_state()
    {
        var (user, _, _) = await _f.RegisterVerifiedUserAsync();
        var me = await Get(user, "/api/profiles/me");
        Assert.False(me.GetProperty("telegramLinked").GetBoolean());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await Post(user, "/api/profiles/telegram/link")).Status);
        Assert.Equal(HttpStatusCode.OK, (await user.DeleteAsync("/api/profiles/telegram")).StatusCode);
    }

    [DbFact]
    public async Task Registration_stores_terms_consent()
    {
        var (_, email, id) = await _f.RegisterVerifiedUserAsync();
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var profile = await db.Profiles.FirstAsync(p => p.Id == id);
        Assert.NotNull(profile.TermsAcceptedAt);
        Assert.False(string.IsNullOrEmpty(profile.ReferralCode));
        _ = email;
    }

    [DbFact]
    public async Task Public_stats_are_real_counts()
    {
        var stats = await Get(_f.CreateClient(), "/api/public/stats");
        using var scope = _f.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var restaurants = await db.Restaurants.CountAsync(r => r.IsApproved && !r.IsSuspended);
        Assert.Equal(restaurants, stats.GetProperty("restaurants").GetInt32());
        Assert.True(stats.GetProperty("cities").GetInt32() >= 1);
        Assert.True(stats.GetProperty("users").GetInt32() >= 1);
    }
}
