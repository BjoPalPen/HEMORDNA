using StoreSpike.Core.Catalog;
using StoreSpike.Core.Shopping;

namespace StoreSpike.Core.Cart;

public enum AddToCartResult
{
    Added,

    /// <summary>The line has no explicit amount; nothing is added until the user gives one.</summary>
    QuantityMissing,

    /// <summary>The amount's unit does not fit how the product is sold (e.g. "500 g" of a per-package product).</summary>
    UnitMismatch,

    /// <summary>A per-package amount that is not a positive whole number, or a non-positive weight.</summary>
    InvalidQuantity,

    ProductUnavailable,

    PriceMissing,
}

/// <param name="Quantity">Packages for <see cref="SalesUnit.Piece"/>, kilograms for <see cref="SalesUnit.Kilogram"/>.</param>
public sealed record CartLine(StoreProduct Product, decimal Quantity)
{
    // Price is guaranteed by SimulatedCart.Add, which refuses products without one.
    public decimal LineTotal => Math.Round(Quantity * Product.Price!.Value, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// An in-memory cart that models the operations the reviewed Willys clients perform (add, set
/// quantity, remove, clear) without talking to any store. It never converts between units: "500 g"
/// of a product sold per package would need a guess about package count, so it is refused.
/// </summary>
public sealed class SimulatedCart
{
    private readonly List<CartLine> _lines = [];

    /// <summary>Lines in the order they were first added.</summary>
    public IReadOnlyList<CartLine> Lines => _lines;

    public decimal Total => _lines.Sum(line => line.LineTotal);

    public AddToCartResult Add(StoreProduct product, Quantity? quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (!product.Available)
        {
            return AddToCartResult.ProductUnavailable;
        }

        if (product.Price is null)
        {
            return AddToCartResult.PriceMissing;
        }

        if (quantity is null)
        {
            return AddToCartResult.QuantityMissing;
        }

        decimal cartQuantity;
        switch (product.SalesUnit)
        {
            case SalesUnit.Piece:
                if (quantity.Unit != QuantityUnit.Piece)
                {
                    return AddToCartResult.UnitMismatch;
                }

                if (quantity.Amount <= 0 || quantity.Amount != decimal.Truncate(quantity.Amount))
                {
                    return AddToCartResult.InvalidQuantity;
                }

                cartQuantity = quantity.Amount;
                break;

            case SalesUnit.Kilogram:
                var kilograms = quantity.ToKilograms();
                if (kilograms is null)
                {
                    return AddToCartResult.UnitMismatch;
                }

                if (kilograms <= 0)
                {
                    return AddToCartResult.InvalidQuantity;
                }

                cartQuantity = kilograms.Value;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(product), product.SalesUnit, "Unknown sales unit.");
        }

        var index = IndexOf(product.Code);
        if (index >= 0)
        {
            _lines[index] = _lines[index] with { Quantity = _lines[index].Quantity + cartQuantity };
        }
        else
        {
            _lines.Add(new CartLine(product, cartQuantity));
        }

        return AddToCartResult.Added;
    }

    /// <summary>
    /// Sets the quantity of an existing line; 0 removes it, mirroring Willys' <c>qty: 0</c>
    /// (OBSERVED in the reviewed clients). Returns <c>false</c> when the product is not in the cart.
    /// </summary>
    public bool SetQuantity(string code, decimal quantity)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(quantity);

        var index = IndexOf(code);
        if (index < 0)
        {
            return false;
        }

        if (quantity == 0)
        {
            _lines.RemoveAt(index);
            return true;
        }

        if (_lines[index].Product.SalesUnit == SalesUnit.Piece && quantity != decimal.Truncate(quantity))
        {
            throw new ArgumentException("A per-package product needs a whole number of packages.", nameof(quantity));
        }

        _lines[index] = _lines[index] with { Quantity = quantity };
        return true;
    }

    public bool Remove(string code) => SetQuantity(code, 0);

    public void Clear() => _lines.Clear();

    private int IndexOf(string code) => _lines.FindIndex(line => line.Product.Code == code);
}
