using Catalog.Application.Images;
using Catalog.Infrastructure.Images;

namespace Catalog.Application.Tests;

/// <summary>Verifies raster validation, immutable shared storage and partial-upload cleanup.</summary>
public sealed class ProductImageTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "commerce-image-tests", Guid.NewGuid().ToString("N"));
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a8XcAAAAASUVORK5CYII=");

    /// <summary>Builds a fresh service instance sharing the test's isolated storage directory.</summary>
    /// <returns>A real stream-based image service.</returns>
    private ProductImageService CreateService() => new(new FileProductImageStore(new ProductImageStorageOptions { DirectoryPath = _directory }));

    /// <summary>Verifies identical photos retain one asset and are visible to a second Catalog storage instance.</summary>
    [Fact]
    public async Task UploadAsync_ValidPhoto_SharesImmutableChecksumPath()
    {
        var first = await CreateService().UploadAsync(new MemoryStream(Png), "image/png", Png.Length, CancellationToken.None);
        var second = await CreateService().UploadAsync(new MemoryStream(Png), "image/png", Png.Length, CancellationToken.None);
        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value.ImageUrl, second.Value.ImageUrl);
        Assert.Equal(64, first.Value.Sha256.Length);
        Assert.Single(Directory.GetFiles(_directory));
        Assert.Equal(Png, await File.ReadAllBytesAsync(Path.Combine(_directory, Path.GetFileName(first.Value.ImageUrl))));
    }

    /// <summary>Verifies a declared type must match the actual raster signature.</summary>
    [Fact]
    public async Task UploadAsync_MismatchedType_RejectsBeforeWriting()
    {
        var result = await CreateService().UploadAsync(new MemoryStream(Png), "image/jpeg", Png.Length, CancellationToken.None);
        Assert.True(result.IsFailure);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    /// <summary>Verifies oversized uploads are rejected without consuming or storing the file.</summary>
    [Fact]
    public async Task UploadAsync_OversizedPhoto_RejectsBeforeReading()
    {
        using var source = new MemoryStream(Png);
        Assert.True((await CreateService().UploadAsync(source, "image/png", ProductImageService.MaximumBytes + 1, CancellationToken.None)).IsFailure);
        Assert.Equal(0, source.Position);
    }

    /// <summary>Verifies incorrect lengths cannot publish partial assets.</summary>
    [Fact]
    public async Task UploadAsync_LengthMismatch_RemovesTemporaryFile()
    {
        Assert.True((await CreateService().UploadAsync(new MemoryStream(Png), "image/png", Png.Length - 1, CancellationToken.None)).IsFailure);
        Assert.Empty(Directory.GetFiles(_directory));
    }

    /// <summary>Verifies request cancellation propagates without publishing an image.</summary>
    [Fact]
    public async Task UploadAsync_Cancelled_PropagatesCancellation()
    {
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService().UploadAsync(
            new MemoryStream(Png), "image/png", Png.Length, new CancellationToken(true)));
        Assert.Empty(Directory.GetFiles(_directory));
    }

    /// <summary>Removes only the exact isolated directory created for this test instance.</summary>
    public void Dispose()
    {
        string parent = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "commerce-image-tests"));
        if (Path.GetDirectoryName(Path.GetFullPath(_directory)) == parent && Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }
}
