namespace Hemordna.Domain.Tasks;

/// <summary>
/// How important a task is relative to others on the same day. The numeric values are
/// ordered so that a higher value means higher priority; planning sorts on that order.
/// </summary>
public enum TaskPriority
{
    Low = 0,
    Normal = 1,
    High = 2
}
