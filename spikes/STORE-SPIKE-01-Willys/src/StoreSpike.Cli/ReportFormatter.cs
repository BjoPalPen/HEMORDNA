using System.Globalization;
using System.Text;
using StoreSpike.Core;
using StoreSpike.Core.Cart;
using StoreSpike.Core.Catalog;
using StoreSpike.Core.Matching;
using StoreSpike.Core.Shopping;

namespace StoreSpike.Cli;

/// <summary>Renders a run as a Swedish plain-text report. Output depends only on the run, never on the machine's culture.</summary>
public static class ReportFormatter
{
    private static readonly CultureInfo Swedish = CultureInfo.GetCultureInfo("sv-SE");

    public static string Format(ShoppingListRunResult result, string catalogName)
    {
        ArgumentNullException.ThrowIfNull(result);

        var report = new StringBuilder();
        report.AppendLine($"STORE-SPIKE-01 – matchningsrapport (katalog: {catalogName})");
        report.AppendLine();

        for (var i = 0; i < result.Lines.Count; i++)
        {
            var line = result.Lines[i];
            report.AppendLine($"{i + 1}. {line.Item.OriginalText}");
            report.AppendLine($"   Tolkning:  {Describe(line.Item)}");
            report.AppendLine($"   Matchning: {Describe(line.Match)}");

            foreach (var candidate in line.Match.Candidates)
            {
                var marker = ReferenceEquals(candidate, line.Match.Product) ? "*" : "-";
                report.AppendLine($"     {marker} {Describe(candidate)}");
            }

            report.AppendLine($"   Varukorg:  {Describe(line.CartResult)}");
            report.AppendLine();
        }

        report.AppendLine("Simulerad varukorg:");
        if (result.Cart.Lines.Count == 0)
        {
            report.AppendLine("  (tom)");
        }

        foreach (var cartLine in result.Cart.Lines)
        {
            var unit = cartLine.Product.SalesUnit == SalesUnit.Kilogram ? "kg" : "st";
            report.AppendLine(
                $"  {cartLine.Quantity.ToString("0.###", Swedish)} {unit} {cartLine.Product.Name}  {Money(cartLine.LineTotal)}");
        }

        report.AppendLine($"Summa: {Money(result.Cart.Total)}");
        return report.ToString();
    }

    private static string Describe(ShoppingListItem item)
    {
        var parts = new List<string> { $"vara \"{item.ProductTerm}\"" };
        parts.Add(item.Quantity is null
            ? "mängd saknas"
            : $"mängd {item.Quantity.Amount.ToString("0.###", Swedish)} {UnitText(item.Quantity.Unit)}");

        if (item.Size is not null)
        {
            parts.Add($"storlek {item.Size}");
        }

        if (item.Note is not null)
        {
            parts.Add($"anteckning \"{item.Note}\"");
        }

        return string.Join(", ", parts);
    }

    private static string Describe(MatchResult match) => match.Status switch
    {
        MatchStatus.Matched => "Matchad",
        MatchStatus.NeedsUserChoice => match.Candidates.Count == 1
            ? "Användaren behöver bekräfta (1 kandidat, lös träff)"
            : $"Användaren behöver välja ({match.Candidates.Count} kandidater)",
        MatchStatus.NotFound => "Ingen träff",
        _ => throw new ArgumentOutOfRangeException(nameof(match), match.Status, null),
    };

    private static string Describe(StoreProduct product)
    {
        var price = product.Price is null
            ? "pris saknas"
            : $"{Money(product.Price.Value)}/{(product.SalesUnit == SalesUnit.Kilogram ? "kg" : "st")}";
        var volume = product.DisplayVolume is null ? string.Empty : $" {product.DisplayVolume}";
        return $"{product.Code} {product.Name}{volume}, {price}";
    }

    private static string Describe(AddToCartResult? result) => result switch
    {
        null => "ej tillagd – ingen entydig produkt",
        AddToCartResult.Added => "tillagd",
        AddToCartResult.QuantityMissing => "ej tillagd – mängd saknas, användaren behöver ange den",
        AddToCartResult.UnitMismatch => "ej tillagd – mängdens enhet passar inte hur varan säljs",
        AddToCartResult.InvalidQuantity => "ej tillagd – ogiltig mängd",
        AddToCartResult.ProductUnavailable => "ej tillagd – varan finns inte",
        AddToCartResult.PriceMissing => "ej tillagd – pris saknas",
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, null),
    };

    private static string UnitText(QuantityUnit unit) => unit switch
    {
        QuantityUnit.Piece => "st",
        QuantityUnit.Gram => "g",
        QuantityUnit.Hectogram => "hg",
        QuantityUnit.Kilogram => "kg",
        QuantityUnit.Millilitre => "ml",
        QuantityUnit.Centilitre => "cl",
        QuantityUnit.Decilitre => "dl",
        QuantityUnit.Litre => "l",
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, null),
    };

    private static string Money(decimal amount) => $"{amount.ToString("0.00", Swedish)} kr";
}
