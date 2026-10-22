using System.Text;

namespace StoreSpike.Core.Matching;

/// <summary>
/// Lower-cases and tokenizes product text for matching. å, ä and ö are kept as distinct letters
/// (they are separate letters in Swedish - "mjölk" is not "mjolk"), while loan-word accents are
/// folded so "kycklingfilé" and "kycklingfile" compare equal.
/// </summary>
public static class TextNormalizer
{
    public static string Normalize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.ToLowerInvariant())
        {
            builder.Append(c switch
            {
                'é' or 'è' or 'ê' or 'ë' => 'e',
                'á' or 'à' or 'â' => 'a',
                'í' or 'ì' or 'î' or 'ï' => 'i',
                'ó' or 'ò' or 'ô' => 'o',
                'ú' or 'ù' or 'û' or 'ü' => 'u',
                _ => c,
            });
        }

        return builder.ToString();
    }

    /// <summary>Normalized words; anything that is not a letter or digit separates words.</summary>
    public static IReadOnlyList<string> Tokenize(string text)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();

        foreach (var c in Normalize(text))
        {
            if (char.IsLetterOrDigit(c))
            {
                current.Append(c);
            }
            else if (current.Length > 0)
            {
                tokens.Add(current.ToString());
                current.Clear();
            }
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return tokens;
    }
}
