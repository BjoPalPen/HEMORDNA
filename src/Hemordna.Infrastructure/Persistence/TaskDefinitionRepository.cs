using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class TaskDefinitionRepository : ITaskDefinitionRepository
{
    private readonly HemordnaDbContext _dbContext;

    public TaskDefinitionRepository(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task AddAsync(TaskDefinition definition, CancellationToken cancellationToken)
    {
        await _dbContext.TaskDefinitions.AddAsync(definition, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<TaskDefinition?> FindByIdAsync(
        Guid householdId,
        Guid taskDefinitionId,
        CancellationToken cancellationToken)
        => _dbContext.TaskDefinitions
            .FirstOrDefaultAsync(
                definition => definition.HouseholdId == householdId && definition.Id == taskDefinitionId,
                cancellationToken);

    public async Task<IReadOnlyList<TaskDefinition>> ListByHouseholdAsync(
        Guid householdId,
        CancellationToken cancellationToken)
        => await _dbContext.TaskDefinitions
            .AsNoTracking()
            .Where(definition => definition.HouseholdId == householdId)
            .OrderBy(definition => definition.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<TaskDefinition>> ListActiveByAreaAsync(
        Guid householdId,
        Guid areaId,
        CancellationToken cancellationToken)
        => await _dbContext.TaskDefinitions
            .Where(definition =>
                definition.HouseholdId == householdId && definition.AreaId == areaId && definition.IsActive)
            .ToListAsync(cancellationToken);

    public Task UpdateAsync(TaskDefinition definition, CancellationToken cancellationToken)
        => _dbContext.SaveChangesAsync(cancellationToken);
}
