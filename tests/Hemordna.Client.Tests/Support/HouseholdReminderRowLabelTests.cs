using Hemordna.Client.Support;

namespace Hemordna.Client.Tests.Support;

/// <summary>Radformatet på Veckas "Andras tider den här veckan" (docs/PRODUCT.md §11) - den rena
/// textbyggnaden testad fristående, utan en renderad sida (se CLAUDE.md §8 och
/// DepartureCountdownTests för samma resonemang).</summary>
public class HouseholdReminderRowLabelTests
{
    private static readonly DateOnly Friday = new(2026, 2, 6);

    [Fact]
    public void A_household_reminder_with_a_time_shows_time_name_and_title()
    {
        var label = HouseholdReminderRowLabel.Build(Friday, new TimeOnly(14, 0), "Anna", "Föräldramöte");

        Assert.Equal("fredag 14:00 · Anna · Föräldramöte", label);
    }

    [Fact]
    public void A_busy_only_reminder_with_a_time_shows_time_and_name_but_never_a_title()
    {
        var label = HouseholdReminderRowLabel.Build(Friday, new TimeOnly(14, 0), "Anna", null);

        Assert.Equal("fredag 14:00 · Anna har en tid", label);
    }

    [Fact]
    public void A_household_reminder_without_a_time_shows_the_whole_day()
    {
        var label = HouseholdReminderRowLabel.Build(Friday, null, "Anna", "Föräldramöte");

        Assert.Equal("fredag · Anna · Föräldramöte", label);
    }

    [Fact]
    public void A_busy_only_reminder_without_a_time_shows_the_whole_day_and_never_a_title()
    {
        var label = HouseholdReminderRowLabel.Build(Friday, null, "Anna", null);

        Assert.Equal("fredag · Anna har en tid", label);
    }
}
