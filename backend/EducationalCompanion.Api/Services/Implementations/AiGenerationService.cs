using System.Net.Http.Json;
using System.Text.Json;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace EducationalCompanion.Api.Services.Implementations;

public class AiGenerationService : IAiGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly AiServiceOptions _options;

    public AiGenerationService(HttpClient httpClient, IOptions<AiServiceOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<int> GenerateRecommendationsForUserAsync(string userId, CancellationToken ct = default)
    {
        var baseUrl = _options.BaseUrl.TrimEnd('/');
        var url = $"{baseUrl}/generate/{userId}";

        using var response = await _httpClient.PostAsync(url, content: null, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
            throw new RecommendationGenerationException(
                $"AI service returned {(int)response.StatusCode}: {body}");

        var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("generated", out var generatedEl) || generatedEl.ValueKind != JsonValueKind.Number)
            throw new RecommendationGenerationException("AI service response missing 'generated' field.");

        return generatedEl.GetInt32();
    }
}

