using StoreSpike.Core.Catalog;

namespace StoreSpike.Tests;

/// <summary>Builds invented products for tests; only the fields a test cares about need naming.</summary>
internal static class Products
{
    internal static StoreProduct Piece(string code, string name, decimal? price = 10m, bool available = true) =>
        new(code, name, Manufacturer: null, DisplayVolume: null, SalesUnit.Piece, price, null, null, available);

    internal static StoreProduct PerKilogram(string code, string name, decimal? price = 10m, bool available = true) =>
        new(code, name, Manufacturer: null, DisplayVolume: null, SalesUnit.Kilogram, price, null, null, available);
}
