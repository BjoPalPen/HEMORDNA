using StoreSpike.Core.Catalog;
using StoreSpike.Core.Shopping;

namespace StoreSpike.Core.Matching;

/// <summary>
/// Picks the product a shopping list line refers to, or says that it cannot.
/// </summary>
/// <remarks>
/// Rules, in order:
/// <list type="number">
/// <item>Unavailable products are never candidates.</item>
/// <item>Every word of the term must match a word of the product name, either exactly (allowing a
/// simple Swedish plural ending) or as the head of a compound. Swedish compounds put the head
/// last, so "mellanmjölk" is a kind of "mjölk" while "mjölkchoklad" and "kycklingfiléburgare"
/// are not kinds of "mjölk" or "kycklingfilé" - a term as a word's prefix never matches.</item>
/// <item>A size ("storlek L") must appear in the name as its own word ("l" or "large");
/// products with another size, or none, are excluded rather than guessed at.</item>
/// <item>Only an exact match can be selected automatically, and only when a single product has
/// the fewest extra name words (digits and sizes do not count). A compound-only match always goes
/// to the user: "sötpotatis" ends with "potatis" but is not what "potatis" means.</item>
/// </list>
/// Ties are ordered by name and then code (ordinal), so the outcome is deterministic.
/// </remarks>
public static class ProductMatcher
{
    private const int MaxCandidates = 5;

    private static readonly string[] PluralEndings = ["ar", "er", "or", "r", "n", "en", "na"];

    private static readonly Dictionary<string, string[]> SizeWords = new(StringComparer.Ordinal)
    {
        ["XS"] = ["xs"],
        ["S"] = ["s", "small"],
        ["M"] = ["m", "medium"],
        ["L"] = ["l", "large"],
        ["XL"] = ["xl"],
    };

    private static readonly HashSet<string> AllSizeWords =
        SizeWords.Values.SelectMany(words => words).ToHashSet(StringComparer.Ordinal);

    private enum Tier
    {
        Exact = 0,
        Compound = 1,
    }

    private sealed record Scored(StoreProduct Product, Tier Tier, int ExtraWords);

    public static MatchResult Match(ShoppingListItem item, IEnumerable<StoreProduct> products)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(products);

        var termTokens = TextNormalizer.Tokenize(item.ProductTerm);
        if (termTokens.Count == 0)
        {
            return MatchResult.NotFound;
        }

        var scored = products
            .Where(product => product.Available)
            .Select(product => Score(product, termTokens, item.Size))
            .OfType<Scored>()
            .OrderBy(s => s.Tier)
            .ThenBy(s => s.ExtraWords)
            .ThenBy(s => s.Product.Name, StringComparer.Ordinal)
            .ThenBy(s => s.Product.Code, StringComparer.Ordinal)
            .ToList();

        if (scored.Count == 0)
        {
            return MatchResult.NotFound;
        }

        var bestTier = scored[0].Tier;
        var candidates = scored.Where(s => s.Tier == bestTier).ToList();
        var candidateProducts = candidates.Take(MaxCandidates).Select(s => s.Product).ToList();

        var isUniqueExact = bestTier == Tier.Exact
            && (candidates.Count == 1 || candidates[0].ExtraWords < candidates[1].ExtraWords);

        return isUniqueExact
            ? new MatchResult(MatchStatus.Matched, candidates[0].Product, candidateProducts)
            : new MatchResult(MatchStatus.NeedsUserChoice, null, candidateProducts);
    }

    private static Scored? Score(StoreProduct product, IReadOnlyList<string> termTokens, string? size)
    {
        var nameTokens = TextNormalizer.Tokenize(product.Name);

        if (size is not null && !HasSize(nameTokens, size))
        {
            return null;
        }

        var used = new HashSet<int>();
        var tier = Tier.Exact;

        foreach (var term in termTokens)
        {
            var exact = IndexOf(nameTokens, used, token => IsSameWord(token, term));
            if (exact >= 0)
            {
                used.Add(exact);
                continue;
            }

            var compound = IndexOf(nameTokens, used, token => IsCompoundHead(token, term));
            if (compound < 0)
            {
                return null;
            }

            used.Add(compound);
            tier = Tier.Compound;
        }

        var extraWords = nameTokens
            .Where((token, index) => !used.Contains(index))
            .Count(token => !char.IsDigit(token[0]) && !AllSizeWords.Contains(token));

        return new Scored(product, tier, extraWords);
    }

    private static bool HasSize(IReadOnlyList<string> nameTokens, string size) =>
        SizeWords.TryGetValue(size, out var words) && nameTokens.Any(token => words.Contains(token));

    private static int IndexOf(IReadOnlyList<string> tokens, HashSet<int> used, Func<string, bool> predicate)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            if (!used.Contains(i) && predicate(tokens[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsSameWord(string token, string term) =>
        token == term
        || PluralEndings.Any(ending => token == term + ending || term == token + ending);

    private static bool IsCompoundHead(string token, string term)
    {
        // At least two letters of modifier in front of the head, so a plural or a typo is not
        // mistaken for a compound.
        const int MinModifierLength = 2;

        return EndsWithWord(token, term)
            || PluralEndings.Any(ending => EndsWithWord(token, term + ending))
            || PluralEndings.Any(ending => term.EndsWith(ending, StringComparison.Ordinal)
                && EndsWithWord(token, term[..^ending.Length]));

        static bool EndsWithWord(string token, string head) =>
            head.Length > 0
            && token.Length >= head.Length + MinModifierLength
            && token.EndsWith(head, StringComparison.Ordinal);
    }
}
