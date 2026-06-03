using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.Extensions.Options;

namespace EducationalCompanion.Api.Services.Implementations;

public class LocalResourceFileStorageService : IResourceFileStorageService
{
    private readonly string _rootPath;

    public LocalResourceFileStorageService(IOptions<ResourceFilesOptions> options, IHostEnvironment env)
    {
        var configured = options.Value.StoragePath;
        var root = Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
        _rootPath = Path.GetFullPath(root);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<string> SaveAsync(Stream content, string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var fileStream = new FileStream(
            fullPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None);

        await content.CopyToAsync(fileStream, ct);
        return storageKey;
    }

    public Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePath(storageKey);
        if (!File.Exists(fullPath))
            throw new NotFoundException("StoredResourceFile", storageKey);

        Stream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var fullPath = ResolveSafePath(storageKey);
        if (File.Exists(fullPath))
            File.Delete(fullPath);
        return Task.CompletedTask;
    }

    private string ResolveSafePath(string storageKey)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            throw new ValidationException("Invalid storage key.");

        if (Path.IsPathRooted(storageKey))
            throw new ValidationException("Invalid storage key.");

        var segments = storageKey
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (segments.Length == 0)
            throw new ValidationException("Invalid storage key.");

        foreach (var segment in segments)
        {
            if (segment is "." or ".." || segment.Contains("..", StringComparison.Ordinal))
                throw new ValidationException("Invalid storage key.");
        }

        var relativePath = string.Join(Path.DirectorySeparatorChar, segments);
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootPrefix = _rootPath.EndsWith(Path.DirectorySeparatorChar)
            ? _rootPath
            : _rootPath + Path.DirectorySeparatorChar;
        if (!combined.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("Invalid storage key.");

        return combined;
    }
}
