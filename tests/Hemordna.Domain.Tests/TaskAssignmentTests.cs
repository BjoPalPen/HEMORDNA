using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class TaskAssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 2, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Monday = new(2026, 3, 2);

    [Fact]
    public void Create_captures_who_was_assigned_and_for_what_date()
    {
        var householdId = Guid.NewGuid();
        var taskDefinitionId = Guid.NewGuid();
        var memberId = Guid.NewGuid();

        var assignment = TaskAssignment.Create(householdId, taskDefinitionId, memberId, Monday, Now);

        Assert.Equal(householdId, assignment.HouseholdId);
        Assert.Equal(taskDefinitionId, assignment.TaskDefinitionId);
        Assert.Equal(memberId, assignment.MemberId);
        Assert.Equal(Monday, assignment.ScheduledDate);
        Assert.Equal(Now, assignment.CreatedAt);
        Assert.NotEqual(Guid.Empty, assignment.Id);
    }

    [Fact]
    public void Rejects_an_empty_member_id()
        => Assert.Throws<ArgumentException>(
            () => TaskAssignment.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, Monday, Now));
}
