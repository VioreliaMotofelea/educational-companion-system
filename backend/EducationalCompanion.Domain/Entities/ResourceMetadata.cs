using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class ResourceMetadata : BaseEntity
{
    public Guid LearningResourceId { get; set; }

    public string Keywords { get; set; } = null!;
    public string? EmbeddingVectorJson { get; set; }

    // Navigation
    public LearningResource? LearningResource { get; set; }
}