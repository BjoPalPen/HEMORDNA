using Hemordna.Application.Tasks;
using Hemordna.Domain.Areas;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Households;

/// <summary>
/// Pauses or resumes a single room's schedule - e.g. a renovation. Unlike
/// <see cref="PauseHousehold"/>/<see cref="PauseHouseholdMember"/>, which only ever affect what
/// gets generated next (see <see cref="Tasks.EnsureOccurrencesGenerated"/>), pausing a room also
/// clears what is already outstanding for it: nobody else can stand in for a room nobody can
/// use, unlike a single paused person whose work can just go to someone else or wait.
/// </summary>
public sealed class PauseArea
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;
    private readonly ITaskOccurrenceRepository _occurrences;

    public PauseArea(
        IHouseholdRepository households, ITaskDefinitionRepository definitions, ITaskOccurrenceRepository occurrences)
    {
        _households = households;
        _definitions = definitions;
        _occurrences = occurrences;
    }

    /// <summary>
    /// Pauses through and including <paramref name="until"/>, or resumes immediately when
    /// <paramref name="until"/> is <c>null</c>. Returns the area, or <c>null</c> when the
    /// household has no such area.
    /// </summary>
    public async Task<Area?> HandleAsync(Guid householdId, Guid areaId, DateOnly? until, CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);
        var area = household?.Areas.FirstOrDefault(a => a.Id == areaId);

        if (household is null || area is null)
        {
            return null;
        }

        if (until is { } date)
        {
            area.Pause(date);
            await ClearAlreadyScheduledAsync(householdId, areaId, date, cancellationToken);
        }
        else
        {
            // Resuming touches nothing already skipped - the room's recurrence simply starts
            // generating again from wherever EnsureOccurrencesGenerated's own catch-up cursor
            // already is, the same "no backlog on return" reasoning a household/member resume
            // already relies on.
            area.Resume();
        }

        await _households.UpdateAsync(household, cancellationToken);

        return area;
    }

    /// <summary>
    /// Drops every outstanding occurrence of this room's own tasks that falls within the pause
    /// window (<see cref="TaskOccurrence.ScheduledDate"/> on or before <paramref name="until"/>)
    /// - an occurrence already scheduled AFTER the pause lifts is left alone, it is not really
    /// "during" the pause. Skipped, not deleted (<see cref="TaskOccurrence.Skip"/>) - same as any
    /// other "not needed this time", so history still refers to a real occurrence.
    /// </summary>
    private async Task ClearAlreadyScheduledAsync(
        Guid householdId, Guid areaId, DateOnly until, CancellationToken cancellationToken)
    {
        var roomDefinitionIds = (await _definitions.ListActiveByAreaAsync(householdId, areaId, cancellationToken))
            .Select(definition => definition.Id)
            .ToHashSet();

        if (roomDefinitionIds.Count == 0)
        {
            return;
        }

        var outstanding = await _occurrences.ListOutstandingByHouseholdAsync(householdId, cancellationToken);
        var toSkip = outstanding
            .Where(occurrence => roomDefinitionIds.Contains(occurrence.TaskDefinitionId) && occurrence.ScheduledDate <= until)
            .ToList();

        foreach (var occurrence in toSkip)
        {
            occurrence.Skip();
        }

        if (toSkip.Count > 0)
        {
            // The current implementation flushes every pending change for the whole unit of
            // work in one call, regardless of which occurrence is passed - see
            // TaskOccurrenceRepository.UpdateAsync (same pattern RebalanceTaskAssignments and
            // RebalanceSchedule already rely on) - so this one call commits every skip above
            // atomically, or none of them.
            await _occurrences.UpdateAsync(toSkip[0], cancellationToken);
        }
    }
}
