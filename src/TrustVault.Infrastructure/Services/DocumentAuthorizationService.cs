using Microsoft.EntityFrameworkCore;
using TrustVault.Core.Entities;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Infrastructure.Services;

public class DocumentAuthorizationService : IDocumentAuthorizationService
{
    private readonly TrustVaultDbContext _dbContext;

    public DocumentAuthorizationService(TrustVaultDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DocumentAuthDecision> AuthorizeAsync(
        ApplicationUser? user, 
        Guid documentId, 
        DocumentPermission requiredPermission, 
        CancellationToken cancellationToken = default)
    {
        var decision = new DocumentAuthDecision();

        // 1. Authenticated user must be active
        if (user == null || !user.IsActive)
        {
            decision.IsAllowed = false;
            decision.ResourceExists = false;
            decision.DenialReason = "User is not authenticated or account is deactivated.";
            return decision;
        }

        // 2. Query authoritative state directly from the database (never relying on stale cache)
        var document = await _dbContext.Documents
            .Include(d => d.Grants)
            .Include(d => d.Owner)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            decision.IsAllowed = false;
            decision.ResourceExists = false;
            decision.DenialReason = "Document does not exist.";
            return decision;
        }

        decision.ResourceExists = true;
        decision.Document = document;

        // 3. Document must be active (not soft-deleted or quarantined)
        if (document.Status == DocumentStatus.Deleted || document.Status == DocumentStatus.Quarantined)
        {
            // Unauthorized users should not even discover existence of deleted/quarantined documents
            if (document.OwnerId != user.Id)
            {
                decision.IsAllowed = false;
                decision.ResourceExists = false; // Hide existence from non-owners
                decision.DenialReason = "Document is deleted or quarantined.";
                return decision;
            }

            // Owner can see metadata for deleted/quarantined documents, but cannot read or edit content
            decision.IsAllowed = false;
            decision.DenialReason = $"Document is currently {document.Status}.";
            return decision;
        }

        // 4. Owner always retains full read and edit rights
        if (document.OwnerId == user.Id)
        {
            decision.IsAllowed = true;
            return decision;
        }

        // 5. Admin role check: Administrators do NOT automatically inherit document read/edit access
        // They must have an explicit active grant like any other viewer

        // 6. Check for an explicit active unrevoked grant
        var activeGrant = document.Grants.FirstOrDefault(g => 
            g.UserId == user.Id && 
            g.IsActive && 
            g.RevokedAtUtc == null);

        if (activeGrant == null)
        {
            // Fail-closed default: No grant means deny
            decision.IsAllowed = false;
            // Prevent IDOR or existence discovery for sensitive content endpoints
            decision.ResourceExists = false;
            decision.DenialReason = "No active permission grant exists for this user.";
            return decision;
        }

        // 7. Verify permission level
        if (requiredPermission == DocumentPermission.Read)
        {
            // Both Read and Edit grants satisfy Read
            decision.IsAllowed = true;
            return decision;
        }

        if (requiredPermission == DocumentPermission.Edit)
        {
            if (activeGrant.Permission == DocumentPermission.Edit)
            {
                decision.IsAllowed = true;
                return decision;
            }

            decision.IsAllowed = false;
            decision.DenialReason = "User has READ permission but EDIT permission is required.";
            return decision;
        }

        decision.IsAllowed = false;
        decision.DenialReason = "Unsupported permission check.";
        return decision;
    }

    public async Task<DocumentAuthDecision> AuthorizeOwnershipAsync(
        ApplicationUser? user, 
        Guid documentId, 
        CancellationToken cancellationToken = default)
    {
        var decision = new DocumentAuthDecision();

        if (user == null || !user.IsActive)
        {
            decision.IsAllowed = false;
            decision.ResourceExists = false;
            decision.DenialReason = "User is not authenticated or account is deactivated.";
            return decision;
        }

        var document = await _dbContext.Documents
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (document == null)
        {
            decision.IsAllowed = false;
            decision.ResourceExists = false;
            decision.DenialReason = "Document does not exist.";
            return decision;
        }

        decision.ResourceExists = true;
        decision.Document = document;

        if (document.OwnerId != user.Id)
        {
            decision.IsAllowed = false;
            decision.DenialReason = "Only the document owner may perform this action.";
            return decision;
        }

        decision.IsAllowed = true;
        return decision;
    }
}
