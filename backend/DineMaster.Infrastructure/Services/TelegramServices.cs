using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using DineMaster.Application.Abstractions;
using DineMaster.Application.Services;
using DineMaster.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DineMaster.Infrastructure.Services;

/// <summary>appsettings.json → "Telegram" (BotToken — faqat user-secrets / env).</summary>
public class TelegramOptions
{
    /// <summary>@BotFather bergan token.</summary>
    public string BotToken { get; set; } = string.Empty;
    /// <summary>Bot nomi @ siz, masalan DineMasterBot (ulanish havolasi uchun).</summary>
    public string BotUsername { get; set; } = string.Empty;

    public bool Enabled => !string.IsNullOrWhiteSpace(BotToken) && !string.IsNullOrWhiteSpace(BotUsername);
}

/// <summary>Bildirishnomalar navbati (bitta nusxa): AppDbContext yozadi, TelegramNotificationSender o'qiydi.</summary>
public sealed class NotificationChannel : INotificationPublisher
{
    private readonly Channel<NotificationMessage> _channel =
        Channel.CreateBounded<NotificationMessage>(new BoundedChannelOptions(10_000) { FullMode = BoundedChannelFullMode.DropOldest });

    public ChannelReader<NotificationMessage> Reader => _channel.Reader;

    public void Publish(IReadOnlyCollection<NotificationMessage> messages)
    {
        foreach (var m in messages) _channel.Writer.TryWrite(m);
    }
}

/// <summary>Telegram Bot API (faqat kerakli usullar).</summary>
public class TelegramClient
{
    private readonly HttpClient _http;
    private readonly TelegramOptions _options;

    public TelegramClient(HttpClient http, IOptions<TelegramOptions> options)
    {
        _http = http;
        _options = options.Value;
        _http.Timeout = TimeSpan.FromSeconds(40);
    }

    private string Url(string method) => $"https://api.telegram.org/bot{_options.BotToken}/{method}";

    public async Task SendMessageAsync(long chatId, string html, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(Url("sendMessage"),
            new { chat_id = chatId, text = html, parse_mode = "HTML", disable_web_page_preview = true }, ct);
        if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Forbidden) // 403 — foydalanuvchi botni bloklagan
            throw new HttpRequestException($"Telegram sendMessage: {(int)resp.StatusCode}");
    }

    public async Task<JsonElement[]> GetUpdatesAsync(long offset, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await _http.GetStringAsync($"{Url("getUpdates")}?timeout=25&offset={offset}", ct));
        return doc.RootElement.GetProperty("result").EnumerateArray().Select(e => e.Clone()).ToArray();
    }
}

/// <summary>
/// Bot buyruqlari (long polling, webhook shart emas): "/start KOD" — profil Telegram'ga ulanadi,
/// "/stop" — uziladi. KOD profil sahifasidagi "Telegram'ni ulash" tugmasi orqali olinadi.
/// </summary>
public class TelegramBotService : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly TelegramOptions _options;
    private readonly ILogger<TelegramBotService> _log;

    public TelegramBotService(IServiceProvider sp, IOptions<TelegramOptions> options, ILogger<TelegramBotService> log)
    {
        _sp = sp; _options = options.Value; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled) return;
        long offset = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _sp.CreateScope();
                var client = scope.ServiceProvider.GetRequiredService<TelegramClient>();
                foreach (var update in await client.GetUpdatesAsync(offset, stoppingToken))
                {
                    offset = update.GetProperty("update_id").GetInt64() + 1;
                    if (!update.TryGetProperty("message", out var msg) || !msg.TryGetProperty("text", out var textEl)) continue;
                    var chatId = msg.GetProperty("chat").GetProperty("id").GetInt64();
                    await HandleAsync(scope.ServiceProvider, client, chatId, textEl.GetString() ?? string.Empty, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Telegram getUpdates xatosi");
                try { await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); } catch { break; }
            }
        }
    }

    private static async Task HandleAsync(IServiceProvider sp, TelegramClient client, long chatId, string text, CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var brand = EmailTemplates.Brand;
        if (text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var code = text.Length > 6 ? text[6..].Trim().ToUpperInvariant() : string.Empty;
            var profile = code.Length == 0 ? null : await db.Profiles.FirstOrDefaultAsync(p => p.TelegramLinkCode == code, ct);
            if (profile is null)
            {
                await client.SendMessageAsync(chatId,
                    $"Salom! Bu {brand} bildirishnomalari boti.\nUlash uchun saytda <b>Profil → Sozlamalar → Telegram'ni ulash</b> tugmasini bosing.", ct);
                return;
            }
            profile.TelegramChatId = chatId;
            profile.TelegramLinkCode = null;
            await db.SaveChangesAsync(ct);
            await client.SendMessageAsync(chatId, $"✅ {WebUtility.HtmlEncode(profile.FullName)}, {brand} bildirishnomalari ulandi.\nO'chirish: /stop", ct);
        }
        else if (text.StartsWith("/stop", StringComparison.OrdinalIgnoreCase))
        {
            var profiles = await db.Profiles.Where(p => p.TelegramChatId == chatId).ToListAsync(ct);
            foreach (var p in profiles) p.TelegramChatId = null;
            await db.SaveChangesAsync(ct);
            await client.SendMessageAsync(chatId, "Bildirishnomalar o'chirildi. Qayta ulash — saytdagi profil orqali.", ct);
        }
    }
}

/// <summary>Navbatdagi bildirishnomalarni Telegram'ni ulagan foydalanuvchilarga yuboradi.</summary>
public class TelegramNotificationSender : BackgroundService
{
    private readonly NotificationChannel _channel;
    private readonly IServiceProvider _sp;
    private readonly TelegramOptions _options;
    private readonly BookingOptions _booking;
    private readonly ILogger<TelegramNotificationSender> _log;

    public TelegramNotificationSender(NotificationChannel channel, IServiceProvider sp, IOptions<TelegramOptions> options,
        IOptions<BookingOptions> booking, ILogger<TelegramNotificationSender> log)
    {
        _channel = channel; _sp = sp; _options = options.Value; _booking = booking.Value; _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            if (!_options.Enabled) continue;
            try
            {
                using var scope = _sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var chatId = await db.Profiles.Where(p => p.Id == message.UserId).Select(p => p.TelegramChatId).FirstOrDefaultAsync(stoppingToken);
                if (chatId is not long id) continue;

                var html = $"<b>{WebUtility.HtmlEncode(message.Title)}</b>\n{WebUtility.HtmlEncode(message.Body)}";
                if (!string.IsNullOrWhiteSpace(message.Link))
                {
                    var url = message.Link.StartsWith("http") ? message.Link : $"{_booking.FrontendBaseUrl.TrimEnd('/')}{message.Link}";
                    html += $"\n<a href=\"{WebUtility.HtmlEncode(url)}\">Ochish</a>";
                }
                await scope.ServiceProvider.GetRequiredService<TelegramClient>().SendMessageAsync(id, html, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Telegram xabari yuborilmadi");
            }
        }
    }
}
