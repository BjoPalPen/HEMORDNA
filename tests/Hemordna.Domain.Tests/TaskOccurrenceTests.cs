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
}
