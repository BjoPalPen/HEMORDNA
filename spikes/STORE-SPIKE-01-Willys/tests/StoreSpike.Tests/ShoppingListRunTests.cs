using StoreSpike.Cli;
using StoreSpike.Core;
using StoreSpike.Core.Cart;
using StoreSpike.Core.Matching;
using StoreSpike.Infrastructure;

namespace StoreSpike.Tests;

/// <summary>The whole flow - test list, mock catalog, matcher, cart, report - as the CLI runs it.</summary>
public class ShoppingListRunTests
{
    private static readonly string TestListPath = Path.Combine(AppContext.BaseDirectory, "testdata", "inkopslista.txt");
    private static readonly string CatalogPath = Path.Combine(AppContext.BaseDirectory, MockProductCatalog.DefaultFixtureFileName);

    private static async Task<ShoppingListRunResult> RunTestListAsync()
    {
        var catalog = await MockProductCatalog.LoadAsync(CatalogPath, CancellationToken.None);
        var lines = await File.ReadAllLinesAsync(TestListPath);
        return await new ShoppingListRun(catalog).ExecuteAsync(lines, CancellationToken.None);
    }

    [Fact]
    public async Task Each_test_list_line_gets_the_expected_outcome_against_the_mock_catalog()
    {
        var result = await RunTestListAsync();

        var outcome = result.Lines.ToDictionary(l => l.Item.OriginalText, l => (l.Match.Status, l.Match.Product?.Code));
        Assert.Equal((MatchStatus.NeedsUserChoice, (string?)null), outcome["nötfärs till två måltider"]);
        Assert.Equal((MatchStatus.Matched, "MOCK-003"), outcome["blandfärs"]);
        Assert.Equal((MatchStatus.Matched, "MOCK-004"), outcome["kycklingfilé"]);
        Assert.Equal((MatchStatus.NeedsUserChoice, (string?)null), outcome["mjölk"]);
        Assert.Equal((MatchStatus.NeedsUserChoice, (string?)null), outcome["kaffe"]);
        Assert.Equal((MatchStatus.Matched, "MOCK-013"), outcome["makaroner"]);
        Assert.Equal((MatchStatus.NeedsUserChoice, (string?)null), outcome["potatis"]);
        Assert.Equal((MatchStatus.Matched, "MOCK-019"), outcome["ägg storlek L"]);
    }

    [Fact]
    public async Task Nothing_reaches_the_cart_because_no_line_states_an_amount()
    {
        var result = await RunTestListAsync();

        Assert.Empty(result.Cart.Lines);
        Assert.All(
            result.Lines.Where(l => l.Match.Status == MatchStatus.Matched),
            l => Assert.Equal(AddToCartResult.QuantityMissing, l.CartResult));
    }

    [Fact]
    public async Task The_report_is_deterministic_and_says_what_the_user_must_supply()
    {
        var first = ReportFormatter.Format(await RunTestListAsync(), "mock");
        var second = ReportFormatter.Format(await RunTestListAsync(), "mock");

        Assert.Equal(first, second);
        Assert.Contains("anteckning \"till två måltider\"", first);
        Assert.Contains("ej tillagd – mängd saknas, användaren behöver ange den", first);
        Assert.Contains("Summa: 0,00 kr", first);
    }
}
