using System.Globalization;
using System.Text;

namespace Galaxy101.Server.Social;

/// <summary>
/// Profanity in what other players read: table chat is masked (***), names, status lines and table names are refused.
/// Words are compared whole after folding Turkish letters, look-alike digits and symbols (s1kt1r, $ik) and repeated
/// letters (siiiktir), and letters spaced or dotted apart (o r o s p u) are joined first — so an ordinary word that
/// merely contains a bad one ("götürmek", "sikke") stays untouched.
/// </summary>
public static class TextFilter
{
    // Whole words only.
    private static readonly HashSet<string> Words = Fold(
        "amk", "aq", "amq", "mk", "oc", "oç", "amcik", "amcık", "aminakoyim", "aminakoyayim", "orospu", "orosbu", "pic", "piç",
        "sik", "sikik", "siki", "sikim", "sikis", "sikiş", "siktir", "siktirgit", "sikerim", "sikeyim", "sokayim", "sokayım", "yarak", "yarrak",
        "yarrağım", "dalyarak", "gotveren", "götveren", "ibne", "kahpe", "pezevenk", "gavat", "surtuk", "sürtük", "yavsak", "yavşak",
        "serefsiz", "şerefsiz", "kaltak", "pust", "puşt", "tasak", "taşak", "kancik", "kancık",
        "fuck", "fucker", "fucking", "motherfucker", "shit", "bitch", "cunt", "dick", "pussy", "asshole", "nigger", "nigga", "faggot", "whore",
        "slut", "bastard", "wanker", "retard");

    // Stems that are bad at the start of any longer word (siktirgit, orospuçocuğu, fuckin, yarrakkafa…).
    private static readonly string[] Stems = Fold(
        "siktir", "sikeyim", "sikerim", "orospu", "orosbu", "amcik", "aminakoy", "yarrak", "pezevenk", "kahpe", "yavsak", "serefsiz",
        "gotveren", "fuck", "motherfuck", "bitch", "asshole", "nigger", "faggot", "whore").ToArray();

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static bool IsClean(string? text) => text is null || Find(text).Count == 0;

    /// <summary>The text with every offending word starred out, letter for letter.</summary>
    public static string Mask(string text)
    {
        var spans = Find(text); if (spans.Count == 0) return text;
        var chars = text.ToCharArray();
        foreach (var (start, end) in spans) for (int i = start; i < end; i++) if (!char.IsWhiteSpace(chars[i])) chars[i] = '*';
        return new string(chars);
    }

    /// <summary>[start, end) ranges of offending words in <paramref name="text"/>.</summary>
    private static List<(int Start, int End)> Find(string text)
    {
        var found = new List<(int, int)>();
        var tokens = Tokens(text);
        foreach (var (start, end) in tokens) if (Bad(Fold(text[start..end]))) found.Add((start, end));
        // Letters typed apart ("o r o s p u", "s.i.k"): runs of one-letter tokens read as one word.
        for (int i = 0; i < tokens.Count;)
        {
            int j = i; var joined = new StringBuilder();
            while (j < tokens.Count && tokens[j].End - tokens[j].Start == 1) { joined.Append(text[tokens[j].Start]); j++; }
            if (j - i >= 2 && Bad(Fold(joined.ToString()))) found.Add((tokens[i].Start, tokens[j - 1].End));
            i = Math.Max(j, i + 1);
        }
        return found;
    }

    private static bool Bad(string word) => word.Length > 0 && (Words.Contains(word) || Stems.Any(word.StartsWith));

    /// <summary>Words: letters, digits and the look-alike symbols @ $ ! (part of a word, not separators).</summary>
    private static List<(int Start, int End)> Tokens(string text)
    {
        var tokens = new List<(int, int)>();
        for (int i = 0; i < text.Length;)
        {
            if (!IsWordChar(text[i])) { i++; continue; }
            int start = i; while (i < text.Length && IsWordChar(text[i])) i++;
            tokens.Add((start, i));
        }
        return tokens;
    }

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '@' or '$' or '!' or '*';

    private static HashSet<string> Fold(params string[] words) => new(words.Select(Fold), StringComparer.Ordinal);

    /// <summary>Lower case (Turkish rules), Turkish letters to their base, look-alikes to letters, no repeated letters.</summary>
    public static string Fold(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var raw in word.ToLower(Turkish))
        {
            char c = raw switch
            {
                'ı' => 'i', 'ş' => 's', 'ğ' => 'g', 'ç' => 'c', 'ö' => 'o', 'ü' => 'u', 'â' => 'a', 'î' => 'i', 'û' => 'u',
                '0' => 'o', '1' => 'i', '!' => 'i', '3' => 'e', '4' => 'a', '@' => 'a', '5' => 's', '$' => 's', '7' => 't', '8' => 'b',
                _ => raw,
            };
            if (!char.IsLetter(c)) continue; // a star or digit inside a word ("s*ktir") is dropped
            if (builder.Length > 0 && builder[^1] == c) continue;
            builder.Append(c);
        }
        return builder.ToString();
    }
}
