using DineMaster.Application.Services;
using DineMaster.Application.Services.Payments;
using DineMaster.Domain.Entities;

namespace DineMaster.Tests;

public class OpeningHoursTests
{
    private static readonly TimeSpan Gap = TimeSpan.FromMinutes(60);
    // 2026-10-16 — juma, 2026-10-17 — shanba, 2026-10-18 — yakshanba
    private static readonly DateOnly Friday = new(2026, 10, 16);
    private static readonly DateOnly Saturday = new(2026, 10, 17);
    private static readonly DateOnly Sunday = new(2026, 10, 18);

    private const string Seed =
        """{"mon":"09:00-23:00","tue":"09:00-23:00","wed":"09:00-23:00","thu":"09:00-23:00","fri":"09:00-00:00","sat":"09:00-00:00","sun":"10:00-22:00"}""";

    [Theory]
    [InlineData("19:00", true)]
    [InlineData("10:00", true)]   // yakshanba 10:00 da ochiladi
    [InlineData("09:30", false)]
    [InlineData("21:00", true)]   // yakshanba 22:00 da yopiladi → oxirgi bron 21:00
    [InlineData("21:30", false)]
    public void Sunday_respects_open_close_and_last_booking_gap(string time, bool expected)
        => Assert.Equal(expected, OpeningHours.Parse(Seed).CanStartBookingAt(Sunday, TimeOnly.Parse(time), Gap));

    [Fact]
    public void Midnight_close_allows_booking_until_23_00()
    {
        var h = OpeningHours.Parse(Seed);
        Assert.True(h.CanStartBookingAt(Friday, new TimeOnly(23, 0), Gap));
        Assert.False(h.CanStartBookingAt(Friday, new TimeOnly(23, 30), Gap));
    }

    [Fact]
    public void Overnight_shift_belongs_to_previous_day()
    {
        var h = OpeningHours.Parse("""{"fri":"18:00-03:00","sat":"closed"}""");
        Assert.True(h.CanStartBookingAt(Saturday, new TimeOnly(1, 30), Gap));  // jumaning tungi smenasi
        Assert.False(h.CanStartBookingAt(Saturday, new TimeOnly(2, 30), Gap)); // yopilishga 30 daqiqa
        Assert.False(h.CanStartBookingAt(Saturday, new TimeOnly(19, 0), Gap)); // shanba dam olish
        Assert.Equal("dam olish kuni", h.Describe(Saturday));
    }

    [Theory]
    [InlineData("""{"mon":"24h","tue":"24h","wed":"24h","thu":"24h","fri":"24h","sat":"24h","sun":"24h"}""")]
    [InlineData("""{"mon":"00:00-00:00","tue":"00:00-00:00","wed":"00:00-00:00","thu":"00:00-00:00","fri":"00:00-00:00","sat":"00:00-00:00","sun":"00:00-00:00"}""")]
    public void All_day(string json)
    {
        var h = OpeningHours.Parse(json);
        Assert.True(h.CanStartBookingAt(Sunday, new TimeOnly(3, 0), Gap));
        Assert.True(h.CanStartBookingAt(Sunday, new TimeOnly(23, 30), Gap));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("")]
    [InlineData("not json")]
    public void Unknown_hours_do_not_restrict(string json)
        => Assert.True(OpeningHours.Parse(json).CanStartBookingAt(Sunday, new TimeOnly(4, 0), Gap));
}

public class BookingRulesTests
{
    private static readonly DateOnly D = new(2026, 10, 16);

    [Fact]
    public void Overlap_across_midnight_is_detected()
        => Assert.True(BookingRules.Overlaps(D, new TimeOnly(23, 30), D.AddDays(1), new TimeOnly(0, 30)));

    [Fact]
    public void Two_hours_apart_do_not_overlap()
        => Assert.False(BookingRules.Overlaps(D, new TimeOnly(19, 0), D, new TimeOnly(21, 0)));

    [Theory]
    [InlineData("confirmed", "cancelled", true)]
    [InlineData("confirmed", "completed", true)]
    [InlineData("completed", "no_show", true)]
    [InlineData("completed", "cancelled", false)]
    [InlineData("cancelled", "confirmed", false)]
    public void Status_transitions(string from, string to, bool allowed)
        => Assert.Equal(allowed, BookingRules.CanTransition(from, to));
}

public class RestaurantChangeSetTests
{
    [Fact]
    public void Keeps_only_changed_fields_and_merges()
    {
        var r = new Restaurant { Name = "Eski", Address = "Manzil", City = "Toshkent" };
        var changes = new RestaurantChangeSet { Name = "Yangi", Address = "Manzil", City = "Toshkent" }.OnlyDifferentFrom(r);
        Assert.Equal("Yangi", changes.Name);
        Assert.Null(changes.Address);

        var merged = new RestaurantChangeSet { City = "Samarqand" }.MergeOnto(changes);
        var roundTrip = RestaurantChangeSet.FromJson(merged.ToJson())!;
        Assert.Equal("Yangi", roundTrip.Name);
        Assert.Equal("Samarqand", roundTrip.City);

        roundTrip.ApplyTo(r);
        Assert.Equal("Yangi", r.Name);
        Assert.Equal("Samarqand", r.City);
    }
}

public class ClickSignTests
{
    [Fact]
    public void Md5_lowercase_hex() =>
        Assert.Equal("900150983cd24fb0d6963f7d28e17f72", PaymentService.ClickSign("abc"));
}
