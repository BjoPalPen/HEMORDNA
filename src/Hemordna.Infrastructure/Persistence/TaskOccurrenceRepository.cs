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
        CancellationToken cancellationToken)
        => _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.TaskDefinitionId == taskDefinitionId)
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
}
