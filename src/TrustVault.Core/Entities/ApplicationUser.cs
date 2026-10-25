using Microsoft.AspNetCore.Identity;
using TrustVault.Core.Enums;

namespace TrustVault.Core.Entities;

public class ApplicationUser : IdentityUser<Guid>
{
    public string FullName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public virtual ICollection<Document> OwnedDocuments { get; set; } = new List<Document>();
    public virtual ICollection<DocumentGrant> ReceivedGrants { get; set; } = new List<DocumentGrant>();
    public virtual ICollection<DocumentGrant> IssuedGrants { get; set; } = new List<DocumentGrant>();
}
