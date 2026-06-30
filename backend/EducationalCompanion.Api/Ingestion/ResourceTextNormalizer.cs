using System.Text;
using System.Text.RegularExpressions;

namespace EducationalCompanion.Api.Ingestion;

public static class ResourceTextNormalizer
{
    private static readonly Regex CollapseWhitespace = new(@"\s+", RegexOptions.Compiled);

    public static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return string.Empty;

        var normalized = CollapseWhitespace.Replace(raw.Trim(), " ");
        return normalized;
    }

    public static string Truncate(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        return text[..maxLength];
    }

    public static string BuildSummary(string normalizedText, int maxSummaryLength)
    {
        if (string.IsNullOrEmpty(normalizedText))
            return string.Empty;

        return Truncate(normalizedText, maxSummaryLength);
    }

    public static string ReadUtf8Text(Stream stream)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }
}
