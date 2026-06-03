using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Domain.Exceptions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace EducationalCompanion.Tests.Tests.Unit.Ingestion;

public class LocalResourceFileStorageServiceTests
{
    [Fact]
    public async Task SaveAsync_AcceptsResourceSubfolderStorageKey()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ecs-storage-{Guid.NewGuid():N}");
        var env = new FakeHostEnvironment(root);
        var service = new LocalResourceFileStorageService(
            Options.Create(new ResourceFilesOptions { StoragePath = root }),
            env);

        var storageKey = $"{Guid.NewGuid():N}_{Guid.NewGuid():N}.md";
        await using var content = new MemoryStream("hello"u8.ToArray());
        await service.SaveAsync(content, storageKey, CancellationToken.None);

        var fullPath = Path.Combine(root, storageKey.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(fullPath));

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_RejectsPathTraversal()
    {
        var root = Path.Combine(Path.GetTempPath(), $"ecs-storage-{Guid.NewGuid():N}");
        var env = new FakeHostEnvironment(root);
        var service = new LocalResourceFileStorageService(
            Options.Create(new ResourceFilesOptions { StoragePath = root }),
            env);

        await using var content = new MemoryStream("x"u8.ToArray());
        await Assert.ThrowsAsync<ValidationException>(() =>
            service.SaveAsync(content, "../escape.txt", CancellationToken.None));

        if (Directory.Exists(root))
            Directory.Delete(root, recursive: true);
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string contentRootPath) => ContentRootPath = contentRootPath;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
