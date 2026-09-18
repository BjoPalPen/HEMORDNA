namespace Hemordna.Domain.Tasks;

/// <summary>Bounds calendar input using an explicitly supplied reference date.</summary>
public static class SchedulingDate
{
    // Five years accommodates annual household planning without accepting arbitrary centuries.
    public const int MaxYearsAhead = 5;
    public static readonly DateOnly MinSupportedDate = DateOnly.MinValue.AddDays(60);
    public static readonly DateOnly MaxSupportedDate = new(9998, 12, 31);

    public static void Validate(DateOnly date, DateOnly referenceDate)
    {
        ValidateCalendar(date);
        var maximum = referenceDate.Year <= MaxSupportedDate.Year - MaxYearsAhead
            ? referenceDate.AddYears(MaxYearsAhead)
            : MaxSupportedDate;

        if (date > maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date,
                $"Scheduling dates must be within the supported calendar and at most {MaxYearsAhead} years ahead.");
        }
    }

    public static void ValidateCalendar(DateOnly date)
    {
        if (date < MinSupportedDate || date > MaxSupportedDate)
        {
            throw new ArgumentOutOfRangeException(nameof(date), date, "Date is outside the supported scheduling calendar.");
        }
    }
}