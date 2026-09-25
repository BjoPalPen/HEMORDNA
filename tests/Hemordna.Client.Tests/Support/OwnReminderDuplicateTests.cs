using Hemordna.Client.Contracts;
using Hemordna.Client.Support;

namespace Hemordna.Client.Tests.Support;

/// <summary>Duplikatspärren bakom Veckas "Lägg till i mina påminnelser" (docs/PRODUCT.md §11,
/// steg 2) - rent presentationell: den styr bara om knappen visas, det finns ingen server-sidan
/// unikhetsregel bakom den (se docs/ARCHITECTURE.md, Beslut: Synlighet för påminnelser).</summary>
public class OwnReminderDuplicateTests
{
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private static ReminderResponse MakeOwn(string title, DateOnly date, TimeOnly? timeOfDay)
        => new(Guid.NewGuid(), title, null, date, timeOfDay, null, "Upcoming", DateTimeOffset.UtcNow, "Private");

    [Fact]
    public void No_own_reminders_means_no_duplicate()
    {
        Assert.False(OwnReminderDuplicate.Exists([], "Föräldramöte", Friday, new TimeOnly(14, 0)));
    }

    [Fact]
    public void Same_title_date_and_time_is_a_duplicate()
    {
        var own = new[] { MakeOwn("Föräldramöte", Friday, new TimeOnly(14, 0)) };

        Assert.True(OwnReminderDuplicate.Exists(own, "Föräldramöte", Friday, new TimeOnly(14, 0)));
    }

    [Fact]
    public void Different_time_is_not_a_duplicate()
    {
        var own = new[] { MakeOwn("Föräldramöte", Friday, new TimeOnly(14, 0)) };

        Assert.False(OwnReminderDuplicate.Exists(own, "Föräldramöte", Friday, new TimeOnly(15, 0)));
    }

    [Fact]
    public void Different_title_is_not_a_duplicate()
    {
        var own = new[] { MakeOwn("Föräldramöte", Friday, new TimeOnly(14, 0)) };

        Assert.False(OwnReminderDuplicate.Exists(own, "Läkarbesök", Friday, new TimeOnly(14, 0)));
    }

    [Fact]
    public void Different_date_is_not_a_duplicate()
    {
        var own = new[] { MakeOwn("Föräldramöte", Friday, new TimeOnly(14, 0)) };

        Assert.False(OwnReminderDuplicate.Exists(own, "Föräldramöte", Friday.AddDays(1), new TimeOnly(14, 0)));
    }

    [Fact]
    public void All_day_reminders_match_on_null_time_too()
    {
        var own = new[] { MakeOwn("Föräldramöte", Friday, null) };

        Assert.True(OwnReminderDuplicate.Exists(own, "Föräldramöte", Friday, null));
    }
}
