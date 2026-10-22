namespace StoreSpike.Core.Shopping;

public enum QuantityUnit
{
    Piece,
    Gram,
    Hectogram,
    Kilogram,
    Millilitre,
    Centilitre,
    Decilitre,
    Litre,
}

/// <summary>An amount the user actually wrote on the list - never one derived or guessed.</summary>
public sealed record Quantity(decimal Amount, QuantityUnit Unit)
{
    public bool IsMass => Unit is QuantityUnit.Gram or QuantityUnit.Hectogram or QuantityUnit.Kilogram;

    /// <summary>The amount in kilograms, or <c>null</c> when this is not a mass.</summary>
    public decimal? ToKilograms() => Unit switch
    {
        QuantityUnit.Gram => Amount / 1000m,
        QuantityUnit.Hectogram => Amount / 10m,
        QuantityUnit.Kilogram => Amount,
        _ => null,
    };
}
