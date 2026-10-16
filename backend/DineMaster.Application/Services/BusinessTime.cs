namespace DineMaster.Application.Services;

/// <summary>
/// Biznes vaqt zonasi (restoran va bron vaqtlari shu zonada kiritiladi).
/// Linux'da IANA ID, Windows'da esa mos keladigan nom ishlatiladi.
/// </summary>
public static class BusinessTime
{
    private const string IanaId = "Asia/Tashkent";
    private const string WindowsId = "West Asia Standard Time";

    private static readonly Lazy<TimeZoneInfo> _zone = new Lazy<TimeZoneInfo>(FindZone);

    public static TimeZoneInfo Zone => _zone.Value;

    private static TimeZoneInfo FindZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(IanaId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById(WindowsId);
        }
    }

    /// <summary>Hozirgi mahalliy vaqt (biznes zonasida).</summary>
    public static DateTime Now() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone);

    /// <summary>Bugungi sana (biznes zonasida).</summary>
    public static DateOnly Today() => DateOnly.FromDateTime(Now());

    /// <summary>Mahalliy sana+vaqtni UTC ga aylantiradi.</summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time) =>
        TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), Zone);
}
