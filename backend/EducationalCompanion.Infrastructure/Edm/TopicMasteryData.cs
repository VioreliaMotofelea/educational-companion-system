namespace EducationalCompanion.Infrastructure.Edm;

// Raw EDM topic mastery from the database (Infrastructure layer only).
public record TopicMasteryData(
    string Topic,
    int ResourcesCompleted,
    double? AverageRating,
    double AverageDifficultyCompleted
);
