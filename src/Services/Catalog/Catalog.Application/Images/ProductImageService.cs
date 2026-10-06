using Commerce.BuildingBlocks.Domain.Results;

namespace Catalog.Application.Images;

/// <summary>Describes an immutable image stored by Catalog.</summary>
/// <param name="ImageUrl">Same-origin public image path.</param>
/// <param name="ContentType">Validated image media type.</param>
/// <param name="Length">Actual stored byte count.</param>
/// <param name="Sha256">Content checksum.</param>
public sealed record ProductImageResponse(string ImageUrl, string ContentType, long Length, string Sha256);

/// <summary>Stores bounded image streams without depending on HTTP or a particular storage provider.</summary>
public interface IProductImageStore
{
    /// <summary>Atomically stores an image using its content checksum as its identity.</summary>
    /// <param name="source">Remaining request stream after the validated prefix.</param>
    /// <param name="prefix">Already validated first bytes.</param>
    /// <param name="extension">Validated file extension.</param>
    /// <param name="contentType">Validated image type.</param>
    /// <param name="expectedLength">Validated declared byte count.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The stored image or a length validation failure.</returns>
    Task<Result<ProductImageResponse>> SaveAsync(Stream source, byte[] prefix, string extension,
        string contentType, long expectedLength, CancellationToken cancellationToken);
}

/// <summary>Defines validated product photo upload use cases.</summary>
public interface IProductImageService
{
    /// <summary>Validates image size, signatures and dimensions before storage.</summary>
    /// <param name="source">Request file stream.</param>
    /// <param name="contentType">Declared file media type.</param>
    /// <param name="length">Declared byte count.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The stored image or safe validation details.</returns>
    Task<Result<ProductImageResponse>> UploadAsync(Stream source, string contentType, long length,
        CancellationToken cancellationToken);
}

/// <summary>Allows raster photos only and bounds both declared and actual upload size.</summary>
public sealed class ProductImageService : IProductImageService
{
    /// <summary>Maximum photo size, five mebibytes.</summary>
    public const long MaximumBytes = 5 * 1024 * 1024;
    private readonly IProductImageStore _store;

    /// <summary>Initializes validated image uploads.</summary>
    /// <param name="store">Catalog image storage abstraction.</param>
    public ProductImageService(IProductImageStore store) => _store = store;

    /// <inheritdoc />
    public async Task<Result<ProductImageResponse>> UploadAsync(Stream source, string contentType,
        long length, CancellationToken cancellationToken)
    {
        if (length < 32 || length > MaximumBytes)
            return Error.Validation("Catalog.ImageSize", "Choose a JPEG, PNG or WebP photo up to 5 MB.");
        byte[] prefix = new byte[32];
        int read = await source.ReadAtLeastAsync(prefix, prefix.Length, throwOnEndOfStream: false, cancellationToken);
        string? extension = DetectFormat(prefix.AsSpan(0, read), contentType);
        // Never serve SVG/HTML or trust a browser-supplied MIME type or original filename.
        if (extension is null)
            return Error.Validation("Catalog.ImageFormat", "The file must be a JPEG, PNG or WebP photo matching its file type.");
        return await _store.SaveAsync(source, prefix, extension, contentType, length, cancellationToken);
    }

    /// <summary>Checks raster signatures and the structural header needed to identify the declared format.</summary>
    /// <param name="header">First 32 image bytes.</param>
    /// <param name="contentType">Declared media type.</param>
    /// <returns>The safe extension or null when the signature does not match.</returns>
    public static string? DetectFormat(ReadOnlySpan<byte> header, string contentType)
    {
        if (header.Length < 32) return null;
        if (contentType == "image/jpeg" && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff)
            return "jpg";
        if (contentType == "image/png" && header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) &&
            header[12..16].SequenceEqual("IHDR"u8) &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[16..20]) is > 0 and <= 12000 &&
            System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(header[20..24]) is > 0 and <= 12000)
            return "png";
        if (contentType == "image/webp" && header[..4].SequenceEqual("RIFF"u8) &&
            header[8..12].SequenceEqual("WEBP"u8) &&
            (header[12..16].SequenceEqual("VP8 "u8) || header[12..16].SequenceEqual("VP8L"u8) || header[12..16].SequenceEqual("VP8X"u8)))
            return "webp";
        return null;
    }
}
