using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class LearningResource : AuditableEntity
{
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public string Topic { get; set; } = null!;
    public int Difficulty { get; set; } // 1..5 (beginner..advanced)
    public int EstimatedDurationMinutes { get; set; }

    public ResourceContentType ContentType { get; set; }

    // Navigation
    public ResourceMetadata? Metadata { get; set; }
    public ICollection<UserInteraction> Interactions { get; set; } = new List<UserInteraction>();
}