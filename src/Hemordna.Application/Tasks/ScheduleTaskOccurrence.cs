using Hemordna.Application.Households;
using Hemordna.Application.Realtime;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Puts a task definition on the calendar for a specific date.
/// </summary>
/// <remarks>
/// Manual scheduling. <see cref="EnsureOccurrencesGenerated"/> is the automatic counterpart
/// for tasks that have a <see cref="Domain.Tasks.RecurrenceRule"/> - this use case stays the
/// explicit path for a one-off or a household correcting the calendar by hand.
/// </remarks>
public sealed class ScheduleTaskOccurrence
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly IHouseholdNotifier _notifier;
    private readonly TimeProvider _timeProvider;

    public ScheduleTaskOccurrence(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        ITaskAssignmentRepository assignments,
        IHouseholdNotifier notifier,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _assignments = assignments;
        _notifier = notifier;
        _timeProvider = timeProvider;
    }

    /// <summary>
    /// Schedules the task, or returns <c>null</c> when the household has no such definition.
    /// An inactive definition is rejected by the domain.
    /// </summary>
    /// <param name="assignToMemberId">
    /// Who to assign it to. For a rotating task, leaving this <c>null</c> lets rotation decide;
    /// naming someone still records it as that person's turn, so rotation continues from them.
    /// </param>
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
        var memberId = assignToMemberId;

        if (definition.HasRotatingResponsibility)
        {
            if (memberId is null)
            {
                var household = await _households.FindByIdAsync(householdId, cancellationToken);

                if (household is not null)
                {
                    var assignedMinutesByMember = await _assignments.GetAssignedMinutesByMemberAsync(
                        householdId, cancellationToken);
                    memberId = RotationPicker.PickNext(household, definition, assignedMinutesByMember);
                }
            }

            if (memberId is { } rotatingMemberId)
            {
                await _assignments.AddAsync(
                    TaskAssignment.Create(
                        householdId, taskDefinitionId, rotatingMemberId, date, _timeProvider.GetUtcNow(),
                        definition.EstimatedMinutes),
                    cancellationToken);
            }
        }

        if (memberId is { } finalMemberId)
        {
            occurrence.AssignTo(finalMemberId);
        }

        await _occurrences.AddAsync(occurrence, cancellationToken);
        await _notifier.NotifyOccurrencesChangedAsync(householdId, cancellationToken);

        return occurrence;
    }
}
