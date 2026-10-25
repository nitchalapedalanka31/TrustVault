namespace TrustVault.Core.Entities;

public class DocumentVersion
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public virtual Document? Document { get; set; }

    public int VersionNumber { get; set; }
    public string StorageKey { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public string Sha256Hash { get; set; } = string.Empty;

    public Guid UploadedByUserId { get; set; }
    public virtual ApplicationUser? UploadedByUser { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
