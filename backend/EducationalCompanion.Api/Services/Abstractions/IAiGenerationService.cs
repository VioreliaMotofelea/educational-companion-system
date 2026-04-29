namespace EducationalCompanion.Api.Services.Abstractions;

public interface IAiGenerationService
{
    Task<int> GenerateRecommendationsForUserAsync(string userId, CancellationToken ct = default);
}

