using Microsoft.Extensions.Options;
using Unify.Application.Abstractions.Storage;

namespace Unify.Infrastructure.Storage;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Root directory for uploads. Relative paths resolve against the content root.</summary>
    public string RootPath { get; set; } = "App_Data/uploads";

    /// <summary>Route prefix the API serves stored files from.</summary>
    public string PublicUrlPrefix { get; set; } = "/api/files";

    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;

    /// <summary>
    /// Allow-list rather than a block-list. Verification documents only ever need these, and an
    /// allow-list cannot be defeated by an extension nobody thought to ban.
    /// </summary>
    public string[] AllowedContentTypes { get; set; } =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "application/pdf",
    ];
}

/// <summary>
/// Local-disk storage.
///
/// Structured so that swapping in S3 or Azure Blob later is a new class implementing
/// IFileStorageService rather than a rewrite: callers only ever see the opaque path returned by
/// SaveAsync, never a filesystem location.
/// </summary>
internal sealed class LocalFileStorageService : IFileStorageService
{
    private readonly FileStorageOptions _options;
    private readonly string _rootPath;

    public LocalFileStorageService(IOptions<FileStorageOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _rootPath = Path.GetFullPath(_options.RootPath);
        Directory.CreateDirectory(_rootPath);
    }

    public async Task<StoredFile> SaveAsync(
        string container,
        FileToStore file,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentException.ThrowIfNullOrWhiteSpace(container);

        if (file.SizeBytes > _options.MaxFileSizeBytes)
        {
            throw new InvalidOperationException(
                $"File exceeds the {_options.MaxFileSizeBytes} byte limit.");
        }

        if (!_options.AllowedContentTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Content type '{file.ContentType}' is not allowed.");
        }

        string safeContainer = SanitiseSegment(container);

        // The stored name is generated, never derived from what the user uploaded: the original
        // name is metadata only, so a crafted filename cannot influence where bytes land.
        string extension = GetSafeExtension(file.OriginalFileName);
        string storedName = $"{Guid.CreateVersion7():n}{extension}";
        string relativePath = $"{safeContainer}/{storedName}";

        string absoluteDirectory = Path.Combine(_rootPath, safeContainer);
        Directory.CreateDirectory(absoluteDirectory);

        string absolutePath = Path.Combine(absoluteDirectory, storedName);

        await using (FileStream destination = File.Create(absolutePath))
        {
            await file.Content.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        return new StoredFile(relativePath, file.ContentType, file.SizeBytes, file.OriginalFileName);
    }

    public Task<Stream?> OpenReadAsync(string path, CancellationToken cancellationToken = default)
    {
        if (!TryResolve(path, out string absolutePath) || !File.Exists(absolutePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        return Task.FromResult<Stream?>(File.OpenRead(absolutePath));
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        if (TryResolve(path, out string absolutePath) && File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicUrl(string path) => $"{_options.PublicUrlPrefix.TrimEnd('/')}/{path}";

    /// <summary>
    /// Resolves a stored path and refuses anything that escapes the root. Without this check a
    /// path such as "../../appsettings.json" would read arbitrary files off the server.
    /// </summary>
    private bool TryResolve(string path, out string absolutePath)
    {
        absolutePath = string.Empty;

        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        string candidate = Path.GetFullPath(Path.Combine(_rootPath, path));

        if (!candidate.StartsWith(_rootPath, StringComparison.Ordinal))
        {
            return false;
        }

        absolutePath = candidate;
        return true;
    }

    private static string SanitiseSegment(string segment) =>
        string.Concat(segment.Where(character => char.IsLetterOrDigit(character) || character is '-' or '_'));

    private static string GetSafeExtension(string originalFileName)
    {
        string extension = Path.GetExtension(originalFileName);

        if (string.IsNullOrEmpty(extension) || extension.Length > 10)
        {
            return string.Empty;
        }

        return string.Concat(extension.Where(character => char.IsLetterOrDigit(character) || character == '.'));
    }
}
