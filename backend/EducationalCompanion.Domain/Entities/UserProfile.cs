using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class UserProfile : AuditableEntity
{
    // Identity user id (string) - îl vei lega în Infrastructure/API
    public string UserId { get; set; } = null!;

    // Gamification
    public int Level { get; set; } = 1;
    public int Xp { get; set; } = 0;

    // Time management personalization
    public int DailyAvailableMinutes { get; set; } = 60;

    // Navigation
    public UserPreferences? Preferences { get; set; }
    public ICollection<UserInteraction> Interactions { get; set; } = new List<UserInteraction>();
    public ICollection<StudyTask> Tasks { get; set; } = new List<StudyTask>();
    public ICollection<GamificationEvent> GamificationEvents { get; set; } = new List<GamificationEvent>();
    public ICollection<UserBadge> UserBadges { get; set; } = new List<UserBadge>();
}