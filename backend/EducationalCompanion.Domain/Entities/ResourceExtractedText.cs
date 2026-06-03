using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class ResourceExtractedText : BaseEntity
{
    public Guid LearningResourceId { get; set; }

    public Guid ResourceFileId { get; set; }

    public string ExtractedText { get; set; } = null!;

    public string? Summary { get; set; }

    public ResourceTextExtractionMethod ExtractionMethod { get; set; }

    public int CharacterCount { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public LearningResource? LearningResource { get; set; }

    public ResourceFile? ResourceFile { get; set; }
}
