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

    public string? SourceName { get; set; }

    public string? Url { get; set; }

    public ResourceAccessType AccessType { get; set; } = ResourceAccessType.NoDirectAccess;

    public string? AccessInstructions { get; set; }

    public ResourceVisibility Visibility { get; set; } = ResourceVisibility.Global;

    public string? OwnerUserId { get; set; }

    // Navigation
    public ICollection<ResourceAccessScope> AccessScopes { get; set; } = new List<ResourceAccessScope>();
    public ResourceMetadata? Metadata { get; set; }
    public ICollection<UserInteraction> Interactions { get; set; } = new List<UserInteraction>();
}