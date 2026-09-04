namespace Hemordna.Domain.Common;

/// <summary>
/// Thrown when an operation would violate a domain invariant or an illegal state transition.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
}
