namespace EducationalCompanion.Api.Dtos.Mastery;

// EDM layer: mastery level for a single topic based on completed resources and ratings.
public record TopicMasteryItem(
    string Topic,
    int ResourcesCompleted,
    double? AverageRating,
    double AverageDifficultyCompleted,
    string MasteryLevel
);
