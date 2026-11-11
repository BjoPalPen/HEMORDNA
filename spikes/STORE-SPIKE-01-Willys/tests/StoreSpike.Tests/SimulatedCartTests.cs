using StoreSpike.Core.Cart;
using StoreSpike.Core.Shopping;
using static StoreSpike.Tests.Products;

namespace StoreSpike.Tests;

public class SimulatedCartTests
{
    private static Quantity Pieces(decimal amount) => new(amount, QuantityUnit.Piece);

    [Fact]
    public void Add_change_remove_and_clear()
    {
        var cart = new SimulatedCart();
        var coffee = Piece("1", "Kaffe", price: 64.90m);
        var pasta = Piece("2", "Makaroner", price: 17.90m);

        Assert.Equal(AddToCartResult.Added, cart.Add(coffee, Pieces(1)));
        Assert.Equal(AddToCartResult.Added, cart.Add(pasta, Pieces(2)));

        Assert.True(cart.SetQuantity("1", 3));
        Assert.Equal(3, cart.Lines.Single(l => l.Product.Code == "1").Quantity);

        Assert.True(cart.Remove("2"));
        Assert.Equal(["1"], cart.Lines.Select(l => l.Product.Code));

        cart.Clear();
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void Adding_the_same_product_twice_sums_the_quantity()
    {
        var cart = new SimulatedCart();
        var coffee = Piece("1", "Kaffe");

        cart.Add(coffee, Pieces(1));
        cart.Add(coffee, Pieces(2));

        Assert.Equal(3, Assert.Single(cart.Lines).Quantity);
    }

    [Fact]
    public void Quantity_zero_removes_the_line()
    {
        var cart = new SimulatedCart();
        cart.Add(Piece("1", "Kaffe"), Pieces(1));

        Assert.True(cart.SetQuantity("1", 0));
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void A_negative_quantity_is_rejected()
    {
        var cart = new SimulatedCart();
        cart.Add(Piece("1", "Kaffe"), Pieces(1));

        Assert.Throws<ArgumentOutOfRangeException>(() => cart.SetQuantity("1", -1));
    }

    [Fact]
    public void A_fraction_of_a_package_is_rejected()
    {
        var cart = new SimulatedCart();

        Assert.Equal(AddToCartResult.InvalidQuantity, cart.Add(Piece("1", "Kaffe"), Pieces(1.5m)));
    }

    [Fact]
    public void Setting_an_unknown_product_reports_false()
    {
        Assert.False(new SimulatedCart().SetQuantity("missing", 1));
    }

    [Fact]
    public void Total_covers_per_package_and_per_kilogram_products()
    {
        var cart = new SimulatedCart();
        cart.Add(Piece("1", "Makaroner", price: 17.90m), Pieces(2));
        cart.Add(PerKilogram("2", "Sötpotatis", price: 34.90m), new Quantity(750, QuantityUnit.Gram));

        // 2 × 17,90 = 35,80 and 0,75 kg × 34,90 = 26,175 → 26,18
        Assert.Equal(61.98m, cart.Total);
    }

    [Fact]
    public void A_line_without_a_quantity_is_not_added()
    {
        var cart = new SimulatedCart();

        Assert.Equal(AddToCartResult.QuantityMissing, cart.Add(Piece("1", "Kaffe"), quantity: null));
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void A_weight_for_a_per_package_product_is_not_converted_to_packages()
    {
        var cart = new SimulatedCart();

        var result = cart.Add(Piece("1", "Nötfärs 12%"), new Quantity(500, QuantityUnit.Gram));

        Assert.Equal(AddToCartResult.UnitMismatch, result);
        Assert.Empty(cart.Lines);
    }

    [Fact]
    public void A_piece_count_for_a_per_kilogram_product_is_a_mismatch()
    {
        Assert.Equal(
            AddToCartResult.UnitMismatch,
            new SimulatedCart().Add(PerKilogram("1", "Sötpotatis"), Pieces(2)));
    }

    [Fact]
    public void An_unavailable_or_unpriced_product_is_not_added()
    {
        var cart = new SimulatedCart();

        Assert.Equal(AddToCartResult.ProductUnavailable, cart.Add(Piece("1", "Kaffe", available: false), Pieces(1)));
        Assert.Equal(AddToCartResult.PriceMissing, cart.Add(Piece("2", "Kaffe", price: null), Pieces(1)));
        Assert.Empty(cart.Lines);
    }
}
