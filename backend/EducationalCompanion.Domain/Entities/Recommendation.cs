using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class Recommendation : AuditableEntity
{
    public string UserId { get; set; } = null!;
    public Guid LearningResourceId { get; set; }

    public double Score { get; set; }
    public string AlgorithmUsed { get; set; } = null!;
    public string Explanation { get; set; } = null!;
    
    // Navigation
    public LearningResource? LearningResource { get; set; }
}