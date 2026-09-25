using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Reminders;

/// <summary>
/// A member's own appointment they want to be reminded of - a doctor's visit, a meeting, a
/// dentist's slot. Owned by the member who created it, and only they can ever change, check
/// off, cancel or restore it - nobody else acts on someone else's reminder, whatever
/// <see cref="Visibility"/> says (docs/PRODUCT.md §8). Visibility only controls what OTHER
/// members can see, defaults to <see cref="ReminderVisibility.Private"/>, and never extends to
/// <see cref="Location"/>: see <see cref="ReminderVisibility"/> for exactly what each level
/// exposes. A reminder never counts toward anyone's time budget, is never "overdue", never
/// rotates and never earns time credit - which is exactly why this is its own entity rather
/// than a <see cref="TaskDefinition"/>/<see cref="TaskOccurrence"/> pair.
/// </summary>
public sealed class Reminder
{
    /// <summary>Long enough for any real appointment title, short enough to stay readable in a notification.</summary>
    public const int MaxTitleLength = 100;

    /// <summary>Free text for where the appointment is - a clinic name, an address fragment.</summary>
    public const int MaxLocationLength = 200;

    /// <summary>A single trip can take at most a full day - same reasoning as
    /// <see cref="TaskDefinition.MaxEstimatedMinutes"/>.</summary>
    public const int MaxTravelMinutes = 24 * 60;

    private Reminder(
        Guid id,
        Guid householdId,
        Guid memberId,
        string title,
        string? location,
        DateOnly date,
        TimeOnly? timeOfDay,
        DateTimeOffset createdAt,
        int? travelMinutes,
        ReminderVisibility visibility)
    {
        Id = id;
        HouseholdId = householdId;
        MemberId = memberId;
        Title = title;
        Location = location;
        Date = date;
        TimeOfDay = timeOfDay;
        Status = ReminderStatus.Upcoming;
        CreatedAt = createdAt;
        TravelMinutes = travelMinutes;
        Visibility = visibility;
    }

    public Guid Id { get; private set; }

    /// <summary>Tenant key.</summary>
    public Guid HouseholdId { get; private set; }

    /// <summary>The member this reminder belongs to. A reminder is private to its owner - see class remarks.</summary>
    public Guid MemberId { get; private set; }

    public string Title { get; private set; }

    /// <summary>Free-text location, or <c>null</c> when none was given.</summary>
    public string? Location { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>The time this is due, or <c>null</c> meaning "all day".</summary>
    public TimeOnly? TimeOfDay { get; private set; }

    /// <summary>
    /// Minutes to leave before <see cref="TimeOfDay"/> - the number that actually matters
    /// ("13:30, not 14:00"), see docs/PRODUCT.md §11. <c>null</c> when no travel time is tracked.
    /// Always <c>null</c> for an "all day" reminder - see <see cref="SetTravelMinutes"/> and
    /// <see cref="MoveTo"/>.
    /// </summary>
    public int? TravelMinutes { get; private set; }

    public ReminderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// What the rest of the household can see of this reminder. Defaults to
    /// <see cref="ReminderVisibility.Private"/> - see <see cref="ReminderVisibility"/> and this
    /// class's own remarks.
    /// </summary>
    public ReminderVisibility Visibility { get; private set; }

    /// <summary>
    /// Creates a new, upcoming reminder. <paramref name="date"/> is validated against
    /// <paramref name="createdAt"/> the same way <see cref="TaskOccurrence"/> validates a newly
    /// scheduled date - no clock is read here; "today" is whatever the caller says it is.
    /// </summary>
    public static Reminder Create(
        Guid householdId,
        Guid memberId,
        string title,
        string? location,
        DateOnly date,
        TimeOnly? timeOfDay,
        DateTimeOffset createdAt,
        int? travelMinutes = null,
        ReminderVisibility visibility = ReminderVisibility.Private)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        SchedulingDate.Validate(date, DateOnly.FromDateTime(createdAt.UtcDateTime));

        var validatedTitle = ValidateTitle(title);

        return new Reminder(
            Guid.NewGuid(),
            householdId,
            memberId,
            validatedTitle,
            ValidateLocation(location),
            date,
            timeOfDay,
            createdAt,
            ValidateTravelMinutes(travelMinutes, timeOfDay, validatedTitle),
            visibility);
    }

    /// <summary>Changes the title. Only an upcoming reminder can be changed.</summary>
    public void ChangeTitle(string title)
    {
        EnsureUpcoming("changed");

        Title = ValidateTitle(title);
    }

    /// <summary>
    /// Changes the location, or clears it with a blank value. Only an upcoming reminder can be
    /// changed.
    /// </summary>
    public void ChangeLocation(string? location)
    {
        EnsureUpcoming("changed");

        Location = ValidateLocation(location);
    }

    /// <summary>
    /// Changes what the rest of the household can see of this reminder - see
    /// <see cref="ReminderVisibility"/>. Only an upcoming reminder can be changed, same as
    /// <see cref="ChangeTitle"/> and <see cref="ChangeLocation"/>.
    /// </summary>
    public void ChangeVisibility(ReminderVisibility visibility)
    {
        EnsureUpcoming("changed");

        Visibility = visibility;
    }

