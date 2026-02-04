using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class UserBadge : BaseEntity
{
    public string UserId { get; set; } = null!;
    public Guid BadgeId { get; set; }

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public UserProfile? UserProfile { get; set; }
    public Badge? Badge { get; set; }
}