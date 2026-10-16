using System.Net.Sockets;
using DineMaster.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DineMaster.Api.Startup;

/// <summary>
/// Ishga tushishda bazaga ulanish, migratsiya va (Development'da) seed.
/// Ulanish xatosi bo'lsa — sababini va tuzatish yo'lini aniq yozib, ilovani to'xtatadi.
/// </summary>
public static class DatabaseStartup
{
    public static async Task MigrateAndSeedAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseStartup");

        var csb = new NpgsqlConnectionStringBuilder(db.Database.GetConnectionString());
        logger.LogInformation("PostgreSQL: {Host}:{Port}, baza '{Database}', foydalanuvchi '{User}'",
            csb.Host, csb.Port, csb.Database, csb.Username);

        try
        {
            // Baza mavjud bo'lmasa, MigrateAsync uni o'zi yaratadi (foydalanuvchida CREATEDB huquqi bo'lsa)
            await db.Database.MigrateAsync();
            logger.LogInformation("Migratsiyalar qo'llandi.");
        }
        catch (Exception ex)
        {
            logger.LogCritical("Bazaga ulanib bo'lmadi: {Hint}", Explain(ex, csb));
            throw;
        }

        // Test akkauntlar faqat Development muhitida yaratiladi
        if (app.Environment.IsDevelopment())
        {
            await DbInitializer.SeedAsync(db, logger);
            logger.LogInformation("Seed (development) muvaffaqiyatli.");
        }
    }

    private static string Explain(Exception ex, NpgsqlConnectionStringBuilder csb)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            switch (e)
            {
                case PostgresException { SqlState: PostgresErrorCodes.InvalidPassword }:
                    return $"'{csb.Username}' foydalanuvchisining paroli noto'g'ri. " +
                           "Tuzatish: dotnet user-secrets set \"ConnectionStrings:DefaultConnection\" \"...;Password=TO'G'RI_PAROL\"";
                case PostgresException { SqlState: PostgresErrorCodes.InvalidAuthorizationSpecification }:
                    return $"'{csb.Username}' foydalanuvchisi mavjud emas yoki pg_hba.conf ulanishga ruxsat bermaydi.";
                case PostgresException { SqlState: PostgresErrorCodes.InvalidCatalogName }:
                    return $"'{csb.Database}' bazasi mavjud emas. Yaratish: psql -U postgres -f scripts/create-database.sql";
                case PostgresException { SqlState: PostgresErrorCodes.InsufficientPrivilege }:
                    return $"'{csb.Username}' foydalanuvchisida bazani yaratish/o'zgartirish huquqi yo'q.";
                case SocketException:
                    return $"{csb.Host}:{csb.Port} manzilida PostgreSQL javob bermadi. " +
                           "PostgreSQL xizmati ishlayotganini va portni tekshiring (Windows: services.msc → postgresql-x64-*).";
                case TimeoutException:
                    return "Ulanish vaqti tugadi — server band yoki tarmoq/firewall to'sayapti.";
            }
        }
        return ex.Message;
    }
}
