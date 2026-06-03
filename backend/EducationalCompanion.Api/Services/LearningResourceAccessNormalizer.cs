using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;

namespace EducationalCompanion.Api.Services;

public static class LearningResourceAccessNormalizer
{
    private const int MaxSourceNameLength = 150;
    private const int MaxUrlLength = 2048;
    private const int MaxAccessInstructionsLength = 1000;

    public static NormalizedAccessFields Normalize(
        string? sourceName,
        string? url,
        string? accessType,
        string? accessInstructions,
        string? visibility)
    {
        var trimmedSource = TrimOrNull(sourceName);
        if (trimmedSource is not null && trimmedSource.Length > MaxSourceNameLength)
            throw new ValidationException($"SourceName must be at most {MaxSourceNameLength} characters.");

        var trimmedUrl = TrimOrNull(url);
        if (trimmedUrl is not null)
        {
            if (trimmedUrl.Length > MaxUrlLength)
                throw new ValidationException($"Url must be at most {MaxUrlLength} characters.");
            if (!IsSafeHttpUrl(trimmedUrl))
                throw new ValidationException("Url must be an absolute http or https URL.");
        }

        var trimmedInstructions = TrimOrNull(accessInstructions);
        if (trimmedInstructions is not null && trimmedInstructions.Length > MaxAccessInstructionsLength)
            throw new ValidationException($"AccessInstructions must be at most {MaxAccessInstructionsLength} characters.");

        var parsedAccessType = ParseAccessType(accessType, trimmedUrl);
        var parsedVisibility = ParseVisibility(visibility);

        return new NormalizedAccessFields(
            trimmedSource,
            trimmedUrl,
            parsedAccessType,
            trimmedInstructions,
            parsedVisibility);
    }

    public static string? TrimOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Trim();
    }

    public static bool IsSafeHttpUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;
        return uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
               || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    private static ResourceAccessType ParseAccessType(string? accessType, string? url)
    {
        if (string.IsNullOrWhiteSpace(accessType))
            return url is not null ? ResourceAccessType.ExternalUrl : ResourceAccessType.NoDirectAccess;

        if (!Enum.TryParse<ResourceAccessType>(accessType.Trim(), true, out var parsed))
            throw new ValidationException($"Invalid accessType '{accessType}'. Use NoDirectAccess, ExternalUrl, InternalPlatform, OfflinePhysical, or CommunicationChannel.");

        return parsed;
    }

    private static ResourceVisibility ParseVisibility(string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility))
            return ResourceVisibility.Global;

        if (!Enum.TryParse<ResourceVisibility>(visibility.Trim(), true, out var parsed))
            throw new ValidationException($"Invalid visibility '{visibility}'. Use Global, CourseOnly, GroupOnly, or PrivateToUser.");

        return parsed;
    }

    public sealed record NormalizedAccessFields(
        string? SourceName,
        string? Url,
        ResourceAccessType AccessType,
        string? AccessInstructions,
        ResourceVisibility Visibility);
}
