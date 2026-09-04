using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Generates the occurrences a recurring task definition owes, up to and including
/// <c>today</c>. This is the resolution of the open question in docs/ARCHITECTURE.md §10 -
/// generation happens on demand, called from <see cref="Planning.GetDailyPlan"/>, rather than
/// from a scheduled background job. Nothing else needs a hosted-service or queue
/// infrastructure yet, and "on demand" cannot silently generate work while no one is looking.
/// </summary>
public sealed class EnsureOccurrencesGenerated
{
    /// <summary>
    /// Safety bound on how many missed occurrences one definition can catch up on in a single
    /// call. A household that opens the app after months away should not get a runaway backlog.
    /// </summary>
    private const int MaxCatchUpPerDefinition = 366;

    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;
    private readonly ITaskAssignmentRepository _assignments;
    private readonly TimeProvider _timeProvider;

    public EnsureOccurrencesGenerated(
        IHouseholdRepository households,
        ITaskDefinitionRepository definitions,
        ITaskOccurrenceRepository occurrences,
        ITaskAssignmentRepository assignments,
        TimeProvider timeProvider)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
        _assignments = assignments;
        _timeProvider = timeProvider;
    }

    public async Task HandleAsync(Guid householdId, DateOnly today, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        if (household is null)
        {
            return;
        }

        var definitions = await _definitions.ListByHouseholdAsync(householdId, cancellationToken);

        foreach (var definition in definitions)
        {
            if (!definition.IsActive || definition.Recurrence is not { } recurrence)
            {
                continue;
            }

            var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(
                householdId, definition.Id, cancellationToken);

            var next = recurrence.NextOnOrAfter(lastDate?.AddDays(1) ?? recurrence.StartDate);
            var generated = 0;

            while (next <= today && generated < MaxCatchUpPerDefinition)
            {
                var occurrence = definition.ScheduleFor(next, _timeProvider.GetUtcNow());
                Guid? memberId = null;

                if (definition.HasRotatingResponsibility)
                {
                    var last = await _assignments.FindMostRecentAsync(
                        householdId, definition.Id, cancellationToken);
                    memberId = RotationPicker.PickNext(household, definition, last);

                    if (memberId is { } rotatingMemberId)
                    {
                        await _assignments.AddAsync(
                            TaskAssignment.Create(
                                householdId, definition.Id, rotatingMemberId, next, _timeProvider.GetUtcNow()),
                            cancellationToken);
                    }
                }

                if (memberId is { } finalMemberId)
                {
                    occurrence.AssignTo(finalMemberId);
                }

                await _occurrences.AddAsync(occurrence, cancellationToken);

                generated++;
                next = recurrence.NextOnOrAfter(next.AddDays(1));
            }
        }
    }
}
