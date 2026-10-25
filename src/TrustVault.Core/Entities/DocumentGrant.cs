using TrustVault.Core.Enums;

namespace TrustVault.Core.Entities;

public class DocumentGrant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid DocumentId { get; set; }
    public virtual Document? Document { get; set; }

    public Guid UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }

    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;

    public Guid GrantedByUserId { get; set; }
    public virtual ApplicationUser? GrantedByUser { get; set; }

    public DateTime GrantedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
}
