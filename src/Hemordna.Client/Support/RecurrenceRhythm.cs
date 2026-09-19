using Hemordna.Client.Contracts;

namespace Hemordna.Client.Support;

/// <summary>
/// The client's own small copy of the domain's <c>RecurrenceRule.IsWeeklyRhythm</c> - the client
/// cannot reference <c>Hemordna.Domain</c> (see CLAUDE.md's layer rule), so this mirrors that
/// property's logic against the wire shape (<see cref="RecurrenceRuleContract"/>) instead. The two
/// must be kept in sync by hand. True only for Weekly/Monthly with Interval 1 - a sparse rule
/// (Interval &gt; 1, e.g. "every 12 months") carries WHICH month or phase is meant in its own
/// StartDate, which the weekday-locking UI cannot express without destroying it. See
/// docs/ARCHITECTURE.md "Beslut: Glesa regler lämnas i fred".
/// </summary>
public static class RecurrenceRhythm
{
    public static bool IsWeeklyRhythm(RecurrenceRuleContract? recurrence)
        => recurrence is { Frequency: "Weekly" or "Monthly", Interval: 1 };
}
