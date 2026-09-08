namespace Hemordna.Client.Components;

/// <summary>
/// A <see cref="BottomSheet"/>'s mobile height - "Half" for a short choice/summary that does not
/// need the full screen, "Full" for a form that needs room to grow. Ignored by the desktop
/// centered-dialog rendering, which has its own fixed sizing regardless of detent.
/// </summary>
public enum SheetDetent
{
    Half,
    Full
}
