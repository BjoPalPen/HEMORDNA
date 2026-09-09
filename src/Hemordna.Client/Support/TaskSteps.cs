using System.Text.RegularExpressions;

namespace Hemordna.Client.Support;

/// <summary>
/// A task's free-text description reads as ordinary prose unless someone actually wrote it as
/// one step per line - a single blank-free line is still just a description (docs/DESIGN.md §6
/// - render a &lt;p&gt;), not a one-item list. Splits only on an actual line break, since that
/// is the one signal a person put there on purpose (typed steps, one per line), never on
/// sentence punctuation or length.
/// </summary>
public static partial class TaskSteps
{
    /// <summary>Null when the description is empty or a single line - the caller's own signal
    /// to fall back to a plain paragraph. A leading "1.", "1)", "-" or "•" is stripped from each
    /// line so someone who already numbered their own steps does not see doubled numbers next to
    /// the rendered &lt;ol&gt;'s own markers.</summary>
    public static IReadOnlyList<string>? Parse(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var lines = LineSplitRegex()
            .Split(description)
            .Select(line => StripLeadingMarker(line.Trim()))
            .Where(line => line.Length > 0)
            .ToList();

        return lines.Count > 1 ? lines : null;
    }

    private static string StripLeadingMarker(string line)
    {
        var match = LeadingMarkerRegex().Match(line);
        return match.Success ? line[match.Length..].TrimStart() : line;
    }

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineSplitRegex();

    [GeneratedRegex(@"^(?:\d+[.)]|-|•)\s*")]
    private static partial Regex LeadingMarkerRegex();
}
