using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class UserAccessScopeMembership : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public ResourceScopeType ScopeType { get; set; }
    public string ScopeKey { get; set; } = null!;
}
