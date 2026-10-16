using System.Net;
using DineMaster.Application.Abstractions;
using DineMaster.Domain.Entities;

namespace DineMaster.Application.Services;

/// <summary>Email xatlari matnlari (HTML + oddiy matn).</summary>
public static class EmailTemplates
{
    /// <summary>Dastur nomi (appsettings: Branding:Name). Xatlar, PDF va Telegram xabarlarida ishlatiladi.</summary>
    public static string Brand { get; set; } = "DineMaster";

    public static EmailMessage Code(string to, string fullName, string purpose, string code, int minutesValid)
    {
        var (subject, intro) = purpose switch
        {
            EmailCode.PurposeResetPassword => ($"{Brand}: parolni tiklash kodi",
                "Parolni tiklash uchun quyidagi kodni kiriting."),
            _ => ($"{Brand}: emailni tasdiqlash kodi",
                "Ro'yxatdan o'tishni yakunlash uchun quyidagi kodni kiriting."),
        };

        var text = $"Assalomu alaykum, {fullName}!\n\n{intro}\n\nKod: {code}\n\n" +
                   $"Kod {minutesValid} daqiqa amal qiladi. Agar bu so'rovni siz yubormagan bo'lsangiz, xatni e'tiborsiz qoldiring.\n\n{Brand}";
        var html = Layout($"""
            <p>Assalomu alaykum, {Enc(fullName)}!</p>
            <p>{Enc(intro)}</p>
            <p style="font-size:32px;font-weight:700;letter-spacing:8px;margin:24px 0;color:#b45309">{Enc(code)}</p>
            <p style="color:#6b7280">Kod {minutesValid} daqiqa amal qiladi. Agar bu so'rovni siz yubormagan bo'lsangiz, xatni e'tiborsiz qoldiring.</p>
            """);
        return new EmailMessage(to, subject, html, text);
    }

    public static EmailMessage WaitlistSpotOpened(string to, string fullName, string restaurantName, DateOnly date, string link)
    {
        var subject = $"{Brand}: {restaurantName} — joy bo'shadi";
        var text = $"Assalomu alaykum, {fullName}!\n\n{restaurantName} restoranida {date:dd.MM.yyyy} sanasiga joy bo'shadi. " +
                   $"Siz kutish ro'yxatida birinchi navbatdasiz — tezroq bron qiling: {link}\n\n{Brand}";
        var html = Layout($"""
            <p>Assalomu alaykum, {Enc(fullName)}!</p>
            <p><b>{Enc(restaurantName)}</b> restoranida <b>{date:dd.MM.yyyy}</b> sanasiga joy bo'shadi.
               Siz kutish ro'yxatida birinchi navbatdasiz.</p>
            <p><a href="{Enc(link)}" style="display:inline-block;background:#b45309;color:#fff;padding:10px 18px;border-radius:8px;text-decoration:none">Bron qilish</a></p>
            """);
        return new EmailMessage(to, subject, html, text);
    }

    private static string Enc(string s) => WebUtility.HtmlEncode(s);

    private static string Layout(string body) => $"""
        <div style="font-family:Arial,sans-serif;max-width:520px;margin:0 auto;padding:24px;color:#111827">
          <h2 style="margin:0 0 16px">{Enc(Brand)}</h2>
          {body}
        </div>
        """;
}
