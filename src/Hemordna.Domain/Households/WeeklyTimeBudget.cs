using Hemordna.Domain.Common;

namespace Hemordna.Domain.Households;

/// <summary>
/// A household member's normal available minutes per weekday. Immutable value object.
/// A day set to zero minutes is a valid, explicit "no time this weekday".
/// </summary>
public sealed class WeeklyTimeBudget : IEquatable<WeeklyTimeBudget>
{
    private const int DaysPerWeek = 7;

    // Indexed by (int)DayOfWeek, i.e. Sunday = 0 .. Saturday = 6.
    private readonly int[] _minutesPerDay;

    private WeeklyTimeBudget(int[] minutesPerDay) => _minutesPerDay = minutesPerDay;

    /// <summary>A budget with no time allocated on any weekday.</summary>
    public static WeeklyTimeBudget Empty { get; } = new(new int[DaysPerWeek]);

    /// <summary>The same number of minutes every weekday.</summary>
    public static WeeklyTimeBudget Uniform(int minutesPerDay)
    {
        Guard.AgainstNegative(minutesPerDay, nameof(minutesPerDay));

        var days = new int[DaysPerWeek];
        Array.Fill(days, minutesPerDay);
        return new WeeklyTimeBudget(days);
    }

    /// <summary>
    /// Builds a budget from explicit weekdays. Weekdays not present default to zero minutes.
    /// </summary>
    public static WeeklyTimeBudget Create(IReadOnlyDictionary<DayOfWeek, int> minutesPerDay)
    {
        ArgumentNullException.ThrowIfNull(minutesPerDay);

        var days = new int[DaysPerWeek];
        foreach (var (day, minutes) in minutesPerDay)
        {
            if (!Enum.IsDefined(day))
            {
                throw new ArgumentException($"'{day}' is not a valid weekday.", nameof(minutesPerDay));
            }

            days[(int)day] = Guard.AgainstNegative(minutes, nameof(minutesPerDay));
        }

        return new WeeklyTimeBudget(days);
    }

    /// <summary>Normal available minutes on the given weekday.</summary>
    public int MinutesFor(DayOfWeek day)
    {
        if (!Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Not a valid weekday.");
        }

        return _minutesPerDay[(int)day];
    }

    /// <summary>Returns a copy with a single weekday changed. The original is unaffected.</summary>
    public WeeklyTimeBudget WithDay(DayOfWeek day, int minutes)
    {
        if (!Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Not a valid weekday.");
        }

        Guard.AgainstNegative(minutes, nameof(minutes));

        var days = (int[])_minutesPerDay.Clone();
        days[(int)day] = minutes;
        return new WeeklyTimeBudget(days);
    }

    /// <summary>Total normal minutes across the week.</summary>
    public int TotalWeeklyMinutes => _minutesPerDay.Sum();

    public bool Equals(WeeklyTimeBudget? other)
        => other is not null && _minutesPerDay.AsSpan().SequenceEqual(other._minutesPerDay);

    public override bool Equals(object? obj) => Equals(obj as WeeklyTimeBudget);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var minutes in _minutesPerDay)
        {
            hash.Add(minutes);
        }

        return hash.ToHashCode();
    }
}
