using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Households;

/// <summary>
/// A household member's ceiling on how heavy a task they take on, per weekday - the heaviest
/// level they are willing to do that day. Immutable value object, same shape as
/// <see cref="WeeklyTimeBudget"/>. <see cref="Default"/> (Heavy every day) means "no
/// limitation" - see docs/ARCHITECTURE.md for why the migration that introduces this backfills
/// every existing member to it rather than a lower level.
/// </summary>
public sealed class WeeklyEffortCeiling : IEquatable<WeeklyEffortCeiling>
{
    private const int DaysPerWeek = 7;

    // Indexed by (int)DayOfWeek, i.e. Sunday = 0 .. Saturday = 6.
    private readonly TaskEffort[] _ceilingPerDay;

    private WeeklyEffortCeiling(TaskEffort[] ceilingPerDay) => _ceilingPerDay = ceilingPerDay;

    /// <summary>Heavy every weekday - no limitation on top of what the task itself already is.</summary>
    public static WeeklyEffortCeiling Default { get; } = Uniform(TaskEffort.Heavy);

    /// <summary>The same ceiling every weekday.</summary>
    public static WeeklyEffortCeiling Uniform(TaskEffort ceiling)
    {
        RequireValidEffort(ceiling, nameof(ceiling));

        var days = new TaskEffort[DaysPerWeek];
        Array.Fill(days, ceiling);
        return new WeeklyEffortCeiling(days);
    }

    /// <summary>Builds a ceiling from explicit weekdays. Weekdays not present default to Heavy.</summary>
    public static WeeklyEffortCeiling Create(IReadOnlyDictionary<DayOfWeek, TaskEffort> ceilingPerDay)
    {
        ArgumentNullException.ThrowIfNull(ceilingPerDay);

        var days = new TaskEffort[DaysPerWeek];
        Array.Fill(days, TaskEffort.Heavy);

        foreach (var (day, ceiling) in ceilingPerDay)
        {
            if (!Enum.IsDefined(day))
            {
                throw new ArgumentException($"'{day}' is not a valid weekday.", nameof(ceilingPerDay));
            }

            days[(int)day] = RequireValidEffort(ceiling, nameof(ceilingPerDay));
        }

        return new WeeklyEffortCeiling(days);
    }

    /// <summary>The heaviest level this member takes on, on the given weekday.</summary>
    public TaskEffort CeilingFor(DayOfWeek day)
    {
        if (!Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Not a valid weekday.");
        }

        return _ceilingPerDay[(int)day];
    }

    /// <summary>Whether a task of this effort is within the ceiling for the given weekday.</summary>
    public bool Allows(TaskEffort effort, DayOfWeek day) => effort <= CeilingFor(day);

    /// <summary>Returns a copy with a single weekday changed. The original is unaffected.</summary>
    public WeeklyEffortCeiling WithDay(DayOfWeek day, TaskEffort ceiling)
    {
        if (!Enum.IsDefined(day))
        {
            throw new ArgumentOutOfRangeException(nameof(day), day, "Not a valid weekday.");
        }

        RequireValidEffort(ceiling, nameof(ceiling));

        var days = (TaskEffort[])_ceilingPerDay.Clone();
        days[(int)day] = ceiling;
        return new WeeklyEffortCeiling(days);
    }

    private static TaskEffort RequireValidEffort(TaskEffort effort, string paramName)
    {
        if (!Enum.IsDefined(effort))
        {
            throw new ArgumentOutOfRangeException(paramName, effort, "Not a valid effort level.");
        }

        return effort;
    }

    public bool Equals(WeeklyEffortCeiling? other)
        => other is not null && _ceilingPerDay.AsSpan().SequenceEqual(other._ceilingPerDay);

    public override bool Equals(object? obj) => Equals(obj as WeeklyEffortCeiling);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var ceiling in _ceilingPerDay)
        {
            hash.Add(ceiling);
        }

        return hash.ToHashCode();
    }
}
