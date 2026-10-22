using StoreSpike.Core.Cart;
using StoreSpike.Core.Catalog;
using StoreSpike.Core.Matching;
using StoreSpike.Core.Shopping;

namespace StoreSpike.Core;

/// <param name="CartResult">The add attempt for a matched line; <c>null</c> when nothing was matched.</param>
public sealed record ShoppingListRunLine(ShoppingListItem Item, MatchResult Match, AddToCartResult? CartResult);

public sealed record ShoppingListRunResult(IReadOnlyList<ShoppingListRunLine> Lines, SimulatedCart Cart);

/// <summary>
/// The whole spike flow for one list: parse each line, search the catalog, match, and try to put
/// the match in a simulated cart. A line only reaches the cart when it is both matched and has an
/// explicit amount.
/// </summary>
public sealed class ShoppingListRun
{
    private readonly IProductCatalog _catalog;

    public ShoppingListRun(IProductCatalog catalog) => _catalog = catalog;

    public async Task<ShoppingListRunResult> ExecuteAsync(IEnumerable<string> lines, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var cart = new SimulatedCart();
        var results = new List<ShoppingListRunLine>();

        foreach (var line in lines.Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var item = ShoppingListParser.Parse(line);

            var match = item.ProductTerm.Length == 0
                ? MatchResult.NotFound
                : ProductMatcher.Match(item, await _catalog.SearchAsync(item.ProductTerm, cancellationToken));

            var cartResult = match.Product is null ? (AddToCartResult?)null : cart.Add(match.Product, item.Quantity);
            results.Add(new ShoppingListRunLine(item, match, cartResult));
        }

        return new ShoppingListRunResult(results, cart);
    }
}
