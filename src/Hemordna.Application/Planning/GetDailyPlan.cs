using Hemordna.Application.Households;

namespace Hemordna.Application.Planning;

/// <summary>
/// Answers "what is reasonable for this person to do on this date": resolves how much time
/// they have, gathers their outstanding work and hands both to <see cref="DailyPlanner"/>.
/// </summary>
public sealed class GetDailyPlan
{
    private readonly IHouseholdRepository _households;
    private readonly IMemberAvailabilityRepository _availabilities;
    private readonly IPlanCandidateQuery _candidates;
    private readonly DailyPlanner _planner;

    public GetDailyPlan(
        IHouseholdRepository households,
        IMemberAvailabilityRepository availabilities,
        IPlanCandidateQuery candidates,
        DailyPlanner planner)
    {
        _households = households;
        _availabilities = availabilities;
        _candidates = candidates;
        _planner = planner;
    }

    /// <summary>
    /// Builds the member's day, or returns <c>null</c> when the household has no such member.
    /// The date is supplied by the caller - this use case reads no clock either.
    /// </summary>
    public async Task<MemberDay?> HandleAsync(
        Guid householdId,
        Guid memberId,
        DateOnly date,
        CancellationToken cancellationToken)
    {
        var household = await _households.FindByIdAsync(householdId, cancellationToken);

        var member = household?.Members.FirstOrDefault(m => m.Id == memberId);

        if (member is null)
        {
            return null;
        }

        // The one-off override for this date if the member set one, otherwise their normal
        // weekly budget. The weekly budget is never modified by a single day's change.
        var availabilityOverride =
            await _availabilities.FindAsync(householdId, memberId, date, cancellationToken);

        var availableMinutes = member.AvailableMinutesOn(date, availabilityOverride);

        var candidates =
            await _candidates.FindOutstandingForMemberAsync(householdId, memberId, date, cancellationToken);

        var completed =
            await _candidates.FindCompletedForMemberOnAsync(householdId, memberId, date, cancellationToken);

        var plan = _planner.Plan(new DailyPlanRequest(memberId, date, availableMinutes, candidates));

        return new MemberDay(plan, completed);
    }
}
