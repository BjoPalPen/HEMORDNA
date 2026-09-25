using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// Whether the signed-in member already has an own reminder matching a shared one they could copy -
/// Vecka's "Lägg till i mina påminnelser" button (docs/PRODUCT.md §11, step 2). Purely
/// presentational: it only controls whether the button is shown. The button disappearing after a
/// successful press is what stops a second press from creating a second copy - there is no
/// server-side uniqueness rule behind this comparison (docs/ARCHITECTURE.md, Beslut: Synlighet för
/// påminnelser).
/// </summary>
public static class OwnReminderDuplicate
{
    public static bool Exists(
        IEnumerable<ReminderResponse> ownReminders, string title, DateOnly date, TimeOnly? timeOfDay)
        => ownReminders.Any(reminder =>
            reminder.Title == title && reminder.Date == date && reminder.TimeOfDay == timeOfDay);
}
