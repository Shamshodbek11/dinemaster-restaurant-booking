using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using DineMaster.Api.Startup;
using DineMaster.Domain.Enums;
using Microsoft.AspNetCore.RateLimiting;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Services;
using DineMaster.Application.Services.Payments;
using DineMaster.Infrastructure.Data;
using DineMaster.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// PDF hisobotlar (QuestPDF). Community litsenziya: yillik daromadi 1 mln USD dan kam kompaniyalar uchun bepul.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
QuestPDF.Settings.ThrowOnMissingTextGlyphs = false;

// ─── Configuration ───────────────────────────────────────────────────────────
// Maxfiy qiymatlar kodda saqlanmaydi: user-secrets (faqat Development) yoki environment variable.
// "dotnet ef migrations add" (design-time) uchun secretlar shart emas — vaqtinchalik qiymat ishlatiladi.
var isDesignTime = EF.IsDesignTime;
var envHint = builder.Environment.IsDevelopment()
    ? "Loyiha papkasida bir marta ishga tushiring: " +
      "powershell -ExecutionPolicy Bypass -File scripts\\setup-dev-secrets.ps1 (README, 2-bo'lim)."
    : $"Muhit: {builder.Environment.EnvironmentName}. User-secrets faqat Development'da o'qiladi — " +
      "'dotnet run --launch-profile http' bilan ishga tushiring yoki environment variable bering " +
      "(ConnectionStrings__DefaultConnection, Jwt__Secret).";

var jwtSecret = builder.Configuration["Jwt:Secret"];
var jwtGeneratedForDev = false;
if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
{
    if (builder.Environment.IsDevelopment() || isDesignTime)
    {
        // Development: kalit kiritilmagan bo'lsa vaqtinchalik tasodifiy kalit (qayta ishga tushganda
        // eski tokenlar bekor bo'ladi — qayta kirish kifoya). Production'da kalit majburiy.
        jwtSecret = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Secret"] = jwtSecret });
        jwtGeneratedForDev = !isDesignTime;
    }
    else
    {
        throw new InvalidOperationException($"Jwt:Secret topilmadi yoki 32 belgidan qisqa. {envHint}");
    }
}
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DineMaster";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DineMasterClient";
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connStr))
{
    if (!isDesignTime)
        throw new InvalidOperationException($"ConnectionStrings:DefaultConnection topilmadi. {envHint}");
    connStr = "Host=localhost;Port=5432;Database=dine_master;Username=postgres";
}

// ─── Database (Infrastructure) ───────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connStr, npgsql =>
    {
        npgsql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(30), null);
        npgsql.CommandTimeout(30);
        npgsql.MigrationsAssembly("DineMaster.Infrastructure");
        // Bir nechta kolleksiyani Include qilganda (menyu + stollar) "cartesian explosion" bo'lmasin
        npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }));

// IAppDbContext → AppDbContext (Application layer uchun)
builder.Services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

builder.Services.AddMemoryCache();

// ─── Application Services ─────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<BookingOptions>(builder.Configuration.GetSection("Booking"));
builder.Services.Configure<PaymentOptions>(builder.Configuration.GetSection("Payments"));
// Test to'lov provayderi: sozlamada ko'rsatilmasa — faqat Development'da yoqiq
builder.Services.PostConfigure<PaymentOptions>(o =>
{
    if (builder.Configuration["Payments:EnableTestProvider"] is null)
        o.EnableTestProvider = builder.Environment.IsDevelopment();
});
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<EmailCodeService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<ReportService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DineMaster.Api.Services.AuditLogger>();
builder.Services.AddScoped<DineMaster.Api.Services.RestaurantAccess>();

// Dastur nomi — bitta joyda (appsettings: Branding:Name)
EmailTemplates.Brand = builder.Configuration["Branding:Name"] is { Length: > 0 } brandName ? brandName : "DineMaster";

// Bildirishnomalar navbati (AppDbContext yozadi) va Telegram bot
builder.Services.AddSingleton<NotificationChannel>();
builder.Services.AddSingleton<INotificationPublisher>(sp => sp.GetRequiredService<NotificationChannel>());
builder.Services.Configure<TelegramOptions>(builder.Configuration.GetSection("Telegram"));
builder.Services.AddHttpClient<TelegramClient>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<TelegramNotificationSender>();

// ─── Infrastructure Services ──────────────────────────────────────────────────
builder.Services.AddScoped<ITokenService, TokenService>();

// Email: SMTP sozlangan bo'lsa — haqiqiy xat; aks holda Development'da konsolga, Production'da 503
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
var emailOptions = builder.Configuration.GetSection("Email").Get<EmailOptions>() ?? new EmailOptions();
if (emailOptions.SmtpConfigured)
    builder.Services.AddSingleton<IEmailService, SmtpEmailService>();
else if (builder.Environment.IsDevelopment())
    builder.Services.AddSingleton<IEmailService, DevelopmentEmailService>();
else
    builder.Services.AddSingleton<IEmailService, NoopEmailService>();

// Background services
builder.Services.AddHostedService<ReminderService>();

