using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

internal sealed class InMemoryTaskAssignmentRepository : ITaskAssignmentRepository
{
    private readonly List<TaskAssignment> _assignments = [];

    internal int Count => _assignments.Count;

    public Task AddAsync(TaskAssignment assignment, CancellationToken cancellationToken)
    {
        _assignments.Add(assignment);
        return Task.CompletedTask;
    }

    public Task<TaskAssignment?> FindMostRecentAsync(
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_assignments
            .Where(a => a.HouseholdId == householdId && a.TaskDefinitionId == taskDefinitionId)
            .OrderByDescending(a => a.ScheduledDate)
            .FirstOrDefault());
}
