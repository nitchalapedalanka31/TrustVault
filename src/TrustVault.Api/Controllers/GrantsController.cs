using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustVault.Core.DTOs;
using TrustVault.Core.Entities;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Api.Controllers;

[ApiController]
[Route("api/documents/{documentId:guid}/[controller]")]
[Authorize]
public class GrantsController : ControllerBase
{
    private readonly TrustVaultDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDocumentAuthorizationService _authService;
    private readonly IAuditService _auditService;
    private readonly IAiSuggestionService _aiSuggestionService;

    public GrantsController(
        TrustVaultDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IDocumentAuthorizationService authService,
        IAuditService auditService,
        IAiSuggestionService aiSuggestionService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _authService = authService;
        _auditService = auditService;
        _aiSuggestionService = aiSuggestionService;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;
        return await _userManager.FindByIdAsync(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetGrants(Guid documentId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        // Security boundary: Only document owner may inspect or manage grants
        var authDecision = await _authService.AuthorizeOwnershipAsync(user, documentId);
        if (!authDecision.IsAllowed)
        {
            return NotFound(new { message = "Document not found or ownership required." });
        }

        var grants = await _dbContext.DocumentGrants
            .Where(g => g.DocumentId == documentId && g.IsActive)
            .Include(g => g.User)
            .OrderByDescending(g => g.GrantedAtUtc)
            .Select(g => new DocumentGrantDto
            {
                Id = g.Id,
                DocumentId = g.DocumentId,
                UserId = g.UserId,
                UserFullName = g.User != null ? g.User.FullName : "Unknown",
                UserEmail = g.User != null ? g.User.Email! : "Unknown",
                Permission = g.Permission,
                GrantedAtUtc = g.GrantedAtUtc,
                RevokedAtUtc = g.RevokedAtUtc,
                IsActive = g.IsActive
            })
            .ToListAsync();

        return Ok(grants);
    }

    [HttpPost]
    public async Task<IActionResult> CreateGrant(Guid documentId, [FromBody] GrantPermissionRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeOwnershipAsync(user, documentId);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                documentId.ToString(),
                AuditResults.Denied,
                new { Reason = "Attempted unauthorized grant management" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found or ownership required." });
        }

        var recipient = await _userManager.FindByEmailAsync(request.UserEmail.Trim());

        // Anti-enumeration defense: Provide safe uniform response
        if (recipient == null || !recipient.IsActive)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.GrantCreated,
                user.Id.ToString(),
                documentId.ToString(),
                AuditResults.Failed,
                new { Target = request.UserEmail, Reason = "Target user invalid or inactive" },
                clientIp,
                correlationId);

            return BadRequest(new { message = "Cannot grant access: specified user is invalid or not active." });
        }

        if (recipient.Id == user.Id)
        {
            return BadRequest(new { message = "Owner already retains full ownership rights." });
        }

        // Deactivate any existing active grant for this user/document to maintain single authoritative record
        var existingGrants = await _dbContext.DocumentGrants
            .Where(g => g.DocumentId == documentId && g.UserId == recipient.Id && g.IsActive)
            .ToListAsync();

        foreach (var eg in existingGrants)
        {
            eg.IsActive = false;
            eg.RevokedAtUtc = DateTime.UtcNow;
        }

        var newGrant = new DocumentGrant
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = recipient.Id,
            Permission = request.Permission,
            GrantedByUserId = user.Id,
            GrantedAtUtc = DateTime.UtcNow,
            IsActive = true
        };

        _dbContext.DocumentGrants.Add(newGrant);
        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.GrantCreated,
            user.Id.ToString(),
            documentId.ToString(),
            AuditResults.Success,
            new 
            { 
                GrantId = newGrant.Id, 
                RecipientId = recipient.Id, 
                RecipientEmail = recipient.Email, 
                Permission = request.Permission.ToString() 
            },
            clientIp,
            correlationId);

        return Ok(new DocumentGrantDto
        {
            Id = newGrant.Id,
            DocumentId = newGrant.DocumentId,
            UserId = recipient.Id,
            UserFullName = recipient.FullName,
            UserEmail = recipient.Email!,
            Permission = newGrant.Permission,
            GrantedAtUtc = newGrant.GrantedAtUtc,
            IsActive = true
        });
    }

    [HttpDelete("{grantId:guid}")]
    public async Task<IActionResult> RevokeGrant(Guid documentId, Guid grantId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeOwnershipAsync(user, documentId);
        if (!authDecision.IsAllowed)
        {
            return NotFound(new { message = "Document not found or ownership required." });
        }

        var grant = await _dbContext.DocumentGrants
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.Id == grantId && g.DocumentId == documentId);

        if (grant == null || !grant.IsActive)
        {
            return NotFound(new { message = "Active grant not found." });
        }

        // IMMEDIATE REVOCATION: Server-enforced access state change
        grant.IsActive = false;
        grant.RevokedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.GrantRevoked,
            user.Id.ToString(),
            documentId.ToString(),
            AuditResults.Success,
            new 
            { 
                GrantId = grant.Id, 
                RevokedUserId = grant.UserId,
                RevokedUserEmail = grant.User?.Email
            },
            clientIp,
            correlationId);

        return Ok(new { message = "Permission grant successfully revoked. Access denied immediately." });
    }

    [HttpGet("suggestions")]
    public async Task<IActionResult> GetRecipientSuggestions(Guid documentId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var authDecision = await _authService.AuthorizeOwnershipAsync(user, documentId);
        if (!authDecision.IsAllowed)
        {
            return NotFound(new { message = "Document not found or ownership required." });
        }

        var suggestions = await _aiSuggestionService.GetSuggestionsAsync(documentId, user.Id);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        await _auditService.RecordEventAsync(
            AuditActionTypes.AiSuggestionGenerated,
            user.Id.ToString(),
            documentId.ToString(),
            AuditResults.Success,
            new { SuggestionCount = suggestions.Count() },
            clientIp,
            correlationId);

        return Ok(suggestions);
    }
}