// ─── Authentication ───────────────────────────────────────────────────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew = TimeSpan.Zero
        };

        // Har so'rovda foydalanuvchi holati bazadan tekshiriladi: bloklangan foydalanuvchi
        // tokeni darhol ishlamay qoladi, rollar esa tokendagidan emas, bazadagidan olinadi
        // (rol olib qo'yilsa, token muddati tugashini kutish shart emas).
        opt.Events = new JwtBearerEvents
        {
            OnTokenValidated = async ctx =>
            {
                var identity = ctx.Principal?.Identity as ClaimsIdentity;
                if (identity is null || !Guid.TryParse(identity.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
                {
                    ctx.Fail("Token noto'g'ri.");
                    return;
                }

                var db = ctx.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
                var state = await db.Profiles.AsNoTracking()
                    .Where(p => p.Id == userId)
                    .Select(p => new { p.IsBanned, Roles = p.User!.Roles.Select(r => r.Role).ToList() })
                    .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);

                if (state is null || state.IsBanned)
                {
                    ctx.Fail("Foydalanuvchi bloklangan yoki mavjud emas.");
                    return;
                }

                foreach (var claim in identity.FindAll(identity.RoleClaimType).ToList())
                    identity.RemoveClaim(claim);
                var roles = state.Roles.Count > 0 ? state.Roles.Select(r => r.ToRoleString()) : new[] { Roles.User };
                foreach (var role in roles.Distinct())
                    identity.AddClaim(new Claim(identity.RoleClaimType, role));
            }
        };
    });

builder.Services.AddAuthorization();

// ─── Rate Limiting ────────────────────────────────────────────────────────────
// Limitlar sozlamadan o'zgartiriladi (masalan, avtomatik testlarda oshiriladi)
var authPermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPerMinute", 10);
var emailCodePermitLimit = builder.Configuration.GetValue("RateLimiting:EmailCodesPer10Minutes", 5);
builder.Services.AddRateLimiter(opts =>
{
    // Auth endpointlar uchun qattiq limit
    // IP bo'yicha partition: bitta foydalanuvchi boshqalarni bloklay olmaydi
    opts.AddPolicy<string>("auth", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));

    // Email kodi: bitta foydalanuvchi/IP 10 daqiqada ko'pi bilan 5 ta xat (spam bo'lmasin)
    opts.AddPolicy<string>("email-code", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = emailCodePermitLimit,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            }));

    opts.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
});

// ─── Response Compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(opts =>
{
    opts.EnableForHttps = true;
    opts.Providers.Add<BrotliCompressionProvider>();
    opts.Providers.Add<GzipCompressionProvider>();
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
// Faqat ruxsat etilgan frontend manzillari (appsettings: Cors:AllowedOrigins)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
// Development: sayt localhost yoki lokal tarmoq IP manzili (10.x, 192.168.x, 172.16–31.x) orqali
// istalgan portda ochilsa ham ishlaydi. Production'da — faqat ro'yxatdagi domenlar.
var isDevelopment = builder.Environment.IsDevelopment();
builder.Services.AddCors(opts =>
{
    opts.AddDefaultPolicy(policy =>
        policy.SetIsOriginAllowed(origin =>
                allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase) ||
                (isDevelopment && IsLocalOrPrivateNetworkOrigin(origin)))
        .AllowAnyMethod()
        .AllowAnyHeader()
        .AllowCredentials()
    );
});

static bool IsLocalOrPrivateNetworkOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri)) return false;
    if (uri.IsLoopback) return true; // localhost, 127.0.0.1, [::1]
    if (!System.Net.IPAddress.TryParse(uri.Host, out var ip) ||
        ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork) return false;
    var b = ip.GetAddressBytes();
    return b[0] == 10 || (b[0] == 192 && b[1] == 168) || (b[0] == 172 && b[1] >= 16 && b[1] <= 31);
}

// ─── Controllers + JSON ───────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        o.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        o.JsonSerializerOptions.Converters.Add(new DineMaster.Api.Extensions.LenientTimeOnlyConverter());
    });

// ─── Swagger ─────────────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "DineMaster API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "JWT token: Bearer {token}",
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// ─── Build ────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Migrations & Seed ───────────────────────────────────────────────────────
// Ulanish xatosi bo'lsa sababi aniq yoziladi va ilova to'xtaydi (buzilgan DB bilan ishlamaslik uchun)
if (jwtGeneratedForDev)
    app.Logger.LogWarning("Jwt:Secret kiritilmagan — vaqtinchalik kalit yaratildi (faqat Development). " +
                          "Doimiy kalit uchun: scripts\\setup-dev-secrets.ps1");

await DatabaseStartup.MigrateAndSeedAsync(app);

// ─── Middleware Pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<DineMaster.Api.Middleware.ErrorHandlingMiddleware>();
app.UseResponseCompression();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Development'da faqat http://localhost:5000 ishlatiladi (launchSettings.json)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseCors();
app.UseAuthentication();
// Autentifikatsiyadan keyin: "email-code" limiti foydalanuvchi ID bo'yicha ajratiladi
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// WebApplicationFactory<Program> (integratsion testlar) uchun
public partial class Program { }
