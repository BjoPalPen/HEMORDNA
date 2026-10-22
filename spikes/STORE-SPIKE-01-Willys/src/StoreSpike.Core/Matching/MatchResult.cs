using StoreSpike.Core.Catalog;

namespace StoreSpike.Core.Matching;

public enum MatchStatus
{
    /// <summary>Exactly one product is the clear best match.</summary>
    Matched,

    /// <summary>Several plausible products, or only loose ones - the user has to pick.</summary>
    NeedsUserChoice,

    /// <summary>No product matches the term (and size, if any).</summary>
    NotFound,
}

/// <param name="Product">Set only when <see cref="Status"/> is <see cref="MatchStatus.Matched"/>.</param>
/// <param name="Candidates">Best candidates in deterministic order; empty for <see cref="MatchStatus.NotFound"/>.</param>
public sealed record MatchResult(MatchStatus Status, StoreProduct? Product, IReadOnlyList<StoreProduct> Candidates)
{
    public static MatchResult NotFound { get; } = new(MatchStatus.NotFound, null, []);
}
