namespace StoreSpike.Core.Catalog;

/// <summary>
/// Free-text product search in one store's catalog - the boundary between the spike's own logic
/// and a store. Store selection is a property of the implementation, not of the call, because no
/// verified way to choose a Willys store per request exists (see docs/findings.md).
/// </summary>
public interface IProductCatalog
{
    Task<IReadOnlyList<StoreProduct>> SearchAsync(string query, CancellationToken cancellationToken);
}
