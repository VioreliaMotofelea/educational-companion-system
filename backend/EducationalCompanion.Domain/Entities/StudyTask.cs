using EducationalCompanion.Domain.Common;
using TaskStatus = EducationalCompanion.Domain.Enums.TaskStatus;

namespace EducationalCompanion.Domain.Entities;

public class StudyTask : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public Guid? LearningResourceId { get; set; }

    public string Title { get; set; } = null!;
    public string? Notes { get; set; }

    public DateTime DeadlineUtc { get; set; }
    public int EstimatedMinutes { get; set; }
    public int Priority { get; set; } = 3; // 1..5 (manual or computed later)

    public TaskStatus Status { get; set; } = TaskStatus.Pending;

    // Navigation
    public UserProfile? UserProfile { get; set; }
    public LearningResource? LearningResource { get; set; }
}