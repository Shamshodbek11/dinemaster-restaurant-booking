using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DineMaster.Application.Abstractions;
using DineMaster.Infrastructure.Data;
using DineMaster.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;

namespace DineMaster.Tests.Infrastructure;

/// <summary>
/// Test bazasi: DINEMASTER_TEST_DB (to'liq connection string) yoki Api user-secrets'dagi
/// ConnectionStrings:DefaultConnection — baza nomi har doim "..._test" bo'ladi
/// (asosiy bazaga tegilmaydi, chunki testlar bazani o'chirib qayta yaratadi).
/// </summary>
public static class TestDb
{
    private static readonly Lazy<string?> Conn = new(Resolve);
    public static string? ConnectionString => Conn.Value;

    private static string? Resolve()
    {
        var raw = Environment.GetEnvironmentVariable("DINEMASTER_TEST_DB");
        if (string.IsNullOrWhiteSpace(raw))
        {
            var cfg = new ConfigurationBuilder().AddUserSecrets(typeof(Program).Assembly, optional: true).Build();
            raw = cfg.GetConnectionString("DefaultConnection");
        }
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var csb = new NpgsqlConnectionStringBuilder(raw);
        var db = string.IsNullOrWhiteSpace(csb.Database) ? "dine_master" : csb.Database;
        csb.Database = db.EndsWith("_test", StringComparison.OrdinalIgnoreCase) ? db : db + "_test";

        try
        {
            var probe = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "postgres", Timeout = 3 };
            using var c = new NpgsqlConnection(probe.ConnectionString);
            c.Open();
        }
        catch
        {
            return null;
        }
        return csb.ConnectionString;
    }
}

/// <summary>PostgreSQL topilmasa integratsion test o'tkazib yuboriladi (xato emas).</summary>
public sealed class DbFactAttribute : FactAttribute
{
    public DbFactAttribute()
    {
        if (TestDb.ConnectionString is null)
            Skip = "PostgreSQL topilmadi: DINEMASTER_TEST_DB yoki Api user-secrets'da ConnectionStrings:DefaultConnection kerak.";
    }
}

/// <summary>Email yubormaydi — testlar kodni shu yerdan oladi.</summary>
public sealed class CapturingEmailService : IEmailService
{
    public ConcurrentQueue<EmailMessage> Sent { get; } = new();

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    public string? LastCodeFor(string email)
    {
        var msg = Sent.LastOrDefault(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase) &&
                                          m.TextBody.Contains("Kod:"));
        return msg is null ? null : Regex.Match(msg.TextBody, @"Kod: (\d{6})").Groups[1].Value;
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PaymeKey = "test-payme-key";
    public const string ClickServiceId = "111";
    public const string ClickSecret = "test-click-secret";

    public CapturingEmailService Emails { get; } = new();

    public static string AdminPassword => SeedConst("AdminPassword");
    public static string UserPassword => SeedConst("UserPassword");
    public static string SuperadminPassword => SeedConst("SuperadminPassword");
    public const string UserEmail = "user@dinemaster.uz";
    public const string SuperadminEmail = "super@dinemaster.uz";

    private static string SeedConst(string name) =>
        (string)typeof(DbInitializer).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetRawConstantValue()!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService>(Emails);

            // Fon xizmati testlarga aralashmasin — kerakli joyda qo'lda chaqiriladi
            foreach (var d in services.Where(d => d.ImplementationType == typeof(ReminderService)
                                                  || d.ImplementationType == typeof(TelegramBotService)).ToList())
                services.Remove(d);
            // Haqiqiy Telegram bot (user-secrets'da sozlangan bo'lsa ham) testlarda ishlatilmaydi
            services.PostConfigure<TelegramOptions>(o => { o.BotToken = string.Empty; o.BotUsername = string.Empty; });
        });
    }

    public async Task InitializeAsync()
    {
        // Environment variable'lar user-secrets'dan ustun turadi — ilova faqat test bazasini ko'radi
        var settings = new Dictionary<string, string>
        {
            ["ConnectionStrings__DefaultConnection"] = TestDb.ConnectionString!,
            ["Jwt__Secret"] = new string('k', 48),
            ["RateLimiting__AuthPerMinute"] = "100000",
            ["RateLimiting__EmailCodesPer10Minutes"] = "100000",
            ["Payments__EnableTestProvider"] = "true",
            ["Payments__Payme__MerchantId"] = "test-merchant",
            ["Payments__Payme__Key"] = PaymeKey,
            ["Payments__Click__ServiceId"] = ClickServiceId,
            ["Payments__Click__MerchantId"] = "222",
            ["Payments__Click__SecretKey"] = ClickSecret,
        };
        foreach (var (k, v) in settings) Environment.SetEnvironmentVariable(k, v);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(TestDb.ConnectionString, o => o.MigrationsAssembly("DineMaster.Infrastructure"))
            .Options;
        await using (var ctx = new AppDbContext(options))
            await ctx.Database.EnsureDeletedAsync();

        _ = Server; // host ishga tushadi: migratsiya + seed

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var dbName = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString()).Database;
        if (dbName is null || !dbName.EndsWith("_test"))
            throw new InvalidOperationException($"Xavfsizlik: ilova test bo'lmagan bazaga ulangan ({dbName}).");
    }

    Task IAsyncLifetime.DisposeAsync() => base.DisposeAsync().AsTask();

    public async Task<HttpClient> LoginAsync(string email, string password)
    {
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());
        return client;
    }

    public Task<HttpClient> AdminAsync(int n) => LoginAsync($"admin{n}@dinemaster.uz", AdminPassword);
    public Task<HttpClient> UserAsync() => LoginAsync(UserEmail, UserPassword);
    public Task<HttpClient> SuperadminAsync() => LoginAsync(SuperadminEmail, SuperadminPassword);

    /// <summary>Yangi foydalanuvchi: ro'yxatdan o'tadi va emaildagi kod bilan tasdiqlaydi.</summary>
    public async Task<(HttpClient Client, string Email, Guid Id)> RegisterVerifiedUserAsync(string password = "Parol12345", string? referralCode = null)
    {
        var email = $"t{Guid.NewGuid():N}@test.uz";
        var client = CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/register",
            new { email, password, fullName = "Test Foydalanuvchi", acceptTerms = true, referralCode });
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", json.GetProperty("accessToken").GetString());

        var code = Emails.LastCodeFor(email) ?? throw new InvalidOperationException("Tasdiqlash kodi yuborilmadi.");
        (await client.PostAsJsonAsync("/api/auth/email/verify", new { code })).EnsureSuccessStatusCode();
        return (client, email, Guid.Parse(json.GetProperty("user").GetProperty("id").GetString()!));
    }
}

[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory> { }
