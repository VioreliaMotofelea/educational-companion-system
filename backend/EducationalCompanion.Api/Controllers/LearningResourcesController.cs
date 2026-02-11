using EducationalCompanion.Api.Dtos.LearningResources;
using EducationalCompanion.Api.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace EducationalCompanion.Api.Controllers;

[ApiController]
[Route("api/resources")]
public class LearningResourcesController : ControllerBase
{
    private readonly ILearningResourceService _service;

    public LearningResourcesController(ILearningResourceService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LearningResourceResponse>>> GetAll(
        [FromQuery] string? topic,
        [FromQuery] int? difficulty,
        [FromQuery] string? contentType,
        CancellationToken ct)
    {
        if (topic is null && difficulty is null && contentType is null)
            return Ok(await _service.GetAllAsync(ct));

        return Ok(await _service.SearchAsync(topic, difficulty, contentType, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LearningResourceResponse>> GetById(Guid id, CancellationToken ct)
    {
        var item = await _service.GetByIdAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<LearningResourceResponse>> Create(CreateLearningResourceRequest request, CancellationToken ct)
    {
        var created = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateLearningResourceRequest request, CancellationToken ct)
    {
        var ok = await _service.UpdateAsync(id, request, ct);
        return ok ? NoContent() : NotFound();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok ? NoContent() : NotFound();
    }
}