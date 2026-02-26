using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Dtos.Users;
using EducationalCompanion.Api.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace EducationalCompanion.Api.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserInteractionService _interactionService;
    private readonly IUserProfileService _userProfileService;

    public UsersController(
        IUserInteractionService interactionService,
        IUserProfileService userProfileService)
    {
        _interactionService = interactionService;
        _userProfileService = userProfileService;
    }

    // Get full profile including preferences (for dashboard, AI aggregation)
    [HttpGet("{id}")]
    public async Task<ActionResult<UserProfileResponse>> GetProfile(string id, CancellationToken ct)
    {
        var profile = await _userProfileService.GetProfileByUserIdAsync(id, ct);
        return Ok(profile);
    }

    // Get only preferences (for settings screen, partial reads)
    [HttpGet("{id}/preferences")]
    public async Task<ActionResult<UserPreferencesResponse>> GetPreferences(string id, CancellationToken ct)
    {
        var prefs = await _userProfileService.GetPreferencesByUserIdAsync(id, ct);
        return Ok(prefs);
    }

    // Update preferences only (prevents accidental changes to XP or Level)
    [HttpPut("{id}/preferences")]
    public async Task<IActionResult> UpdatePreferences(string id, UpdateUserPreferencesRequest request, CancellationToken ct)
    {
        await _userProfileService.UpdatePreferencesAsync(id, request, ct);
        return NoContent();
    }

    // Get all interactions for a user
    [HttpGet("{id}/interactions")]
    public async Task<ActionResult<IReadOnlyList<UserInteractionResponse>>> GetInteractions(string id, CancellationToken ct)
    {
        var items = await _interactionService.GetByUserAsync(id, ct);
        return Ok(items);
    }

    // Get XP and level for a user
    [HttpGet("{id}/xp")]
    public async Task<ActionResult<UserXpResponse>> GetXp(string id, CancellationToken ct)
    {
        var xp = await _userProfileService.GetXpByUserIdAsync(id, ct);
        return Ok(xp);
    }
}
