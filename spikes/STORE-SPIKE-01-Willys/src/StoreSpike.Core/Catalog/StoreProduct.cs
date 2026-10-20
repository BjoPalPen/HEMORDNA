namespace StoreSpike.Core.Catalog;

/// <summary>How a product is sold, and therefore what a cart quantity of it means.</summary>
public enum SalesUnit
{
    /// <summary>Sold per package; cart quantity is a whole number of packages.</summary>
    Piece,

    /// <summary>Sold by weight; cart quantity is kilograms and <see cref="StoreProduct.Price"/> is per kg.</summary>
    Kilogram,
}

/// <summary>
/// A product as a store catalog describes it. The field set mirrors what the reviewed Willys
/// clients read from Willys' search response (ASSUMED, see docs/findings.md) - it has not been
/// confirmed against a live response.
/// </summary>
/// <param name="Code">The store's product key, e.g. Willys <c>101175556_ST</c>.</param>
/// <param name="DisplayVolume">Package size as shown, e.g. "500 g" - describes the product, never the user's wanted amount.</param>
/// <param name="Price">Price per <see cref="SalesUnit"/>; <c>null</c> when the catalog gave none.</param>
public sealed record StoreProduct(
    string Code,
    string Name,
    string? Manufacturer,
    string? DisplayVolume,
    SalesUnit SalesUnit,
    decimal? Price,
    decimal? ComparePrice,
    string? ComparePriceUnit,
    bool Available);
