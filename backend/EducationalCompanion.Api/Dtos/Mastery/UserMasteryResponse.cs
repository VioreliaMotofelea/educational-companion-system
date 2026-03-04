namespace EducationalCompanion.Api.Dtos.Mastery;

// EDM layer: topic mastery and suggested difficulty for adaptive learning.
public record UserMasteryResponse(
    string UserId,
    IReadOnlyList<TopicMasteryItem> TopicMastery,
    int SuggestedDifficulty,
    string? SuggestedDifficultyReason,
    DateTime ComputedAtUtc
);
