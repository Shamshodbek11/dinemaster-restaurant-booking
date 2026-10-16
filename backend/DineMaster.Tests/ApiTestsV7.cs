using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DineMaster.Application.Services;
using DineMaster.Tests.Infrastructure;

namespace DineMaster.Tests;

/// <summary>v7: hamkorlik arizasi, bildirishnomalar, faoliyat jurnali, hisobotlar, superadmin boshqaruvi.</summary>
[Collection("api")]
public class ApiTestsV7
{
    private readonly ApiFactory _f;
    public ApiTestsV7(ApiFactory f) { _f = f; }

    private static DateOnly Tomorrow => BusinessTime.Today().AddDays(1);

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

    private static async Task<Guid> OwnRestaurantId(HttpClient admin)
        => (await Get(admin, "/api/admin/restaurants"))[0].GetProperty("id").GetGuid();

    private static IEnumerable<JsonElement> Items(JsonElement page) => page.GetProperty("items").EnumerateArray();

    [DbFact]
    public async Task Partner_application_approval_grants_admin_role()
    {
        var (client, email, userId) = await _f.RegisterVerifiedUserAsync();
        var (s1, _) = await Post(client, "/api/partner-applications",
            new { restaurantName = "Test Osh Markazi", city = "Toshkent", phone = "+998901112233", message = "20 ta stol" });
        Assert.Equal(HttpStatusCode.OK, s1);
        var (s2, _) = await Post(client, "/api/partner-applications",
            new { restaurantName = "Ikkinchi", city = "Toshkent", phone = "+998901112233" });
        Assert.Equal(HttpStatusCode.Conflict, s2); // bitta faol ariza

        var super = await _f.SuperadminAsync();
        var apps = await Get(super, "/api/superadmin/partner-applications?status=pending");
        var app = apps.EnumerateArray().First(a => a.GetProperty("userEmail").GetString() == email);
        Assert.Equal(HttpStatusCode.OK, (await Post(super, $"/api/superadmin/partner-applications/{app.GetProperty("id").GetGuid()}/approve")).Status);

        // Rol bazadan olinadi — eski token bilan ham darhol admin
        var me = await Get(client, "/api/auth/me");
        Assert.Contains("admin", me.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        var notes = await Get(client, "/api/notifications");
        Assert.Contains(Items(notes), n => n.GetProperty("type").GetString() == "partner_approved");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/restaurants")).StatusCode);

        // Faoliyat jurnalida
        var log = await Get(super, "/api/superadmin/audit-log?entityType=partner_application");
        Assert.Contains(Items(log), a => a.GetProperty("summary").GetString()!.Contains("Test Osh Markazi"));
        _ = userId;
    }

    [DbFact]
    public async Task Owner_gets_notifications_for_bookings_and_cancellations()
    {
        var admin = await _f.AdminAsync(3);
        var rid = await OwnRestaurantId(admin);
        var (guest, _, _) = await _f.RegisterVerifiedUserAsync();

        var (s, booking) = await Post(guest, "/api/reservations",
            new { restaurantId = rid, date = Tomorrow.ToString("yyyy-MM-dd"), time = "13:00", guestsCount = 3 });
        Assert.Equal(HttpStatusCode.OK, s);
        await guest.PostAsync($"/api/reservations/{booking.GetProperty("reservationId").GetGuid()}/cancel", null);

        var types = Items(await Get(admin, "/api/notifications?size=100")).Select(n => n.GetProperty("type").GetString()).ToList();
        Assert.Contains("new_booking", types);
        Assert.Contains("booking_cancelled", types);
        var unread = await Get(admin, "/api/notifications/unread-count");
        Assert.True(unread.GetProperty("count").GetInt32() >= 2);
    }

    [DbFact]
    public async Task Small_admin_changes_appear_in_activity_log()
    {
        var admin = await _f.AdminAsync(4);
        var rid = await OwnRestaurantId(admin);

        // Menyu narxi
        var menu = await Get(admin, $"/api/restaurants/{rid}/menu/all");
        var cat = menu[0];
        var item = cat.GetProperty("items")[0];
        var newPrice = item.GetProperty("price").GetDecimal() + 1000;
        var put = await admin.PutAsJsonAsync($"/api/restaurants/{rid}/menu/items/{item.GetProperty("id").GetGuid()}", new
        {
            categoryId = item.GetProperty("categoryId").GetGuid(), name = item.GetProperty("name").GetString(),
            description = item.GetProperty("description").GetString(), price = newPrice, isPopular = false, isAvailable = true
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        // Depozit va stollar soni (moderatsiyasiz maydonlar)
        Assert.Equal(HttpStatusCode.OK, (await admin.PutAsJsonAsync($"/api/restaurants/{rid}", new { depositPerGuest = 15000, totalSlots = 33 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, "/api/tables", new { restaurantId = rid, label = "V7", capacity = 4 })).Status);

        var super = await _f.SuperadminAsync();
        var log = Items(await Get(super, $"/api/superadmin/audit-log?restaurantId={rid}&size=100")).ToList();
        Assert.Contains(log, a => a.GetProperty("entityType").GetString() == "menu_item" && a.GetProperty("changes").GetString()!.Contains("Narx"));
        Assert.Contains(log, a => a.GetProperty("entityType").GetString() == "restaurant" && a.GetProperty("changes").GetString()!.Contains("Kunlik slotlar"));
        Assert.Contains(log, a => a.GetProperty("entityType").GetString() == "table" && a.GetProperty("action").GetString() == "create");
        Assert.Contains(log, a => a.GetProperty("actorRole").GetString() == "admin" && a.GetProperty("actorEmail").GetString() == "admin4@dinemaster.uz");
    }

    [DbFact]
    public async Task Admin_reports_json_and_pdf()
    {
        var admin = await _f.AdminAsync(1);
        var rid = await OwnRestaurantId(admin);
        var user = await _f.UserAsync();
        await Post(user, "/api/reservations", new { restaurantId = rid, date = Tomorrow.ToString("yyyy-MM-dd"), time = "14:00", guestsCount = 2 });

        foreach (var period in new[] { "day", "week", "month", "year" })
        {
            var report = await Get(admin, $"/api/admin/reports?period={period}&date={Tomorrow:yyyy-MM-dd}");
            Assert.True(report.GetProperty("totals").GetProperty("reservations").GetInt32() >= 1, period);
            Assert.NotEmpty(report.GetProperty("breakdown").EnumerateArray());
        }
        var bad = await admin.GetAsync("/api/admin/reports?period=decade");
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var pdf = await admin.GetAsync($"/api/admin/reports/pdf?period=month&date={Tomorrow:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        var bytes = await pdf.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000 && bytes[0] == '%' && bytes[1] == 'P' && bytes[2] == 'D' && bytes[3] == 'F');

        // Boshqa egasining restorani — ruxsat yo'q
        var other = await _f.AdminAsync(2);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync($"/api/admin/reports?restaurantId={rid}&period=day")).StatusCode);
    }

    [DbFact]
    public async Task Superadmin_user_filters_and_search()
    {
        var super = await _f.SuperadminAsync();
        var admins = Items(await Get(super, "/api/superadmin/users?role=admin&size=100")).ToList();
        Assert.NotEmpty(admins);
        Assert.All(admins, u => Assert.Contains("admin", u.GetProperty("roles").EnumerateArray().Select(r => r.GetString())));
        Assert.All(admins, u => Assert.DoesNotContain("superadmin", u.GetProperty("roles").EnumerateArray().Select(r => r.GetString())));

        var users = Items(await Get(super, "/api/superadmin/users?role=user&size=100")).ToList();
        Assert.All(users, u => Assert.Equal(new[] { "user" }, u.GetProperty("roles").EnumerateArray().Select(r => r.GetString())));

        // Admin restoran nomi bo'yicha topiladi
        var admin1 = await _f.AdminAsync(1);
        var restaurantName = (await Get(admin1, "/api/admin/restaurants"))[0].GetProperty("name").GetString()!;
        var found = Items(await Get(super, $"/api/superadmin/users?search={Uri.EscapeDataString(restaurantName[..6])}")).ToList();
        Assert.Contains(found, u => u.GetProperty("email").GetString() == "admin1@dinemaster.uz");

        // Ism bo'yicha
        var byName = Items(await Get(super, "/api/superadmin/users?search=Aziz")).ToList();
        Assert.Contains(byName, u => u.GetProperty("email").GetString() == "admin1@dinemaster.uz");

        // Takroriy (eski) endpointlar olib tashlangan
        var dup = await super.PostAsync($"/api/superadmin/users/{Guid.NewGuid()}/ban", null);
        Assert.Equal(HttpStatusCode.NotFound, dup.StatusCode);
    }

    [DbFact]
    public async Task Superadmin_assigns_and_revokes_admin_role()
    {
        var (client, _, userId) = await _f.RegisterVerifiedUserAsync();
        var super = await _f.SuperadminAsync();

        var (s1, assigned) = await Post(super, "/api/superadmin/users/assign-role", new { userId, role = "admin" });
        Assert.Equal(HttpStatusCode.OK, s1);
        Assert.Contains("admin", assigned.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/restaurants")).StatusCode);

        var (s2, revoked) = await Post(super, "/api/superadmin/users/revoke-role", new { userId, role = "admin" });
        Assert.Equal(HttpStatusCode.OK, s2);
        Assert.DoesNotContain("admin", revoked.GetProperty("roles").EnumerateArray().Select(r => r.GetString()));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/restaurants")).StatusCode);
    }

    [DbFact]
    public async Task Superadmin_manages_reservations_reviews_payments_and_broadcast()
    {
        var super = await _f.SuperadminAsync();
        var res = await Get(super, "/api/superadmin/reservations?size=5");
        Assert.True(res.GetProperty("total").GetInt32() >= 0);
        await Get(super, "/api/superadmin/reviews?size=5");
        var payments = await Get(super, "/api/superadmin/payments?size=5");
        Assert.True(payments.TryGetProperty("totals", out _));
        var stats = await Get(super, "/api/superadmin/stats");
        Assert.True(stats.TryGetProperty("pendingPartnerApplications", out _));

        var (target, _, _) = await _f.RegisterVerifiedUserAsync();
        var (s, sent) = await Post(super, "/api/superadmin/notifications/broadcast",
            new { audience = "users", title = "Yangi chegirmalar", body = "Bu hafta 20%", link = "/offers" });
        Assert.Equal(HttpStatusCode.OK, s);
        Assert.True(sent.GetProperty("sent").GetInt32() >= 1);
        Assert.Contains(Items(await Get(target, "/api/notifications")), n => n.GetProperty("title").GetString() == "Yangi chegirmalar");
    }

    [DbFact]
    public async Task Venue_booking_can_be_confirmed_by_superadmin()
    {
        var anon = _f.CreateClient();
        var venue = (await Get(anon, "/api/venues"))[1];
        var (guest, _, _) = await _f.RegisterVerifiedUserAsync();
        var (s, booking) = await Post(guest, $"/api/venues/{venue.GetProperty("id").GetGuid()}/reservations", new
        {
            eventDate = BusinessTime.Today().AddDays(250).ToString("yyyy-MM-dd"), eventType = "To'y",
            guestsCount = venue.GetProperty("capacityMin").GetInt32()
        });
        Assert.Equal(HttpStatusCode.OK, s);

        var super = await _f.SuperadminAsync();
        var pending = Items(await Get(super, "/api/superadmin/venue-reservations?status=pending&size=200")).ToList();
        Assert.Contains(pending, r => r.GetProperty("id").GetGuid() == booking.GetProperty("id").GetGuid());

        var put = await super.PutAsJsonAsync($"/api/venues/reservations/{booking.GetProperty("id").GetGuid()}/status", new { status = "confirmed" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Contains(Items(await Get(guest, "/api/notifications")), n => n.GetProperty("type").GetString() == "venue_booking");
        var mine = await Get(guest, "/api/venues/reservations/mine");
        Assert.Equal("confirmed", mine[0].GetProperty("status").GetString());
    }
}
