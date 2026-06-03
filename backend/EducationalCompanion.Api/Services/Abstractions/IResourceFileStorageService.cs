namespace EducationalCompanion.Api.Services.Abstractions;

public interface IResourceFileStorageService
{
    Task<string> SaveAsync(Stream content, string storageKey, CancellationToken ct = default);

    Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default);

    Task DeleteAsync(string storageKey, CancellationToken ct = default);
}
