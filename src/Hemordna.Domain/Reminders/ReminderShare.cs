using Hemordna.Domain.Common;

namespace Hemordna.Domain.Reminders;

/// <summary>
/// One household member a <see cref="Reminder"/>'s owner has explicitly chosen to let see its
/// time, when <see cref="Reminder.Audience"/> is <see cref="ReminderAudience.Selected"/>.
/// Meaningless outside that state, and never present there: <see cref="Reminder.SetAudience"/>
/// and <see cref="Reminder.ChangeVisibility"/> both clear every row out the moment
/// <see cref="ReminderAudience.Everyone"/> is chosen or <see cref="ReminderVisibility.Private"/>
/// is set, so this table never holds a row nobody needs to check against (docs/ARCHITECTURE.md,
/// "Beslut: Synlighet för påminnelser").
/// </summary>
/// <remarks>
/// Deliberately constructible only from <see cref="Reminder.SetAudience"/> (an internal factory,
/// not a public one like <see cref="SentReminderNotification.Create"/>) - unlike that ledger,
/// which nothing else in the domain governs, a share is a genuine part of the
/// <see cref="Reminder"/> aggregate. Routing every creation through the owning reminder is what
/// makes the "owner can never be in their own audience" and "only an upcoming reminder can be
/// re-shared" rules impossible to bypass from outside the aggregate.
/// </remarks>
public sealed class ReminderShare
{
    private ReminderShare(Guid id, Guid reminderId, Guid memberId)
    {
        Id = id;
        ReminderId = reminderId;
        MemberId = memberId;
    }

    public Guid Id { get; private set; }

    public Guid ReminderId { get; private set; }

    /// <summary>The member allowed to see the time - never the reminder's own owner, see
    /// <see cref="Reminder.SetAudience"/>.</summary>
    public Guid MemberId { get; private set; }

    internal static ReminderShare Create(Guid reminderId, Guid memberId)
    {
        Guard.AgainstEmpty(reminderId, nameof(reminderId));
        Guard.AgainstEmpty(memberId, nameof(memberId));

        return new ReminderShare(Guid.NewGuid(), reminderId, memberId);
    }
}
