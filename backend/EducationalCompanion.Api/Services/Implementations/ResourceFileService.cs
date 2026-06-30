using EducationalCompanion.Api.Dtos.ResourceFiles;
using EducationalCompanion.Api.Ingestion;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EducationalCompanion.Api.Services.Implementations;

public class ResourceFileService : IResourceFileService
{
    private readonly ILearningResourceRepository _learningResourceRepo;
    private readonly IResourceAccessRepository _accessRepo;
    private readonly IResourceFileRepository _fileRepo;
    private readonly IResourceExtractedTextRepository _extractedTextRepo;
    private readonly IResourceFileStorageService _storage;
    private readonly IResourceTextExtractionService _textExtractor;
    private readonly ResourceFilesOptions _options;
    private readonly ILogger<ResourceFileService> _logger;

    public ResourceFileService(
        ILearningResourceRepository learningResourceRepo,
        IResourceAccessRepository accessRepo,
        IResourceFileRepository fileRepo,
        IResourceExtractedTextRepository extractedTextRepo,
        IResourceFileStorageService storage,
        IResourceTextExtractionService textExtractor,
        IOptions<ResourceFilesOptions> options,
        ILogger<ResourceFileService> logger)
    {
        _learningResourceRepo = learningResourceRepo;
        _accessRepo = accessRepo;
        _fileRepo = fileRepo;
        _extractedTextRepo = extractedTextRepo;
        _storage = storage;
        _textExtractor = textExtractor;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ResourceFileResponse> UploadAsync(
        Guid learningResourceId,
        string userId,
        IFormFile file,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("userId is required.");

        if (file is null || file.Length == 0)
            throw new ValidationException("File is required.");

        await EnsureResourceExistsAsync(learningResourceId, ct);
        await EnsureUserCanAccessResourceAsync(learningResourceId, userId, ct);

        var extension = ResourceFileUploadValidator.ValidateAndGetExtension(
            file.FileName,
            file.ContentType,
            file.Length,
            _options.MaxFileSizeBytes);

        var displayName = ResourceFileUploadValidator.SanitizeDisplayFileName(file.FileName);
        var storageKey = $"{Guid.NewGuid():N}{extension}";

        await using (var uploadStream = file.OpenReadStream())
        {
            await _storage.SaveAsync(uploadStream, storageKey, ct);
        }

        var entity = new ResourceFile
        {
            LearningResourceId = learningResourceId,
            OriginalFileName = displayName,
            StorageKey = storageKey,
            MimeType = string.IsNullOrWhiteSpace(file.ContentType) ? "application/octet-stream" : file.ContentType,
            SizeBytes = file.Length,
            UploadedByUserId = userId.Trim(),
            ProcessingStatus = ResourceFileProcessingStatus.Processing
        };

        await _fileRepo.AddAsync(entity, ct);
        await _fileRepo.SaveChangesAsync(ct);

        try
        {
            await using var readStream = await _storage.OpenReadAsync(storageKey, ct);
            var extraction = _textExtractor.Extract(readStream, extension);

            if (extraction.RequiresOcrFallback)
            {
                entity.ProcessingStatus = ResourceFileProcessingStatus.Failed;
                entity.ProcessingError =
                    "No selectable text found in PDF; OCR is not implemented yet (future fallback).";
                entity.ProcessedAtUtc = DateTime.UtcNow;
                await _fileRepo.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Resource file {FileId} for resource {ResourceId}: extraction requires OCR fallback.",
                    entity.Id,
                    learningResourceId);

                return Map(entity);
            }

            if (string.IsNullOrWhiteSpace(extraction.NormalizedText))
            {
                entity.ProcessingStatus = ResourceFileProcessingStatus.Failed;
                entity.ProcessingError = "No extractable text found in file.";
                entity.ProcessedAtUtc = DateTime.UtcNow;
                await _fileRepo.SaveChangesAsync(ct);
                return Map(entity);
            }

            var summary = ResourceTextNormalizer.BuildSummary(
                extraction.NormalizedText,
                _options.MaxSummaryLength);

            var extracted = new ResourceExtractedText
            {
                LearningResourceId = learningResourceId,
                ResourceFileId = entity.Id,
                ExtractedText = extraction.NormalizedText,
                Summary = summary,
                ExtractionMethod = extraction.Method,
                CharacterCount = extraction.NormalizedText.Length
            };

            await _extractedTextRepo.AddAsync(extracted, ct);

            entity.ProcessingStatus = ResourceFileProcessingStatus.Completed;
            entity.ProcessedAtUtc = DateTime.UtcNow;
            await _extractedTextRepo.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Resource file {FileId} for resource {ResourceId}: extracted {CharacterCount} characters.",
                entity.Id,
                learningResourceId,
                extracted.CharacterCount);

            return Map(entity);
        }
        catch (Exception ex) when (ex is not ValidationException and not ForbiddenOperationException)
        {
            entity.ProcessingStatus = ResourceFileProcessingStatus.Failed;
            entity.ProcessingError = "Text extraction failed.";
            entity.ProcessedAtUtc = DateTime.UtcNow;
            await _fileRepo.SaveChangesAsync(ct);

            _logger.LogWarning(
                ex,
                "Resource file {FileId} for resource {ResourceId}: extraction failed.",
                entity.Id,
                learningResourceId);

            return Map(entity);
        }
    }

    public async Task<IReadOnlyList<ResourceFileResponse>> ListFilesAsync(
        Guid learningResourceId,
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("userId is required.");

        await EnsureResourceExistsAsync(learningResourceId, ct);
        await EnsureUserCanAccessResourceAsync(learningResourceId, userId, ct);

        var files = await _fileRepo.GetByLearningResourceIdAsync(learningResourceId, ct);
        return files.Select(Map).ToList();
    }

    public async Task<ResourceExtractedTextResponse?> GetExtractedTextAsync(
        Guid learningResourceId,
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("userId is required.");

        await EnsureResourceExistsAsync(learningResourceId, ct);
        await EnsureUserCanAccessResourceAsync(learningResourceId, userId, ct);

        var latest = await _extractedTextRepo.GetLatestByLearningResourceIdAsync(learningResourceId, ct);
        if (latest is null)
            return null;

        return new ResourceExtractedTextResponse(
            latest.LearningResourceId,
            latest.ResourceFileId,
            latest.Summary,
            latest.ExtractionMethod.ToString(),
            latest.CharacterCount,
            latest.CreatedAtUtc);
    }

    public async Task DeleteFileAsync(
        Guid learningResourceId,
        Guid fileId,
        string userId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("userId is required.");

        await EnsureResourceExistsAsync(learningResourceId, ct);
        await EnsureUserCanAccessResourceAsync(learningResourceId, userId, ct);

        var file = await _fileRepo.GetByIdAsync(fileId, ct);
        if (file is null || file.LearningResourceId != learningResourceId)
            throw new NotFoundException("ResourceFile", fileId);

        await _extractedTextRepo.RemoveByResourceFileIdAsync(fileId, ct);
        _fileRepo.Remove(file);
        await _fileRepo.SaveChangesAsync(ct);
        await _extractedTextRepo.SaveChangesAsync(ct);

        try
        {
            await _storage.DeleteAsync(file.StorageKey, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not delete stored file for resource file {FileId}.",
                fileId);
        }
    }

    private async Task EnsureResourceExistsAsync(Guid learningResourceId, CancellationToken ct)
    {
        var resource = await _learningResourceRepo.GetByIdAsync(learningResourceId, ct);
        if (resource is null)
            throw new LearningResourceNotFoundException(learningResourceId);
    }

    private async Task EnsureUserCanAccessResourceAsync(
        Guid learningResourceId,
        string userId,
        CancellationToken ct)
    {
        var accessible = await _accessRepo.IsResourceAccessibleToUserAsync(userId, learningResourceId, ct);
        if (!accessible)
            throw new ForbiddenOperationException("You do not have access to this learning resource.");
    }

    private static ResourceFileResponse Map(ResourceFile f) =>
        new(
            f.Id,
            f.LearningResourceId,
            f.OriginalFileName,
            f.MimeType,
            f.SizeBytes,
            f.ProcessingStatus.ToString(),
            f.ProcessingError,
            f.CreatedAtUtc,
            f.ProcessedAtUtc,
            f.UploadedByUserId);
}