    /// <summary>
    /// Moves this reminder to a new date and time of day. Only an upcoming reminder can be
    /// moved. Unlike <see cref="Create"/>, no reference date is available here, so the date is
    /// only checked against the supported calendar bounds - the same choice
    /// <see cref="TaskOccurrence.DeferTo"/> and <see cref="TaskOccurrence.ReanchorTo"/> already
    /// make for the same reason. Clearing the time of day (moving to "all day") also clears
    /// <see cref="TravelMinutes"/> - the same stale-lock cleanup
    /// <see cref="TaskDefinition.SetRecurrence"/> already does for
    /// <see cref="TaskDefinition.PreferredWeekday"/> when a change leaves it pointing at
    /// something that no longer exists: a travel time with no departure time to count back from
    /// would just be confusing left behind.
    /// </summary>
    public void MoveTo(DateOnly date, TimeOnly? timeOfDay)
    {
        EnsureUpcoming("moved");
        SchedulingDate.ValidateCalendar(date);

        Date = date;
        TimeOfDay = timeOfDay;

        if (timeOfDay is null)
        {
            TravelMinutes = null;
        }
    }

    /// <summary>
    /// Sets or clears the travel time before <see cref="TimeOfDay"/> - see docs/PRODUCT.md §11
    /// and this class's own remarks on <see cref="TravelMinutes"/>. Requires a time of day to
    /// count back from - the same kind of requirement
    /// <see cref="TaskDefinition.SetPreferredWeekday"/> enforces against a task with no single
    /// weekday to lock to. Only an upcoming reminder can be changed.
    /// </summary>
    public void SetTravelMinutes(int? travelMinutes)
    {
        EnsureUpcoming("changed");

        TravelMinutes = ValidateTravelMinutes(travelMinutes, TimeOfDay, Title);
    }

    /// <summary>
    /// Cancels the reminder. Idempotent, so a duplicate request from a second client cannot
    /// fail merely because the first one already went through - the same reasoning as
    /// <see cref="TaskOccurrence.Skip"/>. Blocked once the reminder has been checked off
    /// (<see cref="CheckOff"/>): silently overwriting <see cref="ReminderStatus.CheckedOff"/> would
    /// erase the owner's own "I've handled this" without an explicit <see cref="Restore"/> -
    /// call <see cref="Restore"/> first.
    /// </summary>
    public void Cancel()
    {
        if (Status == ReminderStatus.CheckedOff)
        {
            throw new DomainException("A checked-off reminder cannot be cancelled - restore it first.");
        }

        Status = ReminderStatus.Cancelled;
    }

    /// <summary>
    /// Marks this reminder's own time as no longer needing a reminder - see
    /// <see cref="ReminderStatus.CheckedOff"/> for why this is about the TIME, not about a chore.
    /// Idempotent, same reasoning as <see cref="Cancel"/>: a duplicate request from a second
    /// client cannot fail merely because the first one already went through. Blocked on a
    /// cancelled reminder - an appointment that was called off entirely has nothing left to
    /// check off; <see cref="Restore"/> it first if that was a mistake.
    /// </summary>
    public void CheckOff()
    {
        if (Status == ReminderStatus.CheckedOff)
        {
            return;
        }

        if (Status == ReminderStatus.Cancelled)
        {
            throw new DomainException("A cancelled reminder cannot be checked off.");
        }

        Status = ReminderStatus.CheckedOff;
    }

    /// <summary>
    /// Takes back a cancellation or a check-off, returning the reminder to
    /// <see cref="ReminderStatus.Upcoming"/>. Throws <see cref="DomainException"/> if the
    /// reminder is already upcoming - there is nothing to restore. Unlike
    /// <see cref="TaskOccurrence.Reopen"/>, there is deliberately no time window here for either
    /// direction: <c>Reopen</c>'s 15 minutes exist because a completed occurrence is shared
    /// household history that nobody should be able to quietly rewrite days later. A reminder
    /// is private to its owner - nobody else is affected by restoring one, whether it had been
    /// cancelled or checked off, so a member who notices two days later that an appointment was
    /// cancelled, or checked off, by mistake should still get it back. How long "Undo" is
    /// offered in practice is a client-side decision, not a domain one.
    /// </summary>
    public void Restore()
    {
        if (Status == ReminderStatus.Upcoming)
        {
            throw new DomainException("Only a cancelled or checked-off reminder can be restored.");
        }

        Status = ReminderStatus.Upcoming;
    }

    private void EnsureUpcoming(string action)
    {
        if (Status != ReminderStatus.Upcoming)
        {
            throw new DomainException($"A reminder with status '{Status}' cannot be {action}.");
        }
    }

    private static string ValidateTitle(string title)
    {
        var trimmed = Guard.AgainstNullOrWhiteSpace(title, nameof(title));
        if (trimmed.Length > MaxTitleLength)
        {
            throw new ArgumentException(
                $"Title must be at most {MaxTitleLength} characters.", nameof(title));
        }

        return trimmed;
    }

    private static string? ValidateLocation(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return null;
        }

        var trimmed = location.Trim();
        if (trimmed.Length > MaxLocationLength)
        {
            throw new ArgumentException(
                $"Location must be at most {MaxLocationLength} characters.", nameof(location));
        }

        return trimmed;
    }

    /// <summary>
    /// <c>null</c> clears the travel time. Otherwise requires a time of day to count back from -
    /// see <see cref="SetTravelMinutes"/> - and a positive value up to <see cref="MaxTravelMinutes"/>.
    /// </summary>
    private static int? ValidateTravelMinutes(int? travelMinutes, TimeOnly? timeOfDay, string title)
    {
        if (travelMinutes is not { } minutes)
        {
            return null;
        }

        if (timeOfDay is null)
        {
            throw new DomainException(
                $"Reminder '{title}' has no time of day to count travel time against.");
        }

        Guard.AgainstNonPositive(minutes, nameof(travelMinutes));

        if (minutes > MaxTravelMinutes)
        {
            throw new ArgumentOutOfRangeException(nameof(travelMinutes), minutes,
                $"Travel minutes must be at most {MaxTravelMinutes}.");
        }

        return minutes;
    }
}
