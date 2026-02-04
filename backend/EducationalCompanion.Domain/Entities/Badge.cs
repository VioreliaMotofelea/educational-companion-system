using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class Badge : BaseEntity
{
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string RuleDefinition { get; set; } = null!;

    // Navigation
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}