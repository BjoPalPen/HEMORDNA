using Hemordna.Application.Tasks;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tests.Tasks;

internal sealed class InMemoryTaskOccurrenceRepository : ITaskOccurrenceRepository
{
    private readonly List<TaskOccurrence> _occurrences = [];

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
        Guid householdId, Guid taskDefinitionId, CancellationToken cancellationToken)
        => Task.FromResult(_occurrences
            .Where(o => o.HouseholdId == householdId && o.TaskDefinitionId == taskDefinitionId)
            .Select(o => (DateOnly?)o.OriginalScheduledDate)
            .OrderDescending()
            .FirstOrDefault());
}
