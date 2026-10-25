using TrustVault.Core.Enums;

namespace TrustVault.Core.Entities;

public class Document
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public virtual ApplicationUser? Owner { get; set; }

    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = "General";
    public string Tags { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; } = DocumentStatus.Active;

    public int CurrentVersionNumber { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<DocumentVersion> Versions { get; set; } = new List<DocumentVersion>();
    public virtual ICollection<DocumentGrant> Grants { get; set; } = new List<DocumentGrant>();
}
