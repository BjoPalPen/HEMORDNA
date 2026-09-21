using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class TaskOccurrenceRepository : ITaskOccurrenceRepository
{
    private readonly HemordnaDbContext _dbContext;

    public TaskOccurrenceRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(TaskOccurrence occurrence, CancellationToken cancellationToken)
    {
        await _dbContext.TaskOccurrences.AddAsync(occurrence, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TaskOccurrence?> FindByIdAsync(
        Guid householdId,
        Guid occurrenceId,
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .FirstOrDefaultAsync(
                occurrence => occurrence.HouseholdId == householdId && occurrence.Id == occurrenceId,
                cancellationToken);

    public Task UpdateAsync(TaskOccurrence occurrence, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);

    public Task<DateOnly?> FindMostRecentOriginalDateAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly throughDate,
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId
                && occurrence.OriginalScheduledDate <= throughDate
                && occurrence.OriginalScheduledDate <= DateOnly.FromDateTime(occurrence.CreatedAt.UtcDateTime))
            .OrderByDescending(occurrence => occurrence.OriginalScheduledDate)
            .Select(occurrence => (DateOnly?)occurrence.OriginalScheduledDate)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DateTimeOffset?> FindMostRecentCompletedAtAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId
                && occurrence.Status == TaskOccurrenceStatus.Completed)
            .OrderByDescending(occurrence => occurrence.CompletedAt)
            .Select(occurrence => occurrence.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<bool> HasOutstandingAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .AsNoTracking()
            .AnyAsync(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId
                && occurrence.Status == TaskOccurrenceStatus.Planned, cancellationToken);

    public Task<bool> HasCoveredSlotOnDateAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly date,
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .AsNoTracking()
            .AnyAsync(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId
                && (occurrence.OriginalScheduledDate == date
                    || (occurrence.Status == TaskOccurrenceStatus.Planned && occurrence.ScheduledDate == date)),
                cancellationToken);

    public async Task<IReadOnlyList<TaskOccurrence>> ListOutstandingOnOrBeforeAsync(
        Guid householdId,
        Guid taskDefinitionId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken)
        => await _dbContext.TaskOccurrences
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId
                && occurrence.Status == TaskOccurrenceStatus.Planned
                && occurrence.ScheduledDate <= onOrBefore)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskOccurrence>> ListOutstandingByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
        => await _dbContext.TaskOccurrences
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.Status == TaskOccurrenceStatus.Planned)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<(Guid AreaId, bool IsRoutine), IReadOnlyCollection<Guid>>> GetMemberIdsByAreaOnDateAsync(
        Guid householdId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        // Read-only, and the area lives on the definition, not the occurrence - hence the join.
        // Recurrence/Effort travel along too, so VisitKind can be derived once the rows are
        // materialized - VisitKindClassifier is not translatable to SQL (Recurrence is a JSON
        // column behind a value converter).
        var rows = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.ScheduledDate == date
                && occurrence.AssignedMemberId != null
                && (occurrence.Status == TaskOccurrenceStatus.Planned
                    || occurrence.Status == TaskOccurrenceStatus.Completed))
            .Join(
                _dbContext.TaskDefinitions.AsNoTracking(),
                occurrence => occurrence.TaskDefinitionId,
                definition => definition.Id,
                (occurrence, definition)
                    => new { definition.AreaId, occurrence.AssignedMemberId, definition.Recurrence, definition.Effort })
            .Where(row => row.AreaId != null)
            .ToListAsync(cancellationToken);

        // Samma härledningsmönster som PlanCandidateQuery's egen isRoutine-beräkning. Alternativ B
        // (Björns beslut): RegularClean och DeepClean är samma anspråk, samma besök - nyckeln
        // skiljer nu bara Routine från allt annat, se ITaskOccurrenceRepository.
        return rows
            .Select(row => (
                AreaId: row.AreaId!.Value,
                IsRoutine: VisitKindClassifier.Of(row.Recurrence, row.Effort) == VisitKind.Routine,
                MemberId: row.AssignedMemberId!.Value))
            .Where(row => !row.IsRoutine)
            .GroupBy(row => (row.AreaId, row.IsRoutine))
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyCollection<Guid>)[.. group.Select(row => row.MemberId).Distinct()]);
    }
}
