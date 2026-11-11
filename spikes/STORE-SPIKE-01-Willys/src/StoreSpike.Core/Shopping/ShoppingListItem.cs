namespace StoreSpike.Core.Shopping;

/// <summary>
/// One parsed line of a shopping list. <see cref="Quantity"/> is <c>null</c> whenever the line
/// does not state an explicit amount - it is never filled in with a default.
/// </summary>
/// <param name="OriginalText">The line exactly as the user wrote it.</param>
/// <param name="ProductTerm">What to search for, e.g. "nötfärs".</param>
/// <param name="Quantity">The explicit amount, if any.</param>
/// <param name="Size">A size attribute such as "L" from "storlek L" - a product property, not an amount.</param>
/// <param name="Note">A purpose such as "till två måltider" - kept for the user, never turned into an amount.</param>
public sealed record ShoppingListItem(
    string OriginalText,
    string ProductTerm,
    Quantity? Quantity,
    string? Size,
    string? Note)
{
    public bool IsQuantityMissing => Quantity is null;
}
