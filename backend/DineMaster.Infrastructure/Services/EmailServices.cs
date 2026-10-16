using DineMaster.Application.Abstractions;
using DineMaster.Application.Common;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace DineMaster.Infrastructure.Services;

/// <summary>appsettings.json → "Email" bo'limi (parol — faqat user-secrets / env).</summary>
public class EmailOptions
{
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "DineMaster";
    /// <summary>Mijoz "Javob berish" ni bossa xat shu manzilga boradi (masalan support@...). Bo'sh — From.</summary>
    public string ReplyTo { get; set; } = string.Empty;
    public SmtpOptions Smtp { get; set; } = new();

    public bool SmtpConfigured => !string.IsNullOrWhiteSpace(Smtp.Host);
}

public class SmtpOptions
{
    public string Host { get; set; } = string.Empty;
    /// <summary>587 — STARTTLS (Gmail, Mail.ru, Yandex), 465 — SSL.</summary>
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

/// <summary>SMTP orqali haqiqiy xat yuborish (Gmail uchun "App password" kerak).</summary>
public class SmtpEmailService : IEmailService
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailService> _log;

    public SmtpEmailService(IOptions<EmailOptions> options, ILogger<SmtpEmailService> log)
    {
        _options = options.Value;
        _log = log;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        var mime = new MimeMessage();
        var from = string.IsNullOrWhiteSpace(_options.FromAddress) ? _options.Smtp.Username : _options.FromAddress;
        mime.From.Add(new MailboxAddress(_options.FromName, from));
        mime.To.Add(MailboxAddress.Parse(message.To));
        if (!string.IsNullOrWhiteSpace(_options.ReplyTo))
            mime.ReplyTo.Add(MailboxAddress.Parse(_options.ReplyTo));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder { HtmlBody = message.HtmlBody, TextBody = message.TextBody }.ToMessageBody();

        try
        {
            using var client = new SmtpClient { Timeout = 15_000 };
            var security = _options.Smtp.Port == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;
            await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, security, ct);
            if (!string.IsNullOrEmpty(_options.Smtp.Username))
                await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password, ct);
            await client.SendAsync(mime, ct);
            await client.DisconnectAsync(true, ct);
            _log.LogInformation("Email yuborildi: {Subject} → {To}", message.Subject, MaskEmail(message.To));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _log.LogError(ex, "Email yuborilmadi: {Subject} → {To}", message.Subject, MaskEmail(message.To));
            throw new ServiceUnavailableException("Email yuborib bo'lmadi. Birozdan keyin qayta urinib ko'ring.", ex);
        }
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 1 ? "***" : email[0] + "***" + email[at..];
    }
}

/// <summary>
/// Faqat Development (SMTP sozlanmagan bo'lsa): xat yuborilmaydi, matni backend konsoliga chiqadi —
/// tasdiqlash kodini shu yerdan olish mumkin.
/// </summary>
public class DevelopmentEmailService : IEmailService
{
    private readonly ILogger<DevelopmentEmailService> _log;
    public DevelopmentEmailService(ILogger<DevelopmentEmailService> log) { _log = log; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _log.LogWarning("[DEV EMAIL] → {To}\n  Mavzu: {Subject}\n  {Text}", message.To, message.Subject, message.TextBody);
        return Task.CompletedTask;
    }
}

/// <summary>Production'da SMTP sozlanmagan bo'lsa: xat yuborib bo'lmaydi → 503.</summary>
public class NoopEmailService : IEmailService
{
    private readonly ILogger<NoopEmailService> _log;
    public NoopEmailService(ILogger<NoopEmailService> log) { _log = log; }

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _log.LogError("Email provayder (SMTP) sozlanmagan: \"{Subject}\" yuborilmadi.", message.Subject);
        throw new ServiceUnavailableException("Email xizmati sozlanmagan. Administrator bilan bog'laning.");
    }
}
