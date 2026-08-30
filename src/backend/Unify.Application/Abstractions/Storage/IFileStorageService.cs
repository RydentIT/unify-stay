namespace Unify.Application.Abstractions.Storage;

public sealed record StoredFile(string Path, string ContentType, long SizeBytes, string OriginalFileName);

public sealed record FileToStore(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long SizeBytes);

/// <summary>
/// Storage for verification documents and avatars.
///
/// Deliberately expressed in terms of an opaque path rather than a URL or a disk location, so
/// the local-disk implementation can be swapped for S3 or Azure Blob by writing a new class
/// rather than by changing any caller.
/// </summary>
public interface IFileStorageService
{
    /// <param name="container">Logical grouping, e.g. "verification-documents" or "avatars".</param>
    Task<StoredFile> SaveAsync(
        string container,
        FileToStore file,
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default);

    Task DeleteAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>A URL the browser can fetch. For local disk this is a relative API path.</summary>
    string GetPublicUrl(string path);
}
