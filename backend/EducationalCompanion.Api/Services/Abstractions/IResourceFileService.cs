using EducationalCompanion.Api.Dtos.ResourceFiles;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IResourceFileService
{
    Task<ResourceFileResponse> UploadAsync(
        Guid learningResourceId,
        string userId,
        IFormFile file,
        CancellationToken ct = default);

    Task<IReadOnlyList<ResourceFileResponse>> ListFilesAsync(
        Guid learningResourceId,
        string userId,
        CancellationToken ct = default);

    Task<ResourceExtractedTextResponse?> GetExtractedTextAsync(
        Guid learningResourceId,
        string userId,
        CancellationToken ct = default);

    Task DeleteFileAsync(
        Guid learningResourceId,
        Guid fileId,
        string userId,
        CancellationToken ct = default);
}
