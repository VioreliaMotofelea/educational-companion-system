namespace EducationalCompanion.Infrastructure.Edm;

// Read-only repository for EDM analytics and mastery aggregates.
// Keeps all EDM-specific queries in one place.
public interface IUserEdmReadRepository
{
    Task<UserAnalyticsKpisData?> GetUserAnalyticsKpisAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<TopicMasteryData>> GetTopicMasteryDataAsync(string userId, CancellationToken ct = default);
}
