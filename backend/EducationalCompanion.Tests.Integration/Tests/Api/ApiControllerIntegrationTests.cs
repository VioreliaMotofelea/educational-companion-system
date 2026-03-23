using System.Net;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using Xunit;

namespace EducationalCompanion.Tests.Integration;

[CollectionDefinition("ApiControllerTests", DisableParallelization = true)]
public class ApiControllerTestsCollectionDefinition
{
}

[Collection("ApiControllerTests")]
public class ApiControllerIntegrationTests : IClassFixture<EducationalCompanionApiFactory>
{
    private readonly EducationalCompanionApiFactory _factory;
    private readonly System.Net.Http.HttpClient _client;

    public ApiControllerIntegrationTests(EducationalCompanionApiFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    private static async Task<string> ReadBodySafe(HttpResponseMessage resp)
    {
        try
        {
            return await resp.Content.ReadAsStringAsync();
        }
        catch
        {
            return "<unable-to-read-body>";
        }
    }

    [Fact]
    public async Task LearningResourcesController_Post_InvalidContentType_ReturnsBadRequest()
    {
        _factory.ResetDatabase();

        var payload = new CreateLearningResourceRequest(
            Title: "t",
            Description: null,
            Topic: "Python",
            Difficulty: 3,
            EstimatedDurationMinutes: 10,
            ContentType: "NotAType");

        var resp = await _client.PostAsJsonAsync("/api/resources", payload);
        var body = await ReadBodySafe(resp);
        if (resp.StatusCode != HttpStatusCode.BadRequest)
            throw new Xunit.Sdk.XunitException($"Expected 400, got {(int)resp.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task LearningResourcesController_Post_ThenGetById_Works()
    {
        _factory.ResetDatabase();

        var payload = new CreateLearningResourceRequest(
            Title: "My Resource",
            Description: "desc",
            Topic: "Python",
            Difficulty: 2,
            EstimatedDurationMinutes: 20,
            ContentType: "Article");

        var createdResp = await _client.PostAsJsonAsync("/api/resources", payload);
        var createdBody = await ReadBodySafe(createdResp);
        if (createdResp.StatusCode != HttpStatusCode.Created)
            throw new Xunit.Sdk.XunitException($"Expected 201, got {(int)createdResp.StatusCode}. Body: {createdBody}");

        var created = await createdResp.Content.ReadFromJsonAsync<LearningResourceResponse>();
        Assert.NotNull(created);

        var getResp = await _client.GetFromJsonAsync<LearningResourceResponse>($"/api/resources/{created!.Id}");
        Assert.NotNull(getResp);
        Assert.Equal("My Resource", getResp!.Title);
        Assert.Equal("Python", getResp.Topic);
        Assert.Equal(2, getResp.Difficulty);
        Assert.Equal(20, getResp.EstimatedDurationMinutes);
        Assert.Equal(ResourceContentType.Article.ToString(), getResp.ContentType);
    }

    [Fact]
    public async Task UserInteractionsController_Post_MissingResource_ReturnsNotFound()
    {
        _factory.ResetDatabase();

        var payload = new CreateUserInteractionRequest(
            UserId: "user-1",
            LearningResourceId: System.Guid.NewGuid(),
            InteractionType: "Viewed",
            Rating: null,
            TimeSpentMinutes: 10);

        var resp = await _client.PostAsJsonAsync("/api/interactions", payload);
        var body = await ReadBodySafe(resp);
        if (resp.StatusCode != HttpStatusCode.NotFound)
            throw new Xunit.Sdk.XunitException($"Expected 404, got {(int)resp.StatusCode}. Body: {body}");
    }

    [Fact]
    public async Task UserInteractionsController_Post_RatedWithoutRating_ReturnsBadRequest()
    {
        _factory.ResetDatabase();

        // Insert the LearningResource needed by the service.
        using (var sp = _factory.Services.CreateScope())
        {
            var db = sp.ServiceProvider.GetRequiredService<EducationalCompanion.Infrastructure.Persistence.ApplicationDbContext>();
            var res = new LearningResource
            {
                Id = System.Guid.NewGuid(),
                Title = "t",
                Topic = "Python",
                Difficulty = 1,
                EstimatedDurationMinutes = 10,
                ContentType = ResourceContentType.Article
            };
            db.LearningResources.Add(res);
            await db.SaveChangesAsync();

            var payload = new CreateUserInteractionRequest(
                UserId: "user-1",
                LearningResourceId: res.Id,
                InteractionType: "Rated",
                Rating: null,
                TimeSpentMinutes: 5);

            var resp = await _client.PostAsJsonAsync("/api/interactions", payload);
            var body = await ReadBodySafe(resp);
            if (resp.StatusCode != HttpStatusCode.BadRequest)
                throw new Xunit.Sdk.XunitException($"Expected 400, got {(int)resp.StatusCode}. Body: {body}");
        }
    }

    [Fact]
    public async Task UsersController_PostRecommendations_MissingUser_ReturnsNotFound()
    {
        _factory.ResetDatabase();

        var recPayload = new CreateRecommendationsBatchRequest(
            Recommendations: new[]
            {
                new CreateRecommendationItemRequest(
                    LearningResourceId: System.Guid.NewGuid(),
                    Score: 0.5,
                    AlgorithmUsed: "Algo",
                    Explanation: "Exp")
            },
            ReplaceExisting: true);

        var resp = await _client.PostAsJsonAsync("/api/users/user-1/recommendations", recPayload);
        var body = await ReadBodySafe(resp);
        if (resp.StatusCode != HttpStatusCode.NotFound)
            throw new Xunit.Sdk.XunitException($"Expected 404, got {(int)resp.StatusCode}. Body: {body}");
    }
}

