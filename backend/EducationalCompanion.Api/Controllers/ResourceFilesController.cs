using EducationalCompanion.Api.Dtos.ResourceFiles;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace EducationalCompanion.Api.Controllers;

[ApiController]
[Route("api/resources/{resourceId:guid}")]
public class ResourceFilesController : ControllerBase
{
    private readonly IResourceFileService _resourceFileService;

    public ResourceFilesController(IResourceFileService resourceFileService)
    {
        _resourceFileService = resourceFileService;
    }

    [HttpPost("files")]
    [RequestSizeLimit(25 * 1024 * 1024)]
    public async Task<ActionResult<ResourceFileResponse>> Upload(
        Guid resourceId,
        [FromQuery] string userId,
        IFormFile file,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(userId);
        var result = await _resourceFileService.UploadAsync(resourceId, userId, file, ct);
        return CreatedAtAction(nameof(List), new { resourceId, userId }, result);
    }

    [HttpGet("files")]
    public async Task<ActionResult<IReadOnlyList<ResourceFileResponse>>> List(
        Guid resourceId,
        [FromQuery] string userId,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(userId);
        return Ok(await _resourceFileService.ListFilesAsync(resourceId, userId, ct));
    }

    [HttpGet("extracted-text")]
    public async Task<ActionResult<ResourceExtractedTextResponse>> GetExtractedText(
        Guid resourceId,
        [FromQuery] string userId,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(userId);
        var result = await _resourceFileService.GetExtractedTextAsync(resourceId, userId, ct);
        if (result is null)
            return NotFound();
        return Ok(result);
    }

    [HttpDelete("files/{fileId:guid}")]
    public async Task<IActionResult> Delete(
        Guid resourceId,
        Guid fileId,
        [FromQuery] string userId,
        CancellationToken ct)
    {
        EnsureCallerMatchesUserId(userId);
        await _resourceFileService.DeleteFileAsync(resourceId, fileId, userId, ct);
        return NoContent();
    }

    private void EnsureCallerMatchesUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("userId query parameter is required.");

        if (User?.Identity?.IsAuthenticated != true)
            return;

        var callerUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(callerUserId, userId, StringComparison.Ordinal))
            throw new ForbiddenOperationException("You are not allowed to access another user's data.");
    }
}
