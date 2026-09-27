using System.Text.RegularExpressions;

namespace EKitap.Api.Services;

public sealed partial class ContactInfoCleaner
{
    public string Clean(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var protectedRanges = OrcidRegex().Matches(text).Select(match => (match.Index, match.Length)).ToArray();
        var removals = new List<(int Start, int End)>();

        foreach (var match in EmailRegex().Matches(text).Cast<Match>().Concat(PhoneRegex().Matches(text).Cast<Match>()))
        {
            // ORCID içindeki sayı grupları telefon olarak temizlenmez.
            if (protectedRanges.Any(range => match.Index < range.Index + range.Length && match.Index + match.Length > range.Index))
                continue;

            var start = match.Index;
            var end = match.Index + match.Length;
            var label = ContactLabelRegex().Match(text[..start]);
            if (label.Success)
                start = label.Index;

            var separator = RightSeparatorRegex().Match(text[end..]);
            if (separator.Success)
                end += separator.Length;
            else
            {
                var leftSeparator = LeftSeparatorRegex().Match(text[..start]);
                if (leftSeparator.Success)
                    start = leftSeparator.Index;
            }
            removals.Add((start, end));
        }

        if (removals.Count == 0)
            return text;

        var merged = new List<(int Start, int End)>();
        foreach (var range in removals.OrderBy(range => range.Start))
        {
            if (merged.Count > 0 && range.Start <= merged[^1].End)
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, range.End));
            else
                merged.Add(range);
        }

        var result = text;
        foreach (var range in merged.AsEnumerable().Reverse())
            result = result.Remove(range.Start, range.End - range.Start);
        return result.Trim();
    }

    [GeneratedRegex(@"(?<![\p{L}\p{N}._%+\-])[\p{L}\p{N}._%+\-]+@[\p{L}\p{N}\-]+(?:\.[\p{L}\p{N}\-]+)*\.[\p{L}]{2,}(?![\p{L}\p{N}\-])", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex EmailRegex();

    // Etiketsiz numaralarda ulusal/uluslararası önek zorunludur.
    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:\+[1-9](?:[ \t().\-]*\d){7,14}|(?:0090|90)[ \t.\-]*(?:\([2-5]\d{2}\)|[2-5]\d{2})[ \t.\-]*\d{3}[ \t.\-]*\d{2}[ \t.\-]*\d{2}|(?:\(0[2-5]\d{2}\)|0[ \t.\-]*(?:\([2-5]\d{2}\)|[2-5]\d{2}))[ \t.\-]*\d{3}[ \t.\-]*\d{2}[ \t.\-]*\d{2}|(?:Tel(?:efon)?(?:u)?(?:\.?[ \t]*No\.?)?|GSM|Mobile|Phone|Cep(?:[ \t]+telefonu)?|İrtibat|Irtibat)[ \t]*:?[ \t]*(?:\([2-5]\d{2}\)|[2-5]\d{2})[ \t.\-]*\d{3}[ \t.\-]*\d{2}[ \t.\-]*\d{2})(?![\p{L}\p{N}])", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"(?<!\d)\d{4}-\d{4}-\d{4}-\d{3}[\dXx](?!\d)", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex OrcidRegex();

    [GeneratedRegex(@"(?<![\p{L}\p{N}])(?:E[\- ]?posta|E[\- ]?mail|Mail|Tel(?:efon)?(?:u)?(?:\.?[ \t]*No\.?)?|GSM|Mobile|Phone|Cep(?:[ \t]+telefonu)?|İrtibat|Irtibat|İletişim|Iletisim)[ \t]*:?[ \t]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, 1000)]
    private static partial Regex ContactLabelRegex();

    [GeneratedRegex(@"^[ \t]*(?:[|;/]|-[ \t]+)[ \t]*", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex RightSeparatorRegex();

    [GeneratedRegex(@"[ \t]*(?:[|;/]|[ \t]+-)[ \t]*$", RegexOptions.CultureInvariant, 1000)]
    private static partial Regex LeftSeparatorRegex();
}
