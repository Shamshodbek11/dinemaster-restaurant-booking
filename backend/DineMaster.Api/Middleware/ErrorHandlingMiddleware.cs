using System.Net;
using System.Text.Json;
using DineMaster.Application.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace DineMaster.Api.Middleware;

/// <summary>
/// Xatolarni HTTP javoblarga aylantiradi. Faqat biznes xatolarining matni klientga
/// yuboriladi; .NET/EF/Npgsql ichki xatolari log'ga yoziladi va klientga umumiy xabar boradi.
/// </summary>
public class ErrorHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _log;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> log)
    {
        _next = next; _log = log;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Klient so'rovni bekor qildi — javob yozishga hojat yo'q
        }
        catch (Exception ex)
        {
            var (status, message) = Map(ex);

            if (status >= 500) _log.LogError(ex, "Kutilmagan xatolik: {Path}", context.Request.Path);
            else _log.LogInformation("So'rov rad etildi ({Status}): {Message}", status, ex.Message);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }, JsonOptions));
        }
    }

    private static (int Status, string Message) Map(Exception ex) => ex switch
    {
        BusinessRuleException e => ((int)HttpStatusCode.BadRequest, e.Message),
        NotFoundException e => ((int)HttpStatusCode.NotFound, e.Message),
        ForbiddenAccessException e => ((int)HttpStatusCode.Forbidden, e.Message),
        ConflictException e => ((int)HttpStatusCode.Conflict, e.Message),
        ServiceUnavailableException e => ((int)HttpStatusCode.ServiceUnavailable, e.Message),
        UnauthorizedAccessException e => ((int)HttpStatusCode.Unauthorized, e.Message),
        BadHttpRequestException e => (e.StatusCode, "So'rov noto'g'ri formatda."),

        // Parallel so'rovlar to'qnashuvi (retry ham yordam bermadi)
        DbUpdateConcurrencyException => ((int)HttpStatusCode.Conflict,
            "Ma'lumot boshqa so'rov tomonidan o'zgartirildi. Qayta urinib ko'ring."),
        _ when FindPostgres(ex) is { SqlState: PostgresErrorCodes.SerializationFailure or PostgresErrorCodes.DeadlockDetected }
            => ((int)HttpStatusCode.Conflict, "Server band, qayta urinib ko'ring."),
        _ when FindPostgres(ex) is { SqlState: PostgresErrorCodes.UniqueViolation }
            => ((int)HttpStatusCode.Conflict, "Bunday yozuv allaqachon mavjud."),
        _ when FindPostgres(ex) is { SqlState: PostgresErrorCodes.ForeignKeyViolation }
            => ((int)HttpStatusCode.Conflict, "Bog'langan ma'lumot mavjud emas yoki undan foydalanilmoqda."),

        // Baza bilan aloqa yo'q (PostgreSQL o'chgan, tarmoq va h.k.)
        _ when ex is NpgsqlException { InnerException: System.Net.Sockets.SocketException or TimeoutException }
               || ex.InnerException is NpgsqlException { InnerException: System.Net.Sockets.SocketException or TimeoutException }
            => ((int)HttpStatusCode.ServiceUnavailable, "Ma'lumotlar bazasi vaqtincha mavjud emas."),

        _ => ((int)HttpStatusCode.InternalServerError, "Ichki server xatosi.")
    };

    private static PostgresException? FindPostgres(Exception? ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException pg) return pg;
        return null;
    }
}
