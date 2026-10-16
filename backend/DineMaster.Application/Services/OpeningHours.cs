using System.Text.Json;

namespace DineMaster.Application.Services;

/// <summary>
/// Restoran ish vaqti: {"mon":"10:00-23:00", ..., "fri":"10:00-02:00", "sun":"closed"}.
///  - "HH:mm-HH:mm"; yopilish ochilishdan oldin/teng bo'lsa — tungi smena (ertasi kuni yopiladi), "00:00" = yarim tun
///  - "closed" / "yopiq" / "" — dam olish kuni; "24h" yoki "00:00-24:00" — kun bo'yi
///  - {"open":"10:00","close":"23:00"} ko'rinishi ham qabul qilinadi
/// Ish vaqti umuman ko'rsatilmagan bo'lsa (bo'sh yoki noto'g'ri JSON) — cheklov qo'yilmaydi.
/// </summary>
public sealed class OpeningHours
{
    private static readonly string[] DayKeys = { "sun", "mon", "tue", "wed", "thu", "fri", "sat" };

    private readonly record struct Interval(TimeOnly Open, TimeOnly Close, bool AllDay);

    // null — ish vaqti noma'lum (cheklovsiz); qiymat null — yopiq kun
    private readonly Dictionary<DayOfWeek, Interval?>? _days;
    // Maxsus kunlar (bayram, sanitariya kuni...) — oddiy jadvaldan ustun
    private readonly Dictionary<DateOnly, Interval?> _special = new();

    private OpeningHours(Dictionary<DayOfWeek, Interval?>? days) => _days = days;

    public bool IsKnown => _days is not null;

