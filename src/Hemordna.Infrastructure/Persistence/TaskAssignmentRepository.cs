using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class TaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly HemordnaDbContext _dbContext;

    public TaskAssignmentRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(TaskAssignment assignment, CancellationToken cancellationToken)
    {
        await _dbContext.TaskAssignments.AddAsync(assignment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TaskAssignment?> FindMostRecentAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken)
        => _dbContext.TaskAssignments
            .AsNoTracking()
            .Where(assignment => assignment.HouseholdId == householdId
                && assignment.TaskDefinitionId == taskDefinitionId)
            .OrderByDescending(assignment => assignment.ScheduledDate)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, int>> GetAssignedMinutesByMemberAsync(
        Guid householdId,
        CancellationToken cancellationToken)
        => await _dbContext.TaskAssignments
            .AsNoTracking()
            .Where(assignment => assignment.HouseholdId == householdId)
            .GroupBy(assignment => assignment.MemberId)
            .Select(group => new { MemberId = group.Key, Minutes = group.Sum(assignment => assignment.EstimatedMinutes) })
            .ToDictionaryAsync(entry => entry.MemberId, entry => entry.Minutes, cancellationToken);
}
