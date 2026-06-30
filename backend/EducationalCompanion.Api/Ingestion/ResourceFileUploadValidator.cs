using EducationalCompanion.Domain.Exceptions;

namespace EducationalCompanion.Api.Ingestion;

public static class ResourceFileUploadValidator
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".markdown", ".pdf", ".docx"
    };

    private static readonly Dictionary<string, string[]> ExtensionToMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".txt"] = ["text/plain"],
        [".md"] = ["text/markdown", "text/plain", "application/octet-stream"],
        [".markdown"] = ["text/markdown", "text/plain", "application/octet-stream"],
        [".pdf"] = ["application/pdf"],
        [".docx"] = [
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/octet-stream"
        ]
    };

    public static string ValidateAndGetExtension(string originalFileName, string? contentType, long sizeBytes, long maxSizeBytes)
    {
        if (sizeBytes <= 0)
            throw new ValidationException("Uploaded file is empty.");

        if (sizeBytes > maxSizeBytes)
            throw new ValidationException($"File exceeds maximum size of {maxSizeBytes} bytes.");

        var safeName = Path.GetFileName(originalFileName);
        if (string.IsNullOrWhiteSpace(safeName))
            throw new ValidationException("File name is required.");

        var extension = Path.GetExtension(safeName);
        if (string.IsNullOrEmpty(extension) || !AllowedExtensions.Contains(extension))
            throw new ValidationException("File type is not allowed. Supported: .txt, .md, .pdf, .docx.");

        if (!string.IsNullOrWhiteSpace(contentType)
            && ExtensionToMimeTypes.TryGetValue(extension, out var allowedMimes)
            && !allowedMimes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException("Content type does not match file extension.");
        }

        return extension.ToLowerInvariant();
    }

    public static string SanitizeDisplayFileName(string originalFileName)
    {
        var name = Path.GetFileName(originalFileName.Trim());
        if (name.Length > 255)
            name = name[..255];
        return name;
    }
}
