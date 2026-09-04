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
}
