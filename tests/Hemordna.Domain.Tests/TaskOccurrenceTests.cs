using Hemordna.Domain.Common;
using Hemordna.Domain.Tasks;

namespace Hemordna.Domain.Tests;

public class TaskOccurrenceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 2, 3, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CompletedAt = new(2026, 2, 6, 18, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly Friday = new(2026, 2, 6);

    private static TaskOccurrence CreateOccurrence(bool canBeDeferred = true, DateOnly? date = null)
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Hall", 7, CreatedAt);
        definition.SetCanBeDeferred(canBeDeferred);
        return definition.ScheduleFor(date ?? Friday, CreatedAt);
    }

    [Fact]
    public void A_new_occurrence_is_outstanding()
    {
        var occurrence = CreateOccurrence();

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
        Assert.True(occurrence.IsOutstanding);
        Assert.Null(occurrence.CompletedAt);
        Assert.Null(occurrence.CompletedByMemberId);
    }

    [Fact]
    public void Complete_records_who_finished_it_and_when()
    {
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();

        occurrence.Complete(annaId, CompletedAt);

        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
        Assert.False(occurrence.IsOutstanding);
        Assert.Equal(annaId, occurrence.CompletedByMemberId);
        Assert.Equal(CompletedAt, occurrence.CompletedAt);
    }

    [Fact]
    public void Completing_twice_keeps_the_first_completion()
    {
        // Two clients can mark the same task done; the second request must not rewrite history.
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();
        var bjornId = Guid.NewGuid();

        occurrence.Complete(annaId, CompletedAt);
        occurrence.Complete(bjornId, CompletedAt.AddMinutes(5));

        Assert.Equal(annaId, occurrence.CompletedByMemberId);
        Assert.Equal(CompletedAt, occurrence.CompletedAt);
    }

    [Fact]
    public void Reopening_within_the_window_by_the_same_person_makes_it_outstanding_again()
    {
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();
        occurrence.Complete(annaId, CompletedAt);

        occurrence.Reopen(annaId, CompletedAt.AddMinutes(10));

        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
        Assert.True(occurrence.IsOutstanding);
        Assert.Null(occurrence.CompletedAt);
        Assert.Null(occurrence.CompletedByMemberId);
    }

    [Fact]
    public void Only_the_member_who_completed_it_can_reopen_it()
    {
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();
        var bjornId = Guid.NewGuid();
        occurrence.Complete(annaId, CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.Reopen(bjornId, CompletedAt.AddMinutes(1)));
        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
    }

    [Fact]
    public void Reopening_after_15_minutes_is_rejected()
    {
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();
        occurrence.Complete(annaId, CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.Reopen(annaId, CompletedAt.AddMinutes(15).AddSeconds(1)));
        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
    }

    [Fact]
    public void Only_a_completed_occurrence_can_be_reopened()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() => occurrence.Reopen(Guid.NewGuid(), CompletedAt));
    }

    [Fact]
    public void A_reopened_occurrence_can_be_completed_again()
    {
        var occurrence = CreateOccurrence();
        var annaId = Guid.NewGuid();
        occurrence.Complete(annaId, CompletedAt);
        occurrence.Reopen(annaId, CompletedAt.AddMinutes(1));

        occurrence.Complete(annaId, CompletedAt.AddMinutes(2));

        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);
        Assert.Equal(CompletedAt.AddMinutes(2), occurrence.CompletedAt);
    }

    [Fact]
    public void A_skipped_occurrence_cannot_be_completed()
    {
        var occurrence = CreateOccurrence();
        occurrence.Skip();

        Assert.Throws<DomainException>(() => occurrence.Complete(Guid.NewGuid(), CompletedAt));
    }

    [Fact]
    public void A_completed_occurrence_cannot_be_skipped_or_reassigned()
    {
        var occurrence = CreateOccurrence();
        occurrence.Complete(Guid.NewGuid(), CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.Skip());
        Assert.Throws<DomainException>(() => occurrence.AssignTo(Guid.NewGuid()));
        Assert.Throws<DomainException>(() => occurrence.Unassign());
    }

    [Fact]
    public void Skipping_twice_is_a_no_op()
    {
        var occurrence = CreateOccurrence();

        occurrence.Skip();
        occurrence.Skip();

        Assert.Equal(TaskOccurrenceStatus.Skipped, occurrence.Status);
    }

    [Fact]
    public void AssignTo_rejects_an_empty_member_id()
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<ArgumentException>(() => occurrence.AssignTo(Guid.Empty));
    }

    [Fact]
    public void Deferring_moves_the_scheduled_date_but_keeps_the_original_due_date()
    {
        var occurrence = CreateOccurrence();
        var monday = Friday.AddDays(3);

        occurrence.DeferTo(monday);

        Assert.Equal(monday, occurrence.ScheduledDate);
        Assert.Equal(Friday, occurrence.OriginalScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, occurrence.Status);
        Assert.True(occurrence.IsOutstanding);
    }

    [Fact]
    public void A_non_deferrable_occurrence_cannot_be_deferred()
    {
        var occurrence = CreateOccurrence(canBeDeferred: false);

        Assert.Throws<DomainException>(() => occurrence.DeferTo(Friday.AddDays(1)));
        Assert.Equal(Friday, occurrence.ScheduledDate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void An_occurrence_can_only_be_deferred_forwards(int dayOffset)
    {
        var occurrence = CreateOccurrence();

        Assert.Throws<DomainException>(() => occurrence.DeferTo(Friday.AddDays(dayOffset)));
    }

    [Fact]
    public void A_completed_occurrence_cannot_be_deferred()
    {
        var occurrence = CreateOccurrence();
        occurrence.Complete(Guid.NewGuid(), CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.DeferTo(Friday.AddDays(1)));
    }

    [Fact]
    public void An_outstanding_occurrence_is_overdue_after_its_original_due_date()
    {
        var occurrence = CreateOccurrence(date: Friday.AddDays(-2));

        Assert.True(occurrence.IsOverdueOn(Friday));
        Assert.False(occurrence.IsOverdueOn(Friday.AddDays(-2)));
    }

    [Fact]
    public void Deferring_does_not_hide_that_an_occurrence_is_overdue()
    {
        var occurrence = CreateOccurrence(date: Friday.AddDays(-2));
        occurrence.DeferTo(Friday);

        Assert.True(occurrence.IsOverdueOn(Friday));
    }

    [Fact]
    public void A_completed_occurrence_is_never_overdue()
    {
        var occurrence = CreateOccurrence(date: Friday.AddDays(-2));
        occurrence.Complete(Guid.NewGuid(), CompletedAt);

        Assert.False(occurrence.IsOverdueOn(Friday));
    }

    [Fact]
    public void Bringing_a_tomorrow_task_forward_moves_the_date_but_keeps_the_original()
    {
        var tomorrow = Friday.AddDays(1);
        var occurrence = CreateOccurrence(date: tomorrow);

        occurrence.BringForwardTo(Friday);

        Assert.Equal(Friday, occurrence.ScheduledDate);
        Assert.Equal(tomorrow, occurrence.OriginalScheduledDate);
        Assert.False(occurrence.IsOverdueOn(Friday));
        Assert.False(occurrence.IsOverdueOn(tomorrow));
        Assert.True(occurrence.IsBroughtForwardOn(Friday));
    }

    [Fact]
    public void Bringing_a_task_already_due_today_forward_is_rejected()
    {
        var occurrence = CreateOccurrence(date: Friday);

        Assert.Throws<DomainException>(() => occurrence.BringForwardTo(Friday));
        Assert.Equal(Friday, occurrence.ScheduledDate);
    }

    [Fact]
    public void Bringing_an_already_overdue_task_forward_is_rejected()
    {
        var occurrence = CreateOccurrence(date: Friday.AddDays(-1));

        Assert.Throws<DomainException>(() => occurrence.BringForwardTo(Friday));
    }

    [Fact]
    public void A_completed_occurrence_cannot_be_brought_forward()
    {
        var occurrence = CreateOccurrence(date: Friday.AddDays(1));
        occurrence.Complete(Guid.NewGuid(), CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.BringForwardTo(Friday));
    }

    [Fact]
    public void A_brought_forward_task_can_still_be_completed_and_deferred()
    {
        var tomorrow = Friday.AddDays(1);
        var occurrence = CreateOccurrence(date: tomorrow);
        occurrence.BringForwardTo(Friday);

        occurrence.Complete(Guid.NewGuid(), CompletedAt);
        Assert.Equal(TaskOccurrenceStatus.Completed, occurrence.Status);

        var reopened = CreateOccurrence(date: tomorrow);
        reopened.BringForwardTo(Friday);
        reopened.DeferTo(Friday.AddDays(1));

        Assert.Equal(Friday.AddDays(1), reopened.ScheduledDate);
        Assert.Equal(TaskOccurrenceStatus.Planned, reopened.Status);
    }

    [Fact]
    public void IsBroughtForwardOn_is_false_for_an_ordinary_occurrence()
    {
        var occurrence = CreateOccurrence(date: Friday);

        Assert.False(occurrence.IsBroughtForwardOn(Friday));
    }

    [Fact]
    public void UndoBringForward_puts_it_back_on_its_original_date()
    {
        var tomorrow = Friday.AddDays(1);
        var occurrence = CreateOccurrence(date: tomorrow);
        occurrence.BringForwardTo(Friday);

        occurrence.UndoBringForward();

        Assert.Equal(tomorrow, occurrence.ScheduledDate);
        Assert.Equal(tomorrow, occurrence.OriginalScheduledDate);
        Assert.False(occurrence.IsBroughtForwardOn(tomorrow));
    }

    [Fact]
    public void UndoBringForward_works_even_when_the_task_cannot_normally_be_deferred()
    {
        var tomorrow = Friday.AddDays(1);
        var occurrence = CreateOccurrence(canBeDeferred: false, date: tomorrow);
        occurrence.BringForwardTo(Friday);

        occurrence.UndoBringForward();

        Assert.Equal(tomorrow, occurrence.ScheduledDate);
    }

    [Fact]
    public void UndoBringForward_on_an_occurrence_that_was_never_brought_forward_is_rejected()
    {
        var occurrence = CreateOccurrence(date: Friday);

        Assert.Throws<DomainException>(() => occurrence.UndoBringForward());
    }

    [Fact]
    public void UndoBringForward_on_a_completed_occurrence_is_rejected()
    {
        var tomorrow = Friday.AddDays(1);
        var occurrence = CreateOccurrence(date: tomorrow);
        occurrence.BringForwardTo(Friday);
        occurrence.Complete(Guid.NewGuid(), CompletedAt);

        Assert.Throws<DomainException>(() => occurrence.UndoBringForward());
    }

    [Fact]
    public void A_new_occurrence_is_not_added_as_extra_by_default()
    {
        var occurrence = CreateOccurrence();

        Assert.False(occurrence.AddedAsExtra);
    }

    [Fact]
    public void ScheduleFor_can_mark_an_occurrence_as_added_as_extra()
    {
        var definition = TaskDefinition.Create(Guid.NewGuid(), "Rensa garderoben", 15, CreatedAt);

        var occurrence = definition.ScheduleFor(Friday, CreatedAt, addedAsExtra: true);

        Assert.True(occurrence.AddedAsExtra);
    }
}
