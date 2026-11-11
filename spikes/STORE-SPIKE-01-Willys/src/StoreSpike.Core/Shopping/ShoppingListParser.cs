using System.Globalization;
using System.Text.RegularExpressions;

namespace StoreSpike.Core.Shopping;

/// <summary>
/// Parses free-text shopping list lines. Only amounts written as digits plus a unit ("2 st",
/// "500 g", "1,5 l") count as a quantity; number words ("två") and purposes ("till två
/// måltider") never do, because turning them into an amount would be inventing one.
/// </summary>
public static partial class ShoppingListParser
{
    [GeneratedRegex(
        @"(?<![\p{L}\d])(?<amount>\d+(?:[.,]\d+)?)\s*(?<unit>stycken|st|gram|g|hg|kg|liter|l|dl|cl|ml)(?![\p{L}\d])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex QuantityPattern();

    [GeneratedRegex(@"\bstorlek\s+(?<size>xs|s|m|l|xl)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SizePattern();

    [GeneratedRegex(@"\btill\s+.+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PurposePattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    public static ShoppingListItem Parse(string line)
    {
        ArgumentNullException.ThrowIfNull(line);

        var original = line.Trim();
        var rest = original;

        string? note = null;
        var purpose = PurposePattern().Match(rest);
        if (purpose.Success)
        {
            note = purpose.Value.Trim();
            rest = rest.Remove(purpose.Index, purpose.Length);
        }

        string? size = null;
        var sizeMatch = SizePattern().Match(rest);
        if (sizeMatch.Success)
        {
            size = sizeMatch.Groups["size"].Value.ToUpperInvariant();
            rest = rest.Remove(sizeMatch.Index, sizeMatch.Length);
        }

        Quantity? quantity = null;
        var quantities = QuantityPattern().Matches(rest);
        // Two amounts on one line ("2 st 500 g") cannot be resolved without guessing which one the
        // user meant, so the line is treated as having no quantity and the text is left intact.
        if (quantities.Count == 1)
        {
            var match = quantities[0];
            quantity = new Quantity(ParseAmount(match.Groups["amount"].Value), ParseUnit(match.Groups["unit"].Value));
            rest = rest.Remove(match.Index, match.Length);
        }

        var term = Whitespace().Replace(rest, " ").Trim();
        return new ShoppingListItem(original, term, quantity, size, note);
    }

    private static decimal ParseAmount(string text) =>
        decimal.Parse(text.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);

    private static QuantityUnit ParseUnit(string text) => text.ToLowerInvariant() switch
    {
        "st" or "stycken" => QuantityUnit.Piece,
        "g" or "gram" => QuantityUnit.Gram,
        "hg" => QuantityUnit.Hectogram,
        "kg" => QuantityUnit.Kilogram,
        "ml" => QuantityUnit.Millilitre,
        "cl" => QuantityUnit.Centilitre,
        "dl" => QuantityUnit.Decilitre,
        "l" or "liter" => QuantityUnit.Litre,
        _ => throw new ArgumentOutOfRangeException(nameof(text), text, "Unit not covered by the quantity pattern."),
    };
}
