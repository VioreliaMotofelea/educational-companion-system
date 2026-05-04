using EducationalCompanion.Api.Dtos.Analytics;
using EducationalCompanion.Api.Dtos.Mastery;
using EducationalCompanion.Api.Dtos.Recommendations;
using EducationalCompanion.Api.Dtos.Tasks;
using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EducationalCompanion.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserInteractionService _interactionService;
    private readonly IUserProfileService _userProfileService;
    private readonly IUserEdmService _edmService;
    private readonly IRecommendationService _recommendationService;
    private readonly IAiGenerationService _aiGenerationService;
    private readonly IStudyTaskService _studyTaskService;

    public UsersController(
        IUserInteractionService interactionService,
        IUserProfileService userProfileService,
        IUserEdmService edmService,
        IRecommendationService recommendationService,
        IAiGenerationService aiGenerationService,
        IStudyTaskService studyTaskService)
    {
        _interactionService = interactionService;
        _userProfileService = userProfileService;
        _edmService = edmService;
        _recommendationService = recommendationService;
        _aiGenerationService = aiGenerationService;
        _studyTaskService = studyTaskService;
    }

    // Get full profile including preferences (for dashboard, AI aggregation)
    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var profile = await _userProfileService.GetProfileByUserIdAsync(id, ct);
        return Ok(profile);
    }

    // Get only preferences (for settings screen, partial reads)
    [HttpGet("{id}/preferences")]
    public async Task<ActionResult<UserPreferencesResponse>> GetPreferences(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var prefs = await _userProfileService.GetPreferencesByUserIdAsync(id, ct);
        return Ok(prefs);
    }

    // Update preferences only (prevents accidental changes to XP or Level)
    [HttpPut("{id}/preferences")]
    public async Task<IActionResult> UpdatePreferences(string id, UpdateUserPreferencesRequest request, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        await _userProfileService.UpdatePreferencesAsync(id, request, ct);
        return NoContent();
    }

    // Get all interactions for a user
    [HttpGet("{id}/interactions")]
    public async Task<ActionResult<IReadOnlyList<UserInteractionResponse>>> GetInteractions(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var items = await _interactionService.GetByUserAsync(id, ct);
        return Ok(items);
    }

    // Get XP and level for a user
    [HttpGet("{id}/xp")]
    public async Task<ActionResult<UserXpResponse>> GetXp(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var xp = await _userProfileService.GetXpByUserIdAsync(id, ct);
        return Ok(xp);
    }

    // ========== Educational Data Mining (EDM) Layer ==========

    // User analytics: summary and KPIs for dashboards and reporting.
    [HttpGet("{id}/analytics")]
    public async Task<ActionResult<UserAnalyticsResponse>> GetAnalytics(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var result = await _edmService.GetAnalyticsAsync(id, ct);
        return Ok(result);
    }

    // Personalized content recommendations for the user (read).
    [HttpGet("{id}/recommendations")]
    public async Task<ActionResult<IReadOnlyList<UserRecommendationItemResponse>>> GetRecommendations(
        string id,
        [FromQuery] int? limit,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var list = await _edmService.GetRecommendationsAsync(id, limit, ct);
        return Ok(list);
    }

    // Create or replace recommendations for the user (from AI service).
    [HttpPost("{id}/recommendations")]
    public async Task<ActionResult<CreatedRecommendationsResponse>> CreateRecommendations(
        string id,
        [FromBody] CreateRecommendationsBatchRequest request,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var result = await _recommendationService.CreateBatchForUserAsync(id, request, ct);
        return CreatedAtAction(nameof(GetRecommendations), new { id }, result);
    }

    // Generate recommendations by calling the AI service (server-to-server, avoids CORS).
    [HttpPost("{id}/recommendations/generate")]
    public async Task<ActionResult<GenerateRecommendationsResponse>> GenerateRecommendations(
        string id,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var generated = await _aiGenerationService.GenerateRecommendationsForUserAsync(id, ct);
        return Ok(new GenerateRecommendationsResponse(id, generated));
    }

    // Topic mastery and suggested difficulty for adaptive learning.
    [HttpGet("{id}/mastery")]
    public async Task<ActionResult<UserMasteryResponse>> GetMastery(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var result = await _edmService.GetMasteryAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("{id}/tasks")]
    public async Task<ActionResult<IReadOnlyList<StudyTaskResponse>>> GetTasks(string id, CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var tasks = await _studyTaskService.GetByUserAsync(id, ct);
        return Ok(tasks);
    }

    [HttpPost("{id}/tasks")]
    public async Task<ActionResult<StudyTaskResponse>> CreateTask(
        string id,
        [FromBody] CreateStudyTaskRequest request,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var task = await _studyTaskService.CreateForUserAsync(id, request, ct);
        return Ok(task);
    }

    [HttpPatch("{id}/tasks/{taskId:guid}/status")]
    public async Task<ActionResult<StudyTaskResponse>> UpdateTaskStatus(
        string id,
        Guid taskId,
        [FromBody] UpdateStudyTaskStatusRequest request,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var task = await _studyTaskService.UpdateStatusAsync(id, taskId, request, ct);
        return Ok(task);
    }

    [HttpPut("{id}/tasks/{taskId:guid}")]
    public async Task<ActionResult<StudyTaskResponse>> UpdateTask(
        string id,
        Guid taskId,
        [FromBody] UpdateStudyTaskRequest request,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        var task = await _studyTaskService.UpdateAsync(id, taskId, request, ct);
        return Ok(task);
    }

    [HttpDelete("{id}/tasks/{taskId:guid}")]
    public async Task<IActionResult> DeleteTask(
        string id,
        Guid taskId,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(id);
        await _studyTaskService.DeleteAsync(id, taskId, ct);
        return NoContent();
    }

    private void EnsureCallerMatchesUserId(string userId)
    {
        if (User?.Identity?.IsAuthenticated != true)
            return;

        var callerUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(callerUserId, userId, StringComparison.Ordinal))
            throw new ForbiddenOperationException("You are not allowed to access another user's data.");
    }
}
