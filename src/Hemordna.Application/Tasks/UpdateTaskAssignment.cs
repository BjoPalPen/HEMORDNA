using Hemordna.Application.Households;
using Hemordna.Domain.Tasks;

namespace Hemordna.Application.Tasks;

/// <summary>
/// Changes who normally owns an existing task - a specific member, or rotating between
/// everyone. Today this can only be set once, at creation (or, for a bedroom, when the room
/// itself is set up - see RoomTemplateTask in the client).
/// </summary>
public sealed class UpdateTaskAssignment
{
    private readonly IHouseholdRepository _households;
    private readonly ITaskDefinitionRepository _definitions;

    public UpdateTaskAssignment(IHouseholdRepository households, ITaskDefinitionRepository definitions)
    {
        _households = households;
        _definitions = definitions;
    }

    /// <summary>
    /// Updates the definition, or returns <c>null</c> when the household has no such task. A
    /// <c>null</c> <paramref name="memberId"/> means "rotates between everyone"; a specific
    /// member means fixed, non-rotating responsibility - the same either/or the rest of the app
    /// already uses (see the client's bedroom-owner flow).
    /// </summary>
    public async Task<TaskDefinition?> HandleAsync(
        Guid householdId, Guid taskId, Guid? memberId, CancellationToken cancellationToken)
    {
        var definition = await _definitions.FindByIdAsync(householdId, taskId, cancellationToken);

        if (definition is null)
        {
            return null;
        }

        if (memberId is { } id)
        {
            var household = await _households.FindByIdAsync(householdId, cancellationToken);

            if (household is null || household.Members.All(member => member.Id != id))
            {
                throw new ArgumentException("The member does not belong to this household.", nameof(memberId));
            }
        }

        definition.SetDefaultResponsibleMember(memberId);
        definition.SetRotatingResponsibility(memberId is null);

        await _definitions.UpdateAsync(definition, cancellationToken);

        return definition;
    }
}
