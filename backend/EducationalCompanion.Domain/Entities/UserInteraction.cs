using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class UserInteraction : AuditableEntity
{
    public string UserId { get; set; } = null!; // Identity user id
    public Guid LearningResourceId { get; set; }

    public InteractionType InteractionType { get; set; }
    public int? Rating { get; set; } // 1..5 (only when rated) - null for implicit feedback
    public int? TimeSpentMinutes { get; set; }

    // Navigation
    public UserProfile? UserProfile { get; set; }
    public LearningResource? LearningResource { get; set; }
}