using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class TaskDefinitionTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);
    private static readonly Guid HouseholdId = Guid.NewGuid();

    private static TaskDefinition CreateDefinition(int estimatedMinutes = 10)
        => TaskDefinition.Create(HouseholdId, "Dammsug vardagsrum", estimatedMinutes, CreatedAt);

    [Fact]
    public void A_new_definition_defaults_to_normal_priority_and_is_deferrable()
    {
        var definition = CreateDefinition();

        Assert.Equal(TaskPriority.Normal, definition.Priority);
        Assert.True(definition.CanBeDeferred);
        Assert.True(definition.IsActive);
        Assert.Equal(HouseholdId, definition.HouseholdId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void An_estimate_must_be_greater_than_zero(int estimatedMinutes)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(estimatedMinutes));

    [Fact]
    public void ChangeEstimatedMinutes_rejects_a_non_positive_estimate()
    {
        var definition = CreateDefinition();

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ChangeEstimatedMinutes(0));
        Assert.Equal(10, definition.EstimatedMinutes);
    }

    [Fact]
    public void ScheduleFor_snapshots_the_planning_relevant_fields()
    {
        var definition = CreateDefinition(25);
        definition.ChangePriority(TaskPriority.High);
        definition.SetCanBeDeferred(false);

        var occurrence = definition.ScheduleFor(Friday, CreatedAt);

        Assert.Equal(25, occurrence.EstimatedMinutes);
        Assert.Equal(TaskPriority.High, occurrence.Priority);
        Assert.False(occurrence.CanBeDeferred);
        Assert.Equal(definition.Id, occurrence.TaskDefinitionId);
        Assert.Equal(definition.HouseholdId, occurrence.HouseholdId);
        Assert.Equal(Friday, occurrence.ScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
    }

    [Fact]
    public void Editing_the_definition_does_not_rewrite_an_already_scheduled_occurrence()
    {
        var definition = CreateDefinition(25);
        var occurrence = definition.ScheduleFor(Friday, CreatedAt);

        definition.ChangeEstimatedMinutes(90);
        definition.ChangePriority(TaskPriority.Low);
        definition.SetCanBeDeferred(false);

        Assert.Equal(25, occurrence.EstimatedMinutes);
        Assert.Equal(TaskPriority.Normal, occurrence.Priority);
        Assert.True(occurrence.CanBeDeferred);
    }

    [Fact]
    public void ScheduleFor_assigns_the_default_responsible_member()
    {
        var definition = CreateDefinition();
        var annaId = Guid.NewGuid();
        definition.SetDefaultResponsibleMember(annaId);

        var occurrence = definition.ScheduleFor(Friday, CreatedAt);

        Assert.Equal(annaId, occurrence.AssignedMemberId);
    }

    [Fact]
    public void ScheduleFor_leaves_the_occurrence_unassigned_without_a_default_member()
    {
        var occurrence = CreateDefinition().ScheduleFor(Friday, CreatedAt);

        Assert.Null(occurrence.AssignedMemberId);
    }

    [Fact]
    public void An_inactive_definition_cannot_be_scheduled()
    {
        var definition = CreateDefinition();
        definition.Deactivate();

        Assert.Throws<DomainException>(() => definition.ScheduleFor(Friday, CreatedAt));
    }

    [Fact]
    public void SetRecurrence_can_be_set_and_cleared()
    {
        var definition = CreateDefinition();
        var recurrence = RecurrenceRule.Weekly(Friday, DayOfWeek.Friday);

        definition.SetRecurrence(recurrence);
        Assert.Equal(recurrence, definition.Recurrence);

        definition.SetRecurrence(null);
        Assert.Null(definition.Recurrence);
    }
}
