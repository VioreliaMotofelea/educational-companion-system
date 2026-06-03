using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Enums;

namespace EducationalCompanion.Domain.Entities;

public class ResourceFile : AuditableEntity
{
    public Guid LearningResourceId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string StorageKey { get; set; } = null!;

    public string MimeType { get; set; } = null!;

    public long SizeBytes { get; set; }

    public string? UploadedByUserId { get; set; }

    public ResourceFileProcessingStatus ProcessingStatus { get; set; } = ResourceFileProcessingStatus.Pending;

    public string? ProcessingError { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public LearningResource? LearningResource { get; set; }
}
