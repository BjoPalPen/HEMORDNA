using System.Globalization;

namespace Hemordna.Client.Support;

/// <summary>
/// "Fredag 14:00 · Anna · Föräldramöte" for a reminder shared at Household level, "Fredag 14:00 ·
/// Anna har en tid" for BusyOnly, and the same without the time of day for an all-day reminder -
/// Vecka's "Andras tider den här veckan" section. A pure formatting function so it stays testable
/// without a renderable page - see DepartureCountdown for the same reasoning (CLAUDE.md §8).
/// <paramref name="title"/> alone decides which of the two forms is used: the server already
/// nulls it for anything but Household visibility (see
/// Hemordna.Application.Reminders.GetHouseholdReminders), so this function never itself
/// interprets a visibility level - it only formats what it was handed.
/// </summary>
public static class HouseholdReminderRowLabel
{
    private static readonly CultureInfo SwedishCulture = new("sv-SE");

    public static string Build(DateOnly date, TimeOnly? timeOfDay, string memberDisplayName, string? title)
    {
        var weekday = date.ToString("dddd", SwedishCulture);
        var prefix = timeOfDay is { } time ? $"{weekday} {time.ToString("HH:mm", SwedishCulture)}" : weekday;

        return title is { } t
            ? $"{prefix} · {memberDisplayName} · {t}"
            : $"{prefix} · {memberDisplayName} har en tid";
    }
}
