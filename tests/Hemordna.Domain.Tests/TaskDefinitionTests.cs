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

    [Fact]
    public void An_estimate_of_zero_is_a_deliberate_choice_not_an_error()
        => Assert.Equal(0, CreateDefinition(0).EstimatedMinutes);

    [Fact]
    public void An_estimate_must_not_be_negative()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition(-15));

    [Fact]
    public void ChangeEstimatedMinutes_accepts_zero()
    {
        var definition = CreateDefinition();

        definition.ChangeEstimatedMinutes(0);

        Assert.Equal(0, definition.EstimatedMinutes);
    }

    [Fact]
    public void ChangeEstimatedMinutes_rejects_a_negative_estimate()
    {
        var definition = CreateDefinition();

        Assert.Throws<ArgumentOutOfRangeException>(() => definition.ChangeEstimatedMinutes(-15));
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

    [Fact]
    public void SetStaleAfterDays_can_be_set_and_cleared()
    {
        var definition = CreateDefinition();

        definition.SetStaleAfterDays(21);
        Assert.Equal(21, definition.StaleAfterDays);

        definition.SetStaleAfterDays(null);
        Assert.Null(definition.StaleAfterDays);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void SetStaleAfterDays_rejects_a_non_positive_interval(int days)
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition().SetStaleAfterDays(days));

    [Fact]
    public void A_new_definition_defaults_to_medium_effort()
        => Assert.Equal(TaskEffort.Medium, CreateDefinition().Effort);

    [Fact]
    public void ChangeEffort_changes_the_level()
    {
        var definition = CreateDefinition();

        definition.ChangeEffort(TaskEffort.Heavy);

        Assert.Equal(TaskEffort.Heavy, definition.Effort);
    }

    [Fact]
    public void ChangeEffort_rejects_an_undefined_level()
        => Assert.Throws<ArgumentOutOfRangeException>(() => CreateDefinition().ChangeEffort((TaskEffort)99));

    // "Alltid på en viss veckodag" - Björns krav: PreferredWeekday is a requirement, only
    // meaningful for a Weekly or Monthly recurrence. See docs/ARCHITECTURE.md "Beslut: Alltid på
    // en viss veckodag".

    [Fact]
    public void SetPreferredWeekday_is_accepted_for_a_weekly_task()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));

        definition.SetPreferredWeekday(DayOfWeek.Tuesday);

        Assert.Equal(DayOfWeek.Tuesday, definition.PreferredWeekday);
    }

    [Fact]
    public void SetPreferredWeekday_is_accepted_for_a_monthly_task()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Monthly(Friday));

        definition.SetPreferredWeekday(DayOfWeek.Wednesday);

        Assert.Equal(DayOfWeek.Wednesday, definition.PreferredWeekday);
    }

    [Fact]
    public void SetPreferredWeekday_is_rejected_for_a_daily_task()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Daily(Friday));

        Assert.Throws<DomainException>(() => definition.SetPreferredWeekday(DayOfWeek.Tuesday));
        Assert.Null(definition.PreferredWeekday);
    }

    [Fact]
    public void SetPreferredWeekday_is_rejected_for_an_as_needed_task()
    {
        var definition = CreateDefinition();
        definition.SetStaleAfterDays(14);

        Assert.Throws<DomainException>(() => definition.SetPreferredWeekday(DayOfWeek.Tuesday));
        Assert.Null(definition.PreferredWeekday);
    }

    [Fact]
    public void SetPreferredWeekday_is_rejected_when_there_is_no_recurrence_at_all()
        => Assert.Throws<DomainException>(() => CreateDefinition().SetPreferredWeekday(DayOfWeek.Tuesday));

    [Fact]
    public void Clearing_the_preferred_weekday_is_always_allowed()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));
        definition.SetPreferredWeekday(DayOfWeek.Tuesday);

        definition.SetPreferredWeekday(null);

        Assert.Null(definition.PreferredWeekday);
    }

    [Fact]
    public void Switching_recurrence_to_daily_clears_an_existing_preferred_weekday()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));
        definition.SetPreferredWeekday(DayOfWeek.Friday);

        definition.SetRecurrence(RecurrenceRule.Daily(Friday));

        Assert.Null(definition.PreferredWeekday);
    }

    [Fact]
    public void Clearing_recurrence_entirely_clears_an_existing_preferred_weekday()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));
        definition.SetPreferredWeekday(DayOfWeek.Friday);

        definition.SetRecurrence(null);

        Assert.Null(definition.PreferredWeekday);
    }

    /// <summary>A raw SetRecurrence call (e.g. via "Upprepning" in the UI, which edits frequency
    /// directly) that moves the task to a DIFFERENT weekday must not leave a stale lock pointing
    /// at the day it no longer actually sits on - see SetRecurrence's own remarks. Locking to a
    /// new day on purpose goes through SetTaskPreferredWeekday instead, which re-anchors
    /// Recurrence and PreferredWeekday together in one step.</summary>
    [Fact]
    public void Switching_recurrence_to_a_different_weekly_day_clears_a_stale_preferred_weekday()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));
        definition.SetPreferredWeekday(DayOfWeek.Friday);

        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Monday));

        Assert.Null(definition.PreferredWeekday);
    }

    [Fact]
    public void Re_setting_recurrence_to_the_same_weekday_keeps_the_preferred_weekday()
    {
        var definition = CreateDefinition();
        definition.SetRecurrence(RecurrenceRule.Weekly(Friday, DayOfWeek.Friday));
        definition.SetPreferredWeekday(DayOfWeek.Friday);

        definition.SetRecurrence(RecurrenceRule.Weekly(Friday.AddDays(7), DayOfWeek.Friday));

        Assert.Equal(DayOfWeek.Friday, definition.PreferredWeekday);
    }
}
