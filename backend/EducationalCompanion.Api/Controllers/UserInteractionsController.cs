using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace EducationalCompanion.Api.Controllers;

[ApiController]
[Route("api/interactions")]
public class UserInteractionsController : ControllerBase
{
    private readonly IUserInteractionService _service;

    public UserInteractionsController(IUserInteractionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserInteractionResponse>>> GetAll(
        [FromQuery] string? userId,
        [FromQuery] Guid? learningResourceId,
        [FromQuery] string? interactionType,
        CancellationToken ct)
    {
        if (userId is null && learningResourceId is null && interactionType is null)
            return Ok(await _service.GetAllAsync(ct));

        return Ok(await _service.SearchAsync(userId, learningResourceId, interactionType, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserInteractionResponse>> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return Ok(item);
    }

    // Get all interactions for a user (for AI recommendation / analytics)
    [HttpGet("by-user/{userId}")]
    public async Task<ActionResult<IReadOnlyList<UserInteractionResponse>>> GetByUser(string userId, CancellationToken ct)
    {
        var items = await _service.GetByUserAsync(userId, ct);
        return Ok(items);
    }

    [HttpPost]
    public async Task<ActionResult<UserInteractionResponse>> Create(CreateUserInteractionRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateUserInteractionRequest request, CancellationToken ct)
    {
        await _service.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
