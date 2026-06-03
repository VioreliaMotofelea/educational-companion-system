using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class ResourceAccessScope : AuditableEntity
{
    public Guid LearningResourceId { get; set; }
    public LearningResource LearningResource { get; set; } = null!;

    public ResourceScopeType ScopeType { get; set; }
    public string ScopeKey { get; set; } = null!;
}
