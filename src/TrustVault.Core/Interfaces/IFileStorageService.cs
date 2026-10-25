namespace TrustVault.Core.Interfaces;

public class FileSaveResult
{
    public bool Success { get; set; }
    public string? StorageKey { get; set; }
    public long FileSizeBytes { get; set; }
    public string DetectedContentType { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}

public interface IFileStorageService
{
    Task<FileSaveResult> SaveFileAsync(
        Stream contentStream, 
        string originalFileName, 
        string declaredContentType, 
        CancellationToken cancellationToken = default);

    Task<Stream?> OpenReadStreamAsync(string storageKey, CancellationToken cancellationToken = default);

    Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default);
}