    /// <summary>Oddiy jadval + maxsus kunlar: {"2026-12-31":"closed","2027-01-01":"12:00-20:00"}.</summary>
    public static OpeningHours Parse(string? json, string? specialJson)
    {
        var h = Parse(json);
        if (string.IsNullOrWhiteSpace(specialJson)) return h;
        try
        {
            using var doc = JsonDocument.Parse(specialJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return h;
            foreach (var p in doc.RootElement.EnumerateObject())
                if (DateOnly.TryParseExact(p.Name, "yyyy-MM-dd", out var d))
                    h._special[d] = ParseDay(p.Value);
        }
        catch (JsonException) { /* noto'g'ri — e'tiborsiz */ }
        return h;
    }

    /// <summary>Maxsus kunlar JSON'i to'g'riligini tekshiradi (null — to'g'ri).</summary>
    public static string? ValidateSpecial(string? specialJson)
    {
        if (string.IsNullOrWhiteSpace(specialJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(specialJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return "Maxsus kunlar noto'g'ri formatda.";
            foreach (var p in doc.RootElement.EnumerateObject())
            {
                if (!DateOnly.TryParseExact(p.Name, "yyyy-MM-dd", out _))
                    return $"Sana noto'g'ri: {p.Name} (yyyy-MM-dd kerak).";
                var closed = p.Value.ValueKind == JsonValueKind.String &&
                             p.Value.GetString()?.Trim().ToLowerInvariant() is "closed" or "yopiq";
                if (!closed && ParseDay(p.Value) is null)
                    return $"{p.Name}: vaqt noto'g'ri (\"12:00-20:00\" yoki \"closed\").";
            }
            return null;
        }
        catch (JsonException) { return "Maxsus kunlar noto'g'ri formatda."; }
    }

    /// <summary>Shu kun uchun interval: maxsus kun bo'lsa — u, aks holda hafta kuni jadvali.
    /// Known=false — ish vaqti umuman ko'rsatilmagan (cheklovsiz).</summary>
    private (bool Known, Interval? Interval) IntervalFor(DateOnly day)
    {
        if (_special.TryGetValue(day, out var sp)) return (true, sp);
        if (_days is null) return (false, null);
        return (true, _days[day.DayOfWeek]);
    }

    public static OpeningHours Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new OpeningHours(null);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return new OpeningHours(null);

            var raw = doc.RootElement.EnumerateObject()
                .ToDictionary(p => p.Name.Trim().ToLowerInvariant(), p => p.Value.Clone());

            var days = new Dictionary<DayOfWeek, Interval?>();
            var anyKnown = false;
            for (var i = 0; i < 7; i++)
            {
                var key = DayKeys[i];
                var full = ((DayOfWeek)i).ToString().ToLowerInvariant();
                if (!raw.TryGetValue(key, out var value) && !raw.TryGetValue(full, out value))
                {
                    days[(DayOfWeek)i] = null;
                    continue;
                }
                anyKnown = true;
                days[(DayOfWeek)i] = ParseDay(value);
            }
            return new OpeningHours(anyKnown ? days : null);
        }
        catch (JsonException)
        {
            return new OpeningHours(null);
        }
    }

    private static Interval? ParseDay(JsonElement value)
    {
        string? open = null, close = null;
        if (value.ValueKind == JsonValueKind.String)
        {
            var s = value.GetString()?.Trim().ToLowerInvariant() ?? string.Empty;
            if (s is "" or "closed" or "yopiq" or "-" or "выходной") return null;
            if (s is "24h" or "24/7" or "kun bo'yi") return new Interval(TimeOnly.MinValue, TimeOnly.MinValue, true);
            var parts = s.Replace('–', '-').Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2) return null;
            (open, close) = (parts[0], parts[1]);
        }
        else if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("closed", out var c) && c.ValueKind == JsonValueKind.True) return null;
            open = value.TryGetProperty("open", out var o) ? o.GetString() : null;
            close = value.TryGetProperty("close", out var cl) ? cl.GetString() : null;
        }
        else return null;

        if (!TryTime(open, out var openT) || !TryTime(close, out var closeT, allow24: true)) return null;
        var allDay = openT == closeT; // 00:00-24:00 yoki 00:00-00:00
        return new Interval(openT, closeT, allDay);
    }

    private static bool TryTime(string? s, out TimeOnly t, bool allow24 = false)
    {
        t = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        s = s.Trim();
        if (allow24 && s is "24:00" or "24") { t = TimeOnly.MinValue; return true; }
        return TimeOnly.TryParse(s, System.Globalization.CultureInfo.InvariantCulture, out t);
    }

    /// <summary>
    /// Shu sana va vaqtda bron boshlanishi mumkinmi: ochilishdan keyin va yopilishdan kamida
    /// <paramref name="lastBookingBeforeClose"/> oldin. Kechagi tungi smena ham hisobga olinadi.
    /// </summary>
    public bool CanStartBookingAt(DateOnly date, TimeOnly time, TimeSpan lastBookingBeforeClose)
    {
        var (known, today) = IntervalFor(date);
        if (!known) return true;
        var at = date.ToDateTime(time);

        if (today is { AllDay: true }) return true;

        foreach (var day in new[] { date.AddDays(-1), date })
        {
            var (k, interval) = IntervalFor(day);
            if (!k || interval is not Interval iv || iv.AllDay) continue;
            var start = day.ToDateTime(iv.Open);
            var end = (iv.Close <= iv.Open ? day.AddDays(1) : day).ToDateTime(iv.Close);
            if (at >= start && at <= end - lastBookingBeforeClose) return true;
        }
        return false;
    }

    /// <summary>Foydalanuvchiga ko'rsatish uchun: "10:00–23:00", "kun bo'yi" yoki "dam olish kuni".</summary>
    public string Describe(DateOnly date)
    {
        var (known, interval) = IntervalFor(date);
        if (!known) return "ko'rsatilmagan";
        var text = interval switch
        {
            null => "dam olish kuni",
            { AllDay: true } => "kun bo'yi",
            Interval iv => $"{iv.Open:HH\\:mm}–{iv.Close:HH\\:mm}"
        };
        return _special.ContainsKey(date) ? $"{text} (maxsus kun)" : text;
    }
}
