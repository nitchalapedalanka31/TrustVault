using TrustVault.Core.Entities;
using TrustVault.Core.Enums;

namespace TrustVault.Core.Interfaces;

public class DocumentAuthDecision
{
    public bool IsAllowed { get; set; }
    public bool ResourceExists { get; set; }
    public string DenialReason { get; set; } = string.Empty;
    public Document? Document { get; set; }
}

public interface IDocumentAuthorizationService
{
    Task<DocumentAuthDecision> AuthorizeAsync(
        ApplicationUser? user, 
        Guid documentId, 
        DocumentPermission requiredPermission, 
        CancellationToken cancellationToken = default);

    Task<DocumentAuthDecision> AuthorizeOwnershipAsync(
        ApplicationUser? user, 
        Guid documentId, 
        CancellationToken cancellationToken = default);
}
