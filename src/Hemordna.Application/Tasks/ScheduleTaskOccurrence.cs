using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Puts a task definition on the calendar for a specific date.
/// </summary>
/// <remarks>
/// Scheduling is explicit for now. Generating occurrences from a recurrence rule - and
/// deciding whether that happens on demand or in a scheduled job - is still an open
/// decision in docs/ARCHITECTURE.md, and an explicit endpoint keeps it open rather than
/// settling it by accident.
/// </remarks>
public sealed class ScheduleTaskOccurrence
{
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly TimeProvider _timeProvider;

    public ScheduleTaskOccurrence(
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        TimeProvider timeProvider)
    {
        _definitions = definitions;
        _occurrences = occurrences;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Schedules the task, or returns <c>null</c> when the household has no such definition.
    /// An inactive definition is rejected by the domain.
    /// </summary>
    public async Task<TaskOccurrence?> HandleAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly date,
        Guid? assignToMemberId,
        CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskDefinitionId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        var occurrence = definition.ScheduleFor(date, _timeProvider.GetUtcNow());

        if (assignToMemberId is { } memberId)
        {
            occurrence.AssignTo(memberId);
        }

        await _occurrences.AddAsync(occurrence, cancellationToken);

        return occurrence;
    }
}
