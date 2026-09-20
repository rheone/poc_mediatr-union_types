using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace MediatrUnionPoc.ArchitectureTests;

/// <summary>
/// Reproduces the anchor ids GitHub generates for Markdown headings, so a link fragment can be
/// checked against the headings of the file it points into.
/// </summary>
internal static partial class MarkdownSlug
{
    /// <summary>
    /// Computes the anchor for one heading's Markdown text: the rendered text lower-cased, every
    /// character that is not a letter, mark, number, connector punctuation, hyphen or space removed,
    /// and each space turned into a hyphen. Code spans keep their text, links keep their label, and
    /// raw HTML tags and emphasis markers disappear.
    /// </summary>
    /// <param name="headingText">The heading text without its leading <c>#</c> markers.</param>
    /// <returns>The anchor, without the duplicate suffix.</returns>
    public static string ForHeading(string headingText)
    {
        var rendered = new StringBuilder();
        var position = 0;
        foreach (var span in CodeSpan().Matches(headingText).Cast<Match>())
        {
            rendered.Append(RenderPlain(headingText[position..span.Index]));
            rendered.Append(StripOneSpace(span.Groups["code"].Value));
            position = span.Index + span.Length;
        }

        rendered.Append(RenderPlain(headingText[position..]));

        var slug = new StringBuilder();
        foreach (var character in rendered.ToString().Trim().ToLowerInvariant())
        {
            if (character == ' ')
            {
                slug.Append('-');
            }
            else if (character == '-' || IsKept(character))
            {
                slug.Append(character);
            }
        }

        return slug.ToString();
    }

    /// <summary>
    /// Computes the anchors of every heading in one file, in document order: the second heading with
    /// a given slug gets <c>-1</c>, the third <c>-2</c>, and so on.
    /// </summary>
    /// <param name="headingTexts">The heading texts in document order.</param>
    /// <returns>One anchor per heading, in the same order.</returns>
    public static IReadOnlyList<string> ForHeadings(IEnumerable<string> headingTexts)
    {
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var text in headingTexts)
        {
            var slug = ForHeading(text);
            if (seen.TryGetValue(slug, out var count))
            {
                seen[slug] = count + 1;
                result.Add($"{slug}-{count}");
            }
            else
            {
                seen[slug] = 1;
                result.Add(slug);
            }
        }

        return result;
    }

    private static bool IsKept(char character) =>
        CharUnicodeInfo.GetUnicodeCategory(character)
            is UnicodeCategory.UppercaseLetter
                or UnicodeCategory.LowercaseLetter
                or UnicodeCategory.TitlecaseLetter
                or UnicodeCategory.ModifierLetter
                or UnicodeCategory.OtherLetter
                or UnicodeCategory.NonSpacingMark
                or UnicodeCategory.SpacingCombiningMark
                or UnicodeCategory.EnclosingMark
                or UnicodeCategory.DecimalDigitNumber
                or UnicodeCategory.LetterNumber
                or UnicodeCategory.OtherNumber
                or UnicodeCategory.ConnectorPunctuation;

    private static string StripOneSpace(string code) =>
        code.Length > 1 && code[0] == ' ' && code[^1] == ' ' ? code[1..^1] : code;

    private static string RenderPlain(string text)
    {
        var withoutLinks = InlineLink().Replace(text, "${label}");
        var withoutTags = HtmlTag().Replace(withoutLinks, string.Empty);
        return withoutTags
            .Replace("*", string.Empty, StringComparison.Ordinal)
            .Replace("~~", string.Empty, StringComparison.Ordinal);
    }

    [GeneratedRegex(@"(?<ticks>`+)(?<code>.+?)\k<ticks>", RegexOptions.None, 1000)]
    private static partial Regex CodeSpan();

    [GeneratedRegex(@"!?\[(?<label>[^\]]*)\]\([^)]*\)", RegexOptions.None, 1000)]
    private static partial Regex InlineLink();

    [GeneratedRegex(@"</?[A-Za-z][^>]*>", RegexOptions.None, 1000)]
    private static partial Regex HtmlTag();
}
