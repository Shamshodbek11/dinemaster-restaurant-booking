namespace DineMaster.Application.Services;

/// <summary>
/// Bron bilan bog'liq umumiy qoidalar (BookingService, TablesController, ReminderService).
/// </summary>
public static class BookingRules
{
    public const string Pending = "pending";
    public const string Confirmed = "confirmed";
    public const string Cancelled = "cancelled";
    public const string Completed = "completed";
    public const string NoShow = "no_show";

    /// <summary>Stol bitta bron uchun shu vaqtdan oldin va keyin band hisoblanadi.</summary>
    public static readonly TimeSpan TableHoldWindow = TimeSpan.FromMinutes(90);

    /// <summary>Bron vaqtidan shuncha o'tgach, tasdiqlangan bron avtomatik "completed" bo'ladi.</summary>
    public static readonly TimeSpan AutoCompleteAfter = TimeSpan.FromHours(3);

    public static bool IsActive(string status) => status is Pending or Confirmed;

    /// <summary>
    /// Ikki bron stol bo'yicha to'qnashadimi. Sana+vaqt birga solishtiriladi, shuning uchun
    /// yarim tun atrofidagi bronlar (23:30 va ertasi 00:30) ham to'g'ri aniqlanadi.
    /// </summary>
    public static bool Overlaps(DateOnly d1, TimeOnly t1, DateOnly d2, TimeOnly t2)
        => (d1.ToDateTime(t1) - d2.ToDateTime(t2)).Duration() <= TableHoldWindow;

    /// <summary>Admin/tizim tomonidan ruxsat etilgan status o'tishlari.</summary>
    private static readonly Dictionary<string, string[]> Transitions = new()
    {
        [Pending] = new[] { Confirmed, Cancelled, Completed, NoShow },
        [Confirmed] = new[] { Cancelled, Completed, NoShow },
        [Completed] = new[] { NoShow },
        [NoShow] = new[] { Completed },
        [Cancelled] = Array.Empty<string>(),
    };

    public static bool IsKnownStatus(string status) => Transitions.ContainsKey(status);

    public static bool CanTransition(string from, string to)
        => Transitions.TryGetValue(from, out var targets) && targets.Contains(to);
}
