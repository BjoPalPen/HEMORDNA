using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class PlanCandidateQuery : IPlanCandidateQuery
{
    private readonly HemordnaDbContext _dbContext;

    public PlanCandidateQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public async Task<IReadOnlyList<PlanCandidate>> FindOutstandingForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken)
    {
        // Read-only, so no change tracking. The filter matches the
        // (HouseholdId, ScheduledDate, Status) index.
        var rows = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(occurrence => occurrence.HouseholdId == householdId
                && occurrence.AssignedMemberId == memberId
                && occurrence.Status == TaskOccurrenceStatus.Planned
                && occurrence.ScheduledDate <= onOrBefore)
            .Join(
                _dbContext.TaskDefinitions.AsNoTracking(),
                occurrence => occurrence.TaskDefinitionId,
                definition => definition.Id,
                (occurrence, definition) => new { Occurrence = occurrence, definition.Name })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new PlanCandidate(row.Occurrence, row.Name))];
    }
}
