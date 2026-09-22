using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Reminders;

/// <summary>
/// A member's own appointment they want to be reminded of - a doctor's visit, a meeting, a
/// dentist's slot. Private to the member who created it and deliberately not household work:
/// see docs/PRODUCT.md §11. A reminder never counts toward anyone's time budget, is never
/// "overdue", never rotates and never earns time credit - which is exactly why this is its own
/// entity rather than a <see cref="TaskDefinition"/>/<see cref="TaskOccurrence"/> pair.
/// </summary>
public sealed class Reminder
{
    /// <summary>Long enough for any real appointment title, short enough to stay readable in a notification.</summary>
    public const int MaxTitleLength = 100;

    /// <summary>Free text for where the appointment is - a clinic name, an address fragment.</summary>
    public const int MaxLocationLength = 200;

    private Reminder(
        Guid id,
        Guid householdId,
        Guid memberId,
        string title,
        string? location,
        DateOnly date,
        TimeOnly? timeOfDay,
        DateTimeOffset createdAt)
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

    public ReminderStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

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
        DateTimeOffset createdAt)
    {
        Guard.AgainstEmpty(householdId, nameof(householdId));
        Guard.AgainstEmpty(memberId, nameof(memberId));
        SchedulingDate.Validate(date, DateOnly.FromDateTime(createdAt.UtcDateTime));

        return new Reminder(
            Guid.NewGuid(),
            householdId,
            memberId,
            ValidateTitle(title),
            ValidateLocation(location),
            date,
            timeOfDay,
            createdAt);
    }

    /// <summary>Changes the title. Only an upcoming reminder can be changed.</summary>
    public void ChangeTitle(string title)
    {
        EnsureNotCancelled("changed");

        Title = ValidateTitle(title);
    }

    /// <summary>
    /// Changes the location, or clears it with a blank value. Only an upcoming reminder can be
    /// changed.
    /// </summary>
    public void ChangeLocation(string? location)
    {
        EnsureNotCancelled("changed");

        Location = ValidateLocation(location);
    }

    /// <summary>
    /// Moves this reminder to a new date and time of day. Only an upcoming reminder can be
    /// moved. Unlike <see cref="Create"/>, no reference date is available here, so the date is
    /// only checked against the supported calendar bounds - the same choice
    /// <see cref="TaskOccurrence.DeferTo"/> and <see cref="TaskOccurrence.ReanchorTo"/> already
    /// make for the same reason.
    /// </summary>
    public void MoveTo(DateOnly date, TimeOnly? timeOfDay)
    {
        EnsureNotCancelled("moved");
        SchedulingDate.ValidateCalendar(date);

        Date = date;
        TimeOfDay = timeOfDay;
    }

    /// <summary>
    /// Cancels the reminder. Idempotent, so a duplicate request from a second client cannot
    /// fail merely because the first one already went through - the same reasoning as
    /// <see cref="TaskOccurrence.Skip"/>.
    /// </summary>
    public void Cancel() => Status = ReminderStatus.Cancelled;

    private void EnsureNotCancelled(string action)
    {
        if (Status == ReminderStatus.Cancelled)
        {
            throw new DomainException($"A cancelled reminder cannot be {action}.");
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
}
