namespace Hemordna.Client.Support;

/// <summary>
/// What each <c>PresentationMode</c> value (travels as a plain string on the client - see
/// Contracts/ApiContracts.cs's own header comment) actually turns on. Three orthogonal facts -
/// images, large text, one-at-a-time layout - are folded into a handful of named modes rather
/// than three independent toggles (see docs/ARCHITECTURE.md), so this is the one place that
/// knows which named modes carry which facts, instead of every page re-deriving it.
/// </summary>
internal static class PresentationModes
{
    public static bool ShowsImages(string? presentation)
        => presentation is "ImageAndText" or "ImageAndLargeText" or "OneAtATimeImageAndLargeText";

    public static bool IsLargeText(string? presentation)
        => presentation is "LargeText" or "ImageAndLargeText" or "OneAtATimeImageAndLargeText";

    public static bool IsFocusMode(string? presentation)
        => presentation is "OneAtATime" or "OneAtATimeImageAndLargeText";
}
