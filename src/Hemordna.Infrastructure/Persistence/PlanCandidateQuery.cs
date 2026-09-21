using System.Linq.Expressions;
using Hemordna.Application.Planning;
using Hemordna.Domain.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Hemordna.Infrastructure.Persistence;

internal sealed class PlanCandidateQuery : IPlanCandidateQuery
{
    private readonly HemordnaDbContext _dbContext;

    public PlanCandidateQuery(HemordnaDbContext dbContext) => _dbContext = dbContext;

    public Task<IReadOnlyList<PlanCandidate>> FindOutstandingForMemberAsync(
        Guid householdId,
        Guid memberId,
        DateOnly onOrBefore,
        CancellationToken cancellationToken)
        => QueryAsync(
            occurrence => occurrence.HouseholdId == householdId
                && occurrence.AssignedMemberId == memberId
                && occurrence.Status == TaskOccurrenceStatus.Planned
                && occurrence.ScheduledDate <= onOrBefore,
            cancellationToken);

    public Task<IReadOnlyList<PlanCandidate>> FindCompletedForMemberOnAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken)
        => QueryAsync(
            occurrence => occurrence.HouseholdId == householdId
                && occurrence.AssignedMemberId == memberId
                && occurrence.Status == TaskOccurrenceStatus.Completed
                && occurrence.ScheduledDate == date,
            cancellationToken);

    /// <summary>
    /// Read-only, so no change tracking. Every filter leads with HouseholdId so it matches
    /// the (HouseholdId, ScheduledDate, Status) index.
    /// </summary>
    private async Task<IReadOnlyList<PlanCandidate>> QueryAsync(
        Expression<Func<TaskOccurrence, bool>> filter,
        CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TaskOccurrences
            .AsNoTracking()
            .Where(filter)
            .Join(
                _dbContext.TaskDefinitions.AsNoTracking(),
                occurrence => occurrence.TaskDefinitionId,
                definition => definition.Id,
                (occurrence, definition) => new
                {
                    Occurrence = occurrence,
                    definition.Name,
                    definition.AreaId,
                    definition.Description,
                    definition.Recurrence,
                    definition.Effort
                })
            // Left join: not every task belongs to an area.
            .GroupJoin(
                _dbContext.Areas.AsNoTracking(),
                row => row.AreaId,
                area => area.Id,
                (row, areas) => new { row, areas })
            .SelectMany(
                joined => joined.areas.DefaultIfEmpty(),
                (joined, area) => new
                {
                    joined.row.Occurrence,
                    joined.row.Name,
                    joined.row.Description,
                    joined.row.Recurrence,
                    joined.row.Effort,
                    AreaName = area != null ? area.Name : null
                })
            .ToListAsync(cancellationToken);

        // VisitKindClassifier is not translatable to SQL (Recurrence is a JSON column behind a
        // value converter, see TaskOccurrenceRepository's own GetMemberIdsByAreaOnDateAsync)
        // - applied here, after the rows are materialized, same as there. "Rutiner först" (Björns
        // beslut) reuses this classification rather than a second "is this a routine" check.
        return [.. rows.Select(row => new PlanCandidate(
            row.Occurrence,
            row.Name,
            row.AreaName,
            row.Description,
            isRoutine: VisitKindClassifier.Of(row.Recurrence, row.Effort) == VisitKind.Routine,
            effort: row.Effort))];
    }
}
