using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class UserPreferences : AuditableEntity
{
    public Guid UserProfileId { get; set; }

    public int? PreferredDifficulty { get; set; }
    public string? PreferredContentTypesCsv { get; set; }
    public string? PreferredTopicsCsv { get; set; }

    // Navigation
    public UserProfile? UserProfile { get; set; }
}