using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class ScheduleSuggestion : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public Guid StudyTaskId { get; set; }

    public DateTime SuggestedDateUtc { get; set; }
    public int SuggestedDurationMinutes { get; set; }
    public string Explanation { get; set; } = null!;

    // Navigation
    public StudyTask? StudyTask { get; set; }
}