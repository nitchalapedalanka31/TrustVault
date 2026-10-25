using System.Security.Cryptography;
using Microsoft.Extensions.Configuration;
using TrustVault.Core.Interfaces;

namespace TrustVault.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    private readonly string _storagePath;
    private readonly long _maxFileSize;

    // Strict allowlist of extensions and expected MIME signatures
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg", ".txt", ".csv", ".json", ".docx", ".xlsx"
    };

    // Magic byte signatures for reliable header sniffing
    private static readonly byte[] PdfMagic = [0x25, 0x50, 0x44, 0x46]; // %PDF
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47]; // .PNG
    private static readonly byte[] JpgMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04]; // PK zip (used by docx, xlsx)

    public LocalFileStorageService(IConfiguration configuration)
    {
        var configuredPath = configuration["Storage:LocalPath"];
        if (string.IsNullOrWhiteSpace(configuredPath))
        {
            // Fallback default: dedicated vault storage outside web root
            var baseDir = AppContext.BaseDirectory;
            _storagePath = Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "vault_storage"));
        }
        else
        {
            _storagePath = Path.GetFullPath(configuredPath);
        }

        Directory.CreateDirectory(_storagePath);

        _maxFileSize = long.TryParse(configuration["Storage:MaxFileSizeBytes"], out var max) 
            ? max 
            : 25 * 1024 * 1024L; // 25 MB default
    }

    public async Task<FileSaveResult> SaveFileAsync(
        Stream contentStream, 
        string originalFileName, 
        string declaredContentType, 
        CancellationToken cancellationToken = default)
    {
        var result = new FileSaveResult();

        // 1. Sanitize original filename and check extension against strict allowlist
        var cleanFileName = Path.GetFileName(originalFileName);
        var extension = Path.GetExtension(cleanFileName);

        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            result.Success = false;
            result.ErrorMessage = $"Disallowed file extension '{extension}'. Only authorized secure documents (PDF, PNG, JPG, TXT, CSV, JSON, DOCX, XLSX) are permitted.";
            return result;
        }

        // 2. Prepare atomic temporary file in private storage
        var tempFileName = $"{Guid.NewGuid():N}.tmp";
        var tempFilePath = Path.Combine(_storagePath, tempFileName);

        var finalStorageKey = $"{Guid.NewGuid():N}.vault";
        var finalFilePath = Path.Combine(_storagePath, finalStorageKey);

        long totalBytes = 0;
        using var sha256 = SHA256.Create();

        try
        {
            byte[] headerBuffer = new byte[16];
            int headerBytesRead = 0;

            await using (var fileStream = new FileStream(tempFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                byte[] buffer = new byte[81920]; // 80KB buffer
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                {
                    totalBytes += bytesRead;
                    if (totalBytes > _maxFileSize)
                    {
                        result.Success = false;
                        result.ErrorMessage = $"Uploaded file exceeds maximum permitted size of {_maxFileSize / (1024 * 1024)} MB.";
                        return result;
                    }

                    // Capture magic header bytes from first buffer
                    if (headerBytesRead < headerBuffer.Length)
                    {
                        int toCopy = Math.Min(bytesRead, headerBuffer.Length - headerBytesRead);
                        Array.Copy(buffer, 0, headerBuffer, headerBytesRead, toCopy);
                        headerBytesRead += toCopy;
                    }

                    sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                }

                sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            }

            if (totalBytes == 0)
            {
                result.Success = false;
                result.ErrorMessage = "Empty file uploads are not permitted.";
                return result;
            }

            // 3. Inspect magic bytes for content verification
            var detectedContentType = ValidateMagicBytes(extension, headerBuffer, headerBytesRead);
            if (detectedContentType == null)
            {
                result.Success = false;
                result.ErrorMessage = $"File content signature does not match declared extension '{extension}'. Potential polyglot or disguised executable blocked.";
                return result;
            }

            // 4. Atomically move temporary file to final storage key
            File.Move(tempFilePath, finalFilePath);

            result.Success = true;
            result.StorageKey = finalStorageKey;
            result.FileSizeBytes = totalBytes;
            result.DetectedContentType = detectedContentType;
            result.Sha256Hash = Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.ErrorMessage = $"Storage failure: {ex.Message}";
            return result;
        }
        finally
        {
            // Guarantee cleanup of temporary or abandoned file
            if (File.Exists(tempFilePath))
            {
                try { File.Delete(tempFilePath); } catch { /* best effort cleanup */ }
            }
        }
    }

    public Task<Stream?> OpenReadStreamAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        // Prevent path traversal
        var safeKey = Path.GetFileName(storageKey);
        var filePath = Path.Combine(_storagePath, safeKey);

        if (!File.Exists(filePath))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        return Task.FromResult<Stream?>(stream);
    }

    public Task DeleteFileAsync(string storageKey, CancellationToken cancellationToken = default)
    {
        var safeKey = Path.GetFileName(storageKey);
        var filePath = Path.Combine(_storagePath, safeKey);

        if (File.Exists(filePath))
        {
            try { File.Delete(filePath); } catch { /* best effort */ }
        }

        return Task.CompletedTask;
    }

    private static string? ValidateMagicBytes(string extension, byte[] header, int bytesRead)
    {
        if (bytesRead < 4) return null;

        var ext = extension.ToLowerInvariant();

        switch (ext)
        {
            case ".pdf":
                if (header.Take(4).SequenceEqual(PdfMagic)) return "application/pdf";
                return null;

            case ".png":
                if (bytesRead >= 4 && header.Take(4).SequenceEqual(PngMagic)) return "image/png";
                return null;

            case ".jpg":
            case ".jpeg":
                if (header.Take(3).SequenceEqual(JpgMagic)) return "image/jpeg";
                return null;

            case ".docx":
                if (header.Take(4).SequenceEqual(ZipMagic)) 
                    return "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
                return null;

            case ".xlsx":
                if (header.Take(4).SequenceEqual(ZipMagic)) 
                    return "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
                return null;

            case ".txt":
            case ".csv":
            case ".json":
                // Verify text does not contain null binary bytes
                for (int i = 0; i < bytesRead; i++)
                {
                    if (header[i] == 0x00) return null; // Null byte indicates binary content
                }
                return ext switch
                {
                    ".txt" => "text/plain",
                    ".csv" => "text/csv",
                    ".json" => "application/json",
                    _ => "text/plain"
                };

            default:
                return null;
        }
    }
}
