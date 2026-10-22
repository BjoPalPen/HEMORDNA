using System.Text.Json;
using System.Text.Json.Serialization;
using StoreSpike.Core.Catalog;
using StoreSpike.Core.Matching;

namespace StoreSpike.Infrastructure;

/// <summary>
/// An invented product catalog for exercising the spike's own logic. Its products and prices are
/// made up and its JSON format is the spike's own - it is not a captured Willys response and proves
/// nothing about Willys (see docs/findings.md).
/// </summary>
/// <remarks>
/// Search is deliberately loose - any product whose name contains a query word anywhere - so that
/// it returns the same kind of noise a real store search does ("mjölkchoklad" for "mjölk"), and the
/// matcher, not the catalog, has to sort it out.
/// </remarks>
public sealed class MockProductCatalog : IProductCatalog
{
    public const string DefaultFixtureFileName = "Fixtures/mock-catalog.json";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly IReadOnlyList<StoreProduct> _products;

    public MockProductCatalog(IReadOnlyList<StoreProduct> products) => _products = products;

    public static async Task<MockProductCatalog> LoadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var fixture = await JsonSerializer.DeserializeAsync<Fixture>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException($"Mock catalog fixture '{path}' is empty.");

        return new MockProductCatalog(fixture.Products);
    }

    public Task<IReadOnlyList<StoreProduct>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var words = TextNormalizer.Tokenize(query);

        IReadOnlyList<StoreProduct> hits = _products
            .Where(product =>
            {
                var name = TextNormalizer.Normalize(product.Name);
                return words.Any(word => name.Contains(word, StringComparison.Ordinal));
            })
            .OrderBy(product => product.Code, StringComparer.Ordinal)
            .ToList();

        return Task.FromResult(hits);
    }

    private sealed record Fixture(string Note, IReadOnlyList<StoreProduct> Products);
}
