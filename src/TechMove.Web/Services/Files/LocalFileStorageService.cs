namespace TechMove.Web.Services.Files;

public sealed class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    /// <summary>Folder (relative to the project root) that simulates the file server. Outside wwwroot on purpose.</summary>
    public string RootPath { get; set; } = "App_Data/agreements";
}

public sealed record StoredFile(string OriginalFileName, string StoredFileName, long Size);

public interface IFileStorageService
{
    Task<StoredFile> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default);
    Stream OpenRead(string storedFileName);
    bool Exists(string storedFileName);
    void Delete(string storedFileName);
}

/// <summary>
/// Simulated file server. Files are saved under a GUID name so two uploads called
/// "agreement.pdf" never overwrite each other; only the GUID name is stored in the database.
/// Files live outside wwwroot so they can only be downloaded through the controller.
/// </summary>
public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _root;

    public LocalFileStorageService(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            throw new ArgumentException("A storage folder is required.", nameof(rootPath));
        }

        _root = Path.GetFullPath(rootPath);
        Directory.CreateDirectory(_root);
    }

    public string RootPath => _root;

    public async Task<StoredFile> SaveAsync(Stream content, string originalFileName, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = ResolvePath(storedName);

        await using (var target = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await content.CopyToAsync(target, ct);
        }

        var size = new FileInfo(fullPath).Length;
        return new StoredFile(Path.GetFileName(originalFileName), storedName, size);
    }

    public Stream OpenRead(string storedFileName)
    {
        var fullPath = ResolvePath(storedFileName);
        if (!File.Exists(fullPath))
        {
            throw new NotFoundException("The requested file could not be found on the file server.");
        }

        return new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
    }

    public bool Exists(string storedFileName) => File.Exists(ResolvePath(storedFileName));

    public void Delete(string storedFileName)
    {
        var fullPath = ResolvePath(storedFileName);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }

    /// <summary>Blocks path traversal ("..\\..\\web.config") - only plain file names inside the root are allowed.</summary>
    private string ResolvePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName) ||
            Path.GetFileName(storedFileName) != storedFileName ||
            storedFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Invalid stored file name.", nameof(storedFileName));
        }

        return Path.Combine(_root, storedFileName);
    }
}
