using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class GamificationEvent : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public GamificationEventType EventType { get; set; }

    public int XpGranted { get; set; }

    // Navigation
    public UserProfile? UserProfile { get; set; }
}