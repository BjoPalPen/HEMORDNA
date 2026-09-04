using Hemordna.Domain.Common;

namespace Hemordna.Domain.Tasks;

public enum RecurrenceFrequency
{
    Daily,
    Weekly,
    Monthly
}

/// <summary>Which occurrence of a weekday within a month a monthly-by-weekday rule targets.</summary>
public enum WeekOfMonth
{
    First,
    Second,
    Third,
    Fourth,
    Last
}

/// <summary>
/// How a <see cref="TaskDefinition"/> repeats: daily, weekly on a given weekday, monthly on the
/// same day-of-month as <see cref="StartDate"/>, or monthly on a given occurrence of a weekday
/// ("the third Tuesday"). Immutable value object.
/// </summary>
/// <remarks>
/// Deliberately just forward stepping from <see cref="StartDate"/> rather than closed-form date
/// arithmetic - month-end and "nth weekday" edge cases are easy to get wrong, and a household's
/// recurrence horizon is small enough that a bounded loop is simpler and just as fast in
/// practice. <see cref="MaxSteps"/> exists only as a defensive bound against a future bug, not
/// as an expected code path.
/// </remarks>
public sealed class RecurrenceRule : IEquatable<RecurrenceRule>
{
    private const int MaxSteps = 20_000;

    private RecurrenceRule(
        RecurrenceFrequency frequency,
        int interval,
        DateOnly startDate,
        DayOfWeek? weekday,
        WeekOfMonth? weekOfMonth)
    {
        Frequency = frequency;
        Interval = interval;
        StartDate = startDate;
        Weekday = weekday;
        MonthlyWeek = weekOfMonth;
    }

    public RecurrenceFrequency Frequency { get; }

    /// <summary>Every how many days/weeks/months, depending on <see cref="Frequency"/>. Always &gt;= 1.</summary>
    public int Interval { get; }

    /// <summary>The date the pattern is anchored to. For Weekly, normalised to the first matching weekday.</summary>
    public DateOnly StartDate { get; }

    /// <summary>Set for Weekly, and for Monthly when <see cref="MonthlyWeek"/> is set. Otherwise null.</summary>
    public DayOfWeek? Weekday { get; }

    /// <summary>Set only for a "nth weekday of month" rule. Null means "same day-of-month as StartDate".</summary>
    public WeekOfMonth? MonthlyWeek { get; }

    public static RecurrenceRule Daily(DateOnly startDate, int everyNDays = 1)
        => new(RecurrenceFrequency.Daily, Guard.AgainstNonPositive(everyNDays, nameof(everyNDays)), startDate, null, null);

    public static RecurrenceRule Weekly(DateOnly startDate, DayOfWeek weekday, int everyNWeeks = 1)
    {
        Guard.AgainstNonPositive(everyNWeeks, nameof(everyNWeeks));
        RequireValidWeekday(weekday);

        var anchor = startDate;
        while (anchor.DayOfWeek != weekday)
        {
            anchor = anchor.AddDays(1);
        }

        return new RecurrenceRule(RecurrenceFrequency.Weekly, everyNWeeks, anchor, weekday, null);
    }

    /// <summary>Recurs on the same day-of-month as <paramref name="startDate"/> (clamped at short months).</summary>
    public static RecurrenceRule Monthly(DateOnly startDate, int everyNMonths = 1)
        => new(RecurrenceFrequency.Monthly, Guard.AgainstNonPositive(everyNMonths, nameof(everyNMonths)), startDate, null, null);

    /// <summary>Recurs on e.g. "the third Tuesday" of every Nth month.</summary>
    public static RecurrenceRule MonthlyOnWeekday(
        DateOnly startDate, WeekOfMonth which, DayOfWeek weekday, int everyNMonths = 1)
    {
        Guard.AgainstNonPositive(everyNMonths, nameof(everyNMonths));
        RequireValidWeekday(weekday);

        if (!Enum.IsDefined(which))
        {
            throw new ArgumentOutOfRangeException(nameof(which), which, "Not a valid week-of-month.");
        }

        var anchorMonthStart = new DateOnly(startDate.Year, startDate.Month, 1);
        return new RecurrenceRule(RecurrenceFrequency.Monthly, everyNMonths, anchorMonthStart, weekday, which);
    }

    /// <summary>The earliest date on or after <paramref name="onOrAfter"/> that the rule falls on.</summary>
    public DateOnly NextOnOrAfter(DateOnly onOrAfter)
    {
        var candidate = MonthlyWeek is { } which ? NthWeekdayOfMonth(StartDate.Year, StartDate.Month, which, Weekday!.Value) : StartDate;

        var steps = 0;
        while (candidate < onOrAfter)
        {
            candidate = Advance(candidate);

            if (++steps > MaxSteps)
            {
                throw new InvalidOperationException(
                    "RecurrenceRule.NextOnOrAfter did not converge - this indicates a bug in the rule, not normal use.");
            }
        }

        return candidate;
    }

    private DateOnly Advance(DateOnly current) => Frequency switch
    {
        RecurrenceFrequency.Daily => current.AddDays(Interval),
        RecurrenceFrequency.Weekly => current.AddDays(7 * Interval),
        RecurrenceFrequency.Monthly when MonthlyWeek is { } which
            => NextMonthlyWeekday(current, which),
        RecurrenceFrequency.Monthly => current.AddMonths(Interval),
        _ => throw new InvalidOperationException($"Unhandled frequency '{Frequency}'.")
    };

    private DateOnly NextMonthlyWeekday(DateOnly current, WeekOfMonth which)
    {
        var nextMonthStart = new DateOnly(current.Year, current.Month, 1).AddMonths(Interval);
        return NthWeekdayOfMonth(nextMonthStart.Year, nextMonthStart.Month, which, Weekday!.Value);
    }

    private static DateOnly NthWeekdayOfMonth(int year, int month, WeekOfMonth which, DayOfWeek weekday)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);

        if (which == WeekOfMonth.Last)
        {
            var last = new DateOnly(year, month, daysInMonth);
            while (last.DayOfWeek != weekday)
            {
                last = last.AddDays(-1);
            }

            return last;
        }

        var occurrenceIndex = which switch
        {
            WeekOfMonth.First => 0,
            WeekOfMonth.Second => 1,
            WeekOfMonth.Third => 2,
            WeekOfMonth.Fourth => 3,
            _ => throw new ArgumentOutOfRangeException(nameof(which), which, "Not a valid week-of-month.")
        };

        var first = new DateOnly(year, month, 1);
        while (first.DayOfWeek != weekday)
        {
            first = first.AddDays(1);
        }

        var result = first.AddDays(7 * occurrenceIndex);

        if (result.Month != month)
        {
            throw new ArgumentException(
                $"'{month}/{year}' does not have a {which} {weekday}.", nameof(which));
        }

        return result;
    }

    private static void RequireValidWeekday(DayOfWeek weekday)
    {
        if (!Enum.IsDefined(weekday))
        {
            throw new ArgumentOutOfRangeException(nameof(weekday), weekday, "Not a valid weekday.");
        }
    }

    public bool Equals(RecurrenceRule? other)
        => other is not null
            && Frequency == other.Frequency
            && Interval == other.Interval
            && StartDate == other.StartDate
            && Weekday == other.Weekday
            && MonthlyWeek == other.MonthlyWeek;

    public override bool Equals(object? obj) => Equals(obj as RecurrenceRule);

    public override int GetHashCode() => HashCode.Combine(Frequency, Interval, StartDate, Weekday, MonthlyWeek);
}
