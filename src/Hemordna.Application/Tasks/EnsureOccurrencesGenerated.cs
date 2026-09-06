using Hemordna.Application.Households;
using Hemordna.Domain.Households;
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

        // A mutable snapshot, updated in place as each rotating pick is made below - see
        // RotationPicker's remarks on why this must reflect the whole batch, not just what is
        // already in the database when this method started.
        var assignedMinutesByMember = new Dictionary<Guid, int>(
            await _assignments.GetAssignedMinutesByMemberAsync(householdId, cancellationToken));

        foreach (var definition in definitions)
        {
            if (!definition.IsActive)
            {
                continue;
            }

            if (definition.Recurrence is { } recurrence)
            {
                await GenerateOnScheduleAsync(
                    household, definition, recurrence, today, assignedMinutesByMember, cancellationToken);
            }
            else if (definition.StaleAfterDays is { } staleAfterDays)
            {
                await GenerateIfStaleAsync(
                    household, definition, staleAfterDays, today, assignedMinutesByMember, cancellationToken);
            }
        }
    }

    private async Task GenerateOnScheduleAsync(
        Household household,
        TaskDefinition definition,
        RecurrenceRule recurrence,
        DateOnly today,
        Dictionary<Guid, int> assignedMinutesByMember,
        CancellationToken cancellationToken)
    {
        var lastDate = await _occurrences.FindMostRecentOriginalDateAsync(
            household.Id, definition.Id, cancellationToken);

        var next = recurrence.NextOnOrAfter(lastDate?.AddDays(1) ?? recurrence.StartDate);
        var generated = 0;

        while (next <= today && generated < MaxCatchUpPerDefinition)
        {
            await ScheduleGeneratedOccurrenceAsync(
                household, definition, next, assignedMinutesByMember, cancellationToken);
            generated++;
            next = recurrence.NextOnOrAfter(next.AddDays(1));
        }
    }

    /// <summary>
    /// Unlike calendar recurrence, "as needed" has no missed slots to catch up on - it only
    /// ever asks "is it due right now?", so at most one occurrence is generated per call.
    /// </summary>
    private async Task GenerateIfStaleAsync(
        Household household,
        TaskDefinition definition,
        int staleAfterDays,
        DateOnly today,
        Dictionary<Guid, int> assignedMinutesByMember,
        CancellationToken cancellationToken)
    {
        if (await _occurrences.HasOutstandingAsync(household.Id, definition.Id, cancellationToken))
        {
            return;
        }

        var lastCompletedAt = await _occurrences.FindMostRecentCompletedAtAsync(
            household.Id, definition.Id, cancellationToken);

        var since = lastCompletedAt is { } completedAt
            ? DateOnly.FromDateTime(completedAt.UtcDateTime)
            : DateOnly.FromDateTime(definition.CreatedAt.UtcDateTime);

        if (since.AddDays(staleAfterDays) > today)
        {
            return;
        }

        await ScheduleGeneratedOccurrenceAsync(household, definition, today, assignedMinutesByMember, cancellationToken);
    }

    private async Task ScheduleGeneratedOccurrenceAsync(
        Household household,
        TaskDefinition definition,
        DateOnly date,
        Dictionary<Guid, int> assignedMinutesByMember,
        CancellationToken cancellationToken)
    {
        var occurrence = definition.ScheduleFor(date, _timeProvider.GetUtcNow());
        Guid? memberId = null;

        if (definition.HasRotatingResponsibility)
        {
            memberId = RotationPicker.PickNext(household, definition, assignedMinutesByMember);

            if (memberId is { } rotatingMemberId)
            {
                await _assignments.AddAsync(
                    TaskAssignment.Create(
                        household.Id, definition.Id, rotatingMemberId, date, _timeProvider.GetUtcNow(),
                        definition.EstimatedMinutes),
                    cancellationToken);

                assignedMinutesByMember[rotatingMemberId] =
                    assignedMinutesByMember.GetValueOrDefault(rotatingMemberId) + definition.EstimatedMinutes;
            }
        }

        if (memberId is { } finalMemberId)
        {
            occurrence.AssignTo(finalMemberId);
        }

        await _occurrences.AddAsync(occurrence, cancellationToken);
    }
}
