using StoreSpike.Core.Shopping;

namespace StoreSpike.Tests;

public class ShoppingListParserTests
{
    public static TheoryData<string> TestList => new()
    {
        "nötfärs till två måltider",
        "blandfärs",
        "kycklingfilé",
        "mjölk",
        "kaffe",
        "makaroner",
        "potatis",
        "ägg storlek L",
    };

    [Theory]
    [MemberData(nameof(TestList))]
    public void No_line_in_the_test_list_gets_an_invented_quantity(string line)
    {
        var item = ShoppingListParser.Parse(line);

        Assert.Null(item.Quantity);
        Assert.True(item.IsQuantityMissing);
    }

    [Fact]
    public void A_purpose_is_kept_as_a_note_and_not_turned_into_an_amount()
    {
        var item = ShoppingListParser.Parse("nötfärs till två måltider");

        Assert.Equal("nötfärs", item.ProductTerm);
        Assert.Equal("till två måltider", item.Note);
        Assert.Null(item.Quantity);
    }

    [Fact]
    public void A_size_is_an_attribute_and_not_litres()
    {
        var item = ShoppingListParser.Parse("ägg storlek L");

        Assert.Equal("ägg", item.ProductTerm);
        Assert.Equal("L", item.Size);
        Assert.Null(item.Quantity);
    }

    [Theory]
    [InlineData("2 st gurka", "gurka", 2, QuantityUnit.Piece)]
    [InlineData("gurka 2 st", "gurka", 2, QuantityUnit.Piece)]
    [InlineData("500 g nötfärs", "nötfärs", 500, QuantityUnit.Gram)]
    [InlineData("1,5 l mjölk", "mjölk", 1.5, QuantityUnit.Litre)]
    [InlineData("1.5 l mjölk", "mjölk", 1.5, QuantityUnit.Litre)]
    [InlineData("2kg potatis", "potatis", 2, QuantityUnit.Kilogram)]
    public void An_explicit_amount_is_parsed(string line, string term, double amount, QuantityUnit unit)
    {
        var item = ShoppingListParser.Parse(line);

        Assert.Equal(term, item.ProductTerm);
        Assert.Equal(new Quantity((decimal)amount, unit), item.Quantity);
    }

    [Fact]
    public void Two_amounts_on_one_line_are_not_resolved_by_guessing()
    {
        var item = ShoppingListParser.Parse("2 st 500 g nötfärs");

        Assert.Null(item.Quantity);
    }

    [Fact]
    public void A_number_word_is_not_an_amount()
    {
        var item = ShoppingListParser.Parse("två gurkor");

        Assert.Null(item.Quantity);
        Assert.Equal("två gurkor", item.ProductTerm);
    }
}
