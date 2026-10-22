using StoreSpike.Core.Matching;
using StoreSpike.Core.Shopping;
using static StoreSpike.Tests.Products;

namespace StoreSpike.Tests;

public class ProductMatcherTests
{
    private static MatchResult Match(string line, params StoreSpike.Core.Catalog.StoreProduct[] products) =>
        ProductMatcher.Match(ShoppingListParser.Parse(line), products);

    [Fact]
    public void Case_and_loan_word_accents_do_not_matter()
    {
        var result = Match("KYCKLINGFILE", Piece("1", "Kycklingfilé"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Swedish_letters_are_not_folded()
    {
        var result = Match("mjolk", Piece("1", "Mjölk"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void A_simple_plural_matches()
    {
        var result = Match("makaron", Piece("1", "Makaroner"));

        Assert.Equal(MatchStatus.Matched, result.Status);
    }

    [Fact]
    public void Size_L_matches_L_and_not_M()
    {
        var m = Piece("M", "Ägg Frigående 12-p M");
        var l = Piece("L", "Ägg Frigående 12-p L");

        var result = Match("ägg storlek L", m, l);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(l, result.Product);
        Assert.DoesNotContain(m, result.Candidates);
    }

    [Fact]
    public void A_product_without_the_requested_size_is_not_guessed_to_have_it()
    {
        var result = Match("ägg storlek L", Piece("1", "Ägg Frigående 12-p"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void A_term_as_prefix_of_a_compound_does_not_match()
    {
        var result = Match("kycklingfilé", Piece("1", "Kycklingfiléburgare"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void The_plain_product_beats_a_processed_variant()
    {
        var plain = Piece("1", "Kycklingfilé");
        var breaded = Piece("2", "Kycklingfilé Panerad");

        var result = Match("kycklingfilé", breaded, plain, Piece("3", "Kycklingfiléburgare"));

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(plain, result.Product);
    }

    [Fact]
    public void Several_equally_good_products_need_a_user_choice()
    {
        var result = Match(
            "mjölk",
            Piece("1", "Mellanmjölk 1,5%"),
            Piece("2", "Standardmjölk 3%"),
            Piece("3", "Lättmjölk 0,5%"),
            Piece("4", "Mjölkchoklad"));

        Assert.Equal(MatchStatus.NeedsUserChoice, result.Status);
        Assert.Null(result.Product);
        Assert.Equal(["Lättmjölk 0,5%", "Mellanmjölk 1,5%", "Standardmjölk 3%"], result.Candidates.Select(p => p.Name));
    }

    [Fact]
    public void A_single_compound_match_is_still_left_to_the_user()
    {
        var result = Match("potatis", Piece("1", "Sötpotatis"));

        Assert.Equal(MatchStatus.NeedsUserChoice, result.Status);
        Assert.Null(result.Product);
    }

    [Fact]
    public void An_exact_match_outranks_a_compound()
    {
        var plain = Piece("1", "Makaroner");

        var result = Match("makaroner", Piece("2", "Snabbmakaroner"), plain);

        Assert.Equal(MatchStatus.Matched, result.Status);
        Assert.Same(plain, result.Product);
    }

    [Fact]
    public void No_match_gives_not_found_and_never_a_random_product()
    {
        var result = Match("saffran", Piece("1", "Kaffe"), Piece("2", "Makaroner"));

        Assert.Equal(MatchStatus.NotFound, result.Status);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void An_unavailable_product_is_never_matched()
    {
        var result = Match("kaffe", Piece("1", "Kaffe", available: false));

        Assert.Equal(MatchStatus.NotFound, result.Status);
    }

    [Fact]
    public void Ties_are_ordered_deterministically_regardless_of_input_order()
    {
        var a = Piece("A", "Nötfärs 12%");
        var b = Piece("B", "Nötfärs 5%");

        var forward = Match("nötfärs", a, b);
        var backward = Match("nötfärs", b, a);

        Assert.Equal(MatchStatus.NeedsUserChoice, forward.Status);
        Assert.Equal(forward.Candidates, backward.Candidates);
    }
}
