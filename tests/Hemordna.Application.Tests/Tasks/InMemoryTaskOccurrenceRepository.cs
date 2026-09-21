using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

internal sealed class InMemoryTaskOccurrenceRepository : ITaskOccurrenceRepository
{
    private readonly List<TaskOccurrence> _occurrences = [];

    /// <summary>Vilket rum varje uppgiftsdefinition hör till, och definitionen själv (för att
    /// kunna härleda VisitKind - se VisitKindClassifier). Förekomster bär inte själva någon
    /// AreaId - i produktion kommer den från en join mot TaskDefinitions - så ett test som vill
    /// pröva rumsregeln registrerar kopplingen här.</summary>
    private readonly Dictionary<Guid, (Guid AreaId, TaskDefinition Definition)> _areaByDefinition = [];

    internal void SeedArea(TaskDefinition definition, Guid areaId) => _areaByDefinition[definition.Id] = (areaId, definition);

    internal int UpdateCallCount { get; private set; }

    internal int AddCallCount { get; private set; }

    internal void Seed(TaskOccurrence occurrence) => _occurrences.Add(occurrence);

    public Task AddAsync(TaskOccurrence occurrence, CancellationToken cancellationToken)
    {
        AddCallCount++;
        _occurrences.Add(occurrence);
        return Task.CompletedTask;
    }

    public Task<TaskOccurrence?> FindByIdAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
        => Task.FromResult(_occurrences.FirstOrDefault(o =>
            o.HouseholdId == householdId && o.Id == occurrenceId));

    public Task UpdateAsync(TaskOccurrence occurrence, CancellationToken cancellationToken)
    {
        UpdateCallCount++;
        return Task.CompletedTask;
    }

    public Task<DateOnly?> FindMostRecentOriginalDateAsync(
        Guid householdId, Guid taskDefinitionId, DateOnly throughDate, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences
            .Where(o => o.HouseholdId == householdId && o.TaskDefinitionId == taskDefinitionId
                && o.OriginalScheduledDate <= throughDate
                && o.OriginalScheduledDate <= DateOnly.FromDateTime(o.CreatedAt.UtcDateTime))
            .Select(o => (DateOnly?)o.OriginalScheduledDate)
            .OrderDescending()
            .FirstOrDefault());

    internal Task<DateOnly?> LatestScheduledOriginalDateAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences
            .Where(o => o.HouseholdId == householdId && o.TaskDefinitionId == taskDefinitionId)
            .Select(o => (DateOnly?)o.OriginalScheduledDate)
            .OrderDescending().FirstOrDefault());

    public Task<DateTimeOffset?> FindMostRecentCompletedAtAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences
            .Where(o => o.HouseholdId == householdId && o.TaskDefinitionId == taskDefinitionId
                && o.Status == TaskOccurrenceStatus.Completed)
            .Select(o => o.CompletedAt)
            .OrderDescending()
            .FirstOrDefault());

    public Task<bool> HasOutstandingAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences.Any(o => o.HouseholdId == householdId
            && o.TaskDefinitionId == taskDefinitionId && o.Status == TaskOccurrenceStatus.Planned));

    public Task<bool> HasCoveredSlotOnDateAsync(
        Guid householdId, Guid taskDefinitionId, DateOnly date, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences.Any(o => o.HouseholdId == householdId
            && o.TaskDefinitionId == taskDefinitionId
            && (o.OriginalScheduledDate == date
                || (o.Status == TaskOccurrenceStatus.Planned && o.ScheduledDate == date))));

    public Task<IReadOnlyList<TaskOccurrence>> ListOutstandingOnOrBeforeAsync(
        Guid householdId, Guid taskDefinitionId, DateOnly onOrBefore, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TaskOccurrence>>([.. _occurrences.Where(o =>
            o.HouseholdId == householdId && o.TaskDefinitionId == taskDefinitionId
            && o.Status == TaskOccurrenceStatus.Planned && o.ScheduledDate <= onOrBefore)]);

    public Task<IReadOnlyList<TaskOccurrence>> ListOutstandingByHouseholdAsync(
        Guid householdId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TaskOccurrence>>([.. _occurrences.Where(o =>
            o.HouseholdId == householdId && o.Status == TaskOccurrenceStatus.Planned)]);

    public Task<IReadOnlyDictionary<(Guid AreaId, bool IsRoutine), IReadOnlyCollection<Guid>>> GetMemberIdsByAreaOnDateAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var byAreaAndIsRoutine = _occurrences
            .Where(o => o.HouseholdId == householdId
                && o.ScheduledDate == date
                && o.AssignedMemberId is not null
                && (o.Status == TaskOccurrenceStatus.Planned || o.Status == TaskOccurrenceStatus.Completed)
                && _areaByDefinition.ContainsKey(o.TaskDefinitionId))
            .Select(o => (
                Claim: _areaByDefinition[o.TaskDefinitionId],
                o.AssignedMemberId))
            .Select(row => (
                AreaId: row.Claim.AreaId,
                IsRoutine: VisitKindClassifier.Of(row.Claim.Definition) == VisitKind.Routine,
                MemberId: row.AssignedMemberId!.Value))
            .Where(row => !row.IsRoutine)
            .GroupBy(row => (row.AreaId, row.IsRoutine))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)[.. group.Select(row => row.MemberId).Distinct()]);

        return Task.FromResult<IReadOnlyDictionary<(Guid AreaId, bool IsRoutine), IReadOnlyCollection<Guid>>>(byAreaAndIsRoutine);
    }
}
