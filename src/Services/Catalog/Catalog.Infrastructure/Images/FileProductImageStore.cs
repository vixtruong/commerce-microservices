using System.Security.Cryptography;
using Catalog.Application.Images;
using Commerce.BuildingBlocks.Domain.Results;

namespace Catalog.Infrastructure.Images;

/// <summary>Configures the durable directory shared by Catalog instances.</summary>
public sealed class ProductImageStorageOptions
{
    /// <summary>Absolute storage directory, mounted on a shared volume in Compose.</summary>
    public string DirectoryPath { get; init; } = string.Empty;
}

/// <summary>Writes immutable photos with bounded buffers and atomic publication.</summary>
public sealed class FileProductImageStore : IProductImageStore
{
    private readonly string _directory;

    /// <summary>Initializes the operator-configured storage directory.</summary>
    /// <param name="options">Shared image directory configuration.</param>
    public FileProductImageStore(ProductImageStorageOptions options)
    {
        _directory = Path.GetFullPath(options.DirectoryPath);
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public async Task<Result<ProductImageResponse>> SaveAsync(Stream source, byte[] prefix, string extension,
        string contentType, long expectedLength, CancellationToken cancellationToken)
    {
        string temporary = Path.Combine(_directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            using IncrementalHash checksum = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = prefix.Length;
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous))
            {
                await output.WriteAsync(prefix, cancellationToken);
                checksum.AppendData(prefix);
                byte[] buffer = new byte[64 * 1024];
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > ProductImageService.MaximumBytes || total > expectedLength)
                        return Error.Validation("Catalog.ImageSize", "The actual photo exceeds its declared size or the 5 MB limit.");
                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    checksum.AppendData(buffer, 0, read);
                }
                if (total != expectedLength)
                    return Error.Validation("Catalog.ImageSize", "The photo upload was incomplete.");
                await output.FlushAsync(cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            string sha256 = Convert.ToHexStringLower(checksum.GetHashAndReset());
            string filename = $"upload-{sha256}.{extension}";
            string destination = Path.Combine(_directory, filename);
            // Concurrent uploads of the same content share one immutable asset across both Catalog instances.
            try { File.Move(temporary, destination, overwrite: false); }
            catch (IOException) when (File.Exists(destination)) { File.Delete(temporary); }
            return new ProductImageResponse($"/api/catalog/media/{filename}", contentType, total, sha256);
        }
        finally
        {
            // Cancelled, rejected or disconnected uploads must not leave partial publicly accessible files.
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }
}
