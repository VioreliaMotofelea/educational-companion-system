using EducationalCompanion.Api.Services;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Services;

public sealed class LearningResourceAccessNormalizerTests
{
    [Fact]
    public void Normalize_WithoutUrl_DefaultsToNoDirectAccess()
    {
        var result = LearningResourceAccessNormalizer.Normalize(null, null, null, null, null);
        Assert.Equal(ResourceAccessType.NoDirectAccess, result.AccessType);
        Assert.Equal(ResourceVisibility.Global, result.Visibility);
    }

    [Fact]
    public void Normalize_WithUrl_DefaultsToExternalUrl()
    {
        var result = LearningResourceAccessNormalizer.Normalize(
            "OpenStax",
            "https://openstax.org/",
            null,
            null,
            null);
        Assert.Equal(ResourceAccessType.ExternalUrl, result.AccessType);
        Assert.Equal("OpenStax", result.SourceName);
    }

    [Fact]
    public void Normalize_RejectsUnsafeUrlScheme()
    {
        Assert.Throws<ValidationException>(() =>
            LearningResourceAccessNormalizer.Normalize(null, "javascript:alert(1)", null, null, null));
    }

    [Fact]
    public void Normalize_ParsesExplicitAccessAndVisibility()
    {
        var result = LearningResourceAccessNormalizer.Normalize(
            null,
            null,
            "OfflinePhysical",
            "Printed in Seminar 4",
            "CourseOnly");
        Assert.Equal(ResourceAccessType.OfflinePhysical, result.AccessType);
        Assert.Equal(ResourceVisibility.CourseOnly, result.Visibility);
    }
}
