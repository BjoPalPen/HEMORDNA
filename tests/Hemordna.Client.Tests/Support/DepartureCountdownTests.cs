using Hemordna.Client.Contracts;
using Hemordna.Client.Support;

namespace Hemordna.Client.Tests.Support;

/// <summary>Nedräkning mot avgång (docs/PRODUCT.md §11, DepartureLabel i MinDag.razor) - den
/// rena beräkningen testad med fasta tidpunkter, utan en renderad komponent (se CLAUDE.md §8:
/// det här är precis vad Hemordna.Client.Tests finns för, eftersom E2E inte kan styra klockan).
/// </summary>
public class DepartureCountdownTests
{
    private static readonly DateTime Now = new(2026, 9, 24, 12, 0, 0);

    [Theory]
    [InlineData(60, 12)]
    [InlineData(25, 5)]
    [InlineData(1, 1)]
    [InlineData(21, 5)] // avrundning uppåt: 21 / 5 = 4,2 -> 5 staplar
    public void Calculate_fills_bars_by_five_minute_quantum_rounded_up(int minutesUntilDeparture, int expectedFilledBars)
    {
        var departure = Now.AddMinutes(minutesUntilDeparture);

        var countdown = DepartureCountdown.Calculate(Now, departure);

        Assert.NotNull(countdown);
        Assert.Equal(expectedFilledBars, countdown!.Value.FilledBars);
        Assert.Equal(DepartureCountdown.TotalBars, countdown.Value.TotalBars);
        Assert.Equal(minutesUntilDeparture, countdown.Value.MinutesRemaining);
    }

    [Fact]
    public void Calculate_returns_null_when_departure_has_already_passed()
    {
        Assert.Null(DepartureCountdown.Calculate(Now, Now)); // 0 minuter kvar
        Assert.Null(DepartureCountdown.Calculate(Now, Now.AddMinutes(-1)));
    }

    [Fact]
    public void Calculate_returns_null_when_more_than_sixty_minutes_remain()
    {
        Assert.Null(DepartureCountdown.Calculate(Now, Now.AddMinutes(61)));
    }

    [Fact]
    public void Calculate_at_exactly_sixty_minutes_still_shows_the_full_countdown()
    {
        var countdown = DepartureCountdown.Calculate(Now, Now.AddMinutes(60));

        Assert.NotNull(countdown);
        Assert.Equal(12, countdown!.Value.FilledBars);
    }

    [Fact]
    public void SelectReminder_ignores_a_reminder_without_a_time_of_day()
    {
        var reminder = MakeReminder(
            "Utan klockslag", DateOnly.FromDateTime(Now), timeOfDay: null, travelMinutes: 15);

        Assert.Null(DepartureCountdown.SelectReminder([reminder], Now));
    }

    [Fact]
    public void SelectReminder_ignores_a_reminder_without_travel_minutes()
    {
        var reminder = MakeReminder(
            "Utan restid", DateOnly.FromDateTime(Now), TimeOnly.FromDateTime(Now.AddMinutes(20)), travelMinutes: null);

        Assert.Null(DepartureCountdown.SelectReminder([reminder], Now));
    }

    [Fact]
    public void SelectReminder_returns_null_when_nothing_is_within_the_window()
    {
        var date = DateOnly.FromDateTime(Now);
        // Avgång om 65 min (klockslag 13:10 minus 5 min restid = 13:05) - strax utanför fönstret.
        var outside = MakeReminder("Mötet", date, TimeOnly.FromDateTime(Now.AddMinutes(70)), travelMinutes: 5);

        Assert.Null(DepartureCountdown.SelectReminder([outside], Now));
    }

    [Fact]
    public void SelectReminder_picks_only_the_nearest_of_several_within_the_window()
    {
        var date = DateOnly.FromDateTime(Now);
        // Avgångar (klockslag minus restid): "Tandläkaren" om 50 min, "Frisören" om 10 min,
        // "Mötet" om 65 min - utanför 60-minutersfönstret och ska aldrig kunna väljas.
        var farther = MakeReminder("Tandläkaren", date, TimeOnly.FromDateTime(Now.AddMinutes(60)), travelMinutes: 10);
        var nearest = MakeReminder("Frisören", date, TimeOnly.FromDateTime(Now.AddMinutes(15)), travelMinutes: 5);
        var outside = MakeReminder("Mötet", date, TimeOnly.FromDateTime(Now.AddMinutes(70)), travelMinutes: 5);

        var selected = DepartureCountdown.SelectReminder([farther, nearest, outside], Now);

        Assert.NotNull(selected);
        Assert.Equal(nearest.Id, selected!.Id);
    }

    /// <summary>En avbockad påminnelse ligger kvar på dagen (MinDag.razor:s TodaysReminders/
    /// ReminderCheckedRow), men har ägaren själv sagt "den här är avklarad" ska appen inte
    /// motsäga det genom att fortsätta räkna ner mot den - samma skäl som redan tystar notiserna
    /// för den (ReminderNotificationSelector, ReminderStatus.CheckedOff).</summary>
    [Fact]
    public void SelectReminder_ignores_a_checked_off_reminder_even_when_it_is_nearer()
    {
        var date = DateOnly.FromDateTime(Now);
        var checkedOff = MakeReminder(
            "Avbockad", date, TimeOnly.FromDateTime(Now.AddMinutes(20)), travelMinutes: 0, status: "CheckedOff");
        var upcoming = MakeReminder(
            "Kommande", date, TimeOnly.FromDateTime(Now.AddMinutes(45)), travelMinutes: 0);

        var selected = DepartureCountdown.SelectReminder([checkedOff, upcoming], Now);

        Assert.NotNull(selected);
        Assert.Equal(upcoming.Id, selected!.Id);
    }

    /// <summary>Två avgångar som båda avrundas UPPÅT till samma antal minuter (24m50s och
    /// 24m10s blir båda "25 min") ska ändå avgöras av vilken som faktiskt ligger närmast i
    /// tiden, inte av Calculate's redan avrundade MinutesRemaining (som gör dem lika) eller av
    /// listordningen. "Tidigare i tiden" (12:24:10) listas sist med avsikt: en
    /// implementation som jämför det avrundade värdet och bara byter vid ett strikt mindre
    /// (<c>&lt;</c>) skulle behålla den FÖRSTA av två lika avrundade värden - fel svar här,
    /// eftersom den riktiga avgången 12:24:50 inte är den som ligger närmast.</summary>
    [Fact]
    public void SelectReminder_breaks_a_rounding_tie_by_the_real_departure_time()
    {
        var date = DateOnly.FromDateTime(Now);
        var laterRoundedTheSame = MakeReminder(
            "Senare", date, new TimeOnly(12, 24, 50), travelMinutes: 0);
        var trulyNearest = MakeReminder(
            "Faktiskt närmast", date, new TimeOnly(12, 24, 10), travelMinutes: 0);

        var selected = DepartureCountdown.SelectReminder([laterRoundedTheSame, trulyNearest], Now);

        Assert.NotNull(selected);
        Assert.Equal(trulyNearest.Id, selected!.Id);
    }

    private static ReminderResponse MakeReminder(
        string title, DateOnly date, TimeOnly? timeOfDay, int? travelMinutes, string status = "Upcoming")
        => new(Guid.NewGuid(), title, Location: null, date, timeOfDay, travelMinutes, status, CreatedAt: DateTimeOffset.UtcNow);
}
