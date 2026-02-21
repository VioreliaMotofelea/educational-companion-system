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

    /// <summary>
    /// Get all interactions for a user. Id is the user identifier (UserId).
    /// </summary>
    [HttpGet("{id}/interactions")]
    public async Task<ActionResult<IReadOnlyList<UserInteractionResponse>>> GetInteractions(string id, CancellationToken ct)
    {
        var items = await _interactionService.GetByUserAsync(id, ct);
        return Ok(items);
    }

    /// <summary>
    /// Get XP and level for a user. Id is the user identifier (UserId).
    /// </summary>
    [HttpGet("{id}/xp")]
    public async Task<ActionResult<UserXpResponse>> GetXp(string id, CancellationToken ct)
    {
        var xp = await _userProfileService.GetXpByUserIdAsync(id, ct);
        return Ok(xp);
    }
}
