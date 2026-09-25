using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// Whether the signed-in member already has an own reminder matching a shared one they could copy -
/// Vecka's "Lägg till i mina påminnelser" button (docs/PRODUCT.md §11, steg 2). Rent
/// presentationell: den styr bara om knappen visas. Att knappen försvinner efter ett lyckat tryck
/// är det som hindrar ett andra tryck från att ge två kopior - det finns ingen server-sidan
/// unikhetsregel bakom den här jämförelsen (docs/ARCHITECTURE.md, Beslut: Synlighet för
/// påminnelser).
/// </summary>
public static class OwnReminderDuplicate
{
    public static bool Exists(
        IEnumerable<ReminderResponse> ownReminders, string title, DateOnly date, TimeOnly? timeOfDay)
        => ownReminders.Any(reminder =>
            reminder.Title == title && reminder.Date == date && reminder.TimeOfDay == timeOfDay);
}
