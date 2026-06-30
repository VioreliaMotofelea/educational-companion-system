namespace EducationalCompanion.Api.Dtos.ResourceFiles;

public record ResourceFileResponse(
    Guid Id,
    Guid LearningResourceId,
    string OriginalFileName,
    string MimeType,
    long SizeBytes,
    string ProcessingStatus,
    string? ProcessingError,
    DateTime CreatedAtUtc,
    DateTime? ProcessedAtUtc,
    string? UploadedByUserId
);
