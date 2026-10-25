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
[Route("api/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly TrustVaultDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDocumentAuthorizationService _authService;
    private readonly IFileStorageService _storageService;
    private readonly IAuditService _auditService;

    public DocumentsController(
        TrustVaultDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IDocumentAuthorizationService authService,
        IFileStorageService storageService,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _authService = authService;
        _storageService = storageService;
        _auditService = auditService;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;
        return await _userManager.FindByIdAsync(userId);
    }

    [HttpGet]
    public async Task<IActionResult> GetDocuments([FromQuery] DocumentSearchQuery query)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive)
        {
            return Unauthorized();
        }

        // Security boundary: A user can discover ONLY documents they own or have an active grant for
        // Administrators do NOT implicitly see tenant documents in document listings
        var baseQuery = _dbContext.Documents
            .Include(d => d.Owner)
            .Include(d => d.Versions)
            .Include(d => d.Grants)
            .Where(d => d.Status == DocumentStatus.Active &&
                        (d.OwnerId == user.Id ||
                         d.Grants.Any(g => g.UserId == user.Id && g.IsActive && g.RevokedAtUtc == null)));

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            var q = query.Query.Trim().ToLower();
            baseQuery = baseQuery.Where(d => 
                d.Title.ToLower().Contains(q) || 
                d.OriginalFileName.ToLower().Contains(q) ||
                (d.Description != null && d.Description.ToLower().Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            baseQuery = baseQuery.Where(d => d.Category.ToLower() == query.Category.Trim().ToLower());
        }

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            baseQuery = baseQuery.Where(d => d.Tags.ToLower().Contains(query.Tag.Trim().ToLower()));
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.CreatedAtUtc >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(d => d.CreatedAtUtc <= query.ToDate.Value);
        }

        var totalCount = await baseQuery.CountAsync();

        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var documents = await baseQuery
            .OrderByDescending(d => d.UpdatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(d => new DocumentDto
            {
                Id = d.Id,
                Title = d.Title,
                OriginalFileName = d.OriginalFileName,
                Description = d.Description,
                Category = d.Category,
                Tags = d.Tags,
                Status = d.Status,
                CurrentVersionNumber = d.CurrentVersionNumber,
                CreatedAtUtc = d.CreatedAtUtc,
                UpdatedAtUtc = d.UpdatedAtUtc,
                OwnerId = d.OwnerId,
                OwnerName = d.Owner != null ? d.Owner.FullName : "Unknown",
                FileSizeBytes = d.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.FileSizeBytes).FirstOrDefault(),
                ContentType = d.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.ContentType).FirstOrDefault() ?? "application/octet-stream",
                Sha256Hash = d.Versions.OrderByDescending(v => v.VersionNumber).Select(v => v.Sha256Hash).FirstOrDefault() ?? string.Empty,
                IsOwner = d.OwnerId == user.Id,
                PermissionLevel = d.OwnerId == user.Id 
                    ? DocumentPermission.Edit 
                    : d.Grants.Where(g => g.UserId == user.Id && g.IsActive && g.RevokedAtUtc == null).Select(g => (DocumentPermission?)g.Permission).FirstOrDefault()
            })
            .ToListAsync();

        return Ok(new PagedResult<DocumentDto>
        {
            Items = documents,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    [HttpPost]
    [RequestSizeLimit(30 * 1024 * 1024)] // 30MB bound
    public async Task<IActionResult> UploadDocument([FromForm] CreateDocumentRequest request, IFormFile? file)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        if (file == null || file.Length == 0)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.UploadValidationFailed,
                user.Id.ToString(),
                null,
                AuditResults.Failed,
                new { Reason = "Empty or missing file payload" },
                clientIp,
                correlationId);

            return BadRequest(new { message = "No valid file uploaded." });
        }

        // Validate and store atomically via IFileStorageService
        await using var stream = file.OpenReadStream();
        var saveResult = await _storageService.SaveFileAsync(stream, file.FileName, file.ContentType);

        if (!saveResult.Success)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.UploadValidationFailed,
                user.Id.ToString(),
                null,
                AuditResults.Failed,
                new { FileName = file.FileName, Reason = saveResult.ErrorMessage },
                clientIp,
                correlationId);

            return BadRequest(new { message = saveResult.ErrorMessage });
        }

        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = user.Id,
            Title = string.IsNullOrWhiteSpace(request.Title) ? Path.GetFileNameWithoutExtension(file.FileName) : request.Title.Trim(),
            OriginalFileName = Path.GetFileName(file.FileName),
            Description = request.Description?.Trim(),
            Category = string.IsNullOrWhiteSpace(request.Category) ? "General" : request.Category.Trim(),
            Tags = request.Tags ?? string.Empty,
            Status = DocumentStatus.Active,
            CurrentVersionNumber = 1,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var initialVersion = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = document.Id,
            VersionNumber = 1,
            StorageKey = saveResult.StorageKey!,
            FileSizeBytes = saveResult.FileSizeBytes,
            ContentType = saveResult.DetectedContentType,
            Sha256Hash = saveResult.Sha256Hash,
            UploadedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        document.Versions.Add(initialVersion);

        _dbContext.Documents.Add(document);
        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentUploaded,
            user.Id.ToString(),
            document.Id.ToString(),
            AuditResults.Success,
            new 
            { 
                FileName = document.OriginalFileName, 
                FileSize = saveResult.FileSizeBytes, 
                Hash = saveResult.Sha256Hash 
            },
            clientIp,
            correlationId);

        return CreatedAtAction(nameof(GetDocumentById), new { id = document.Id }, new DocumentDto
        {
            Id = document.Id,
            Title = document.Title,
            OriginalFileName = document.OriginalFileName,
            Description = document.Description,
            Category = document.Category,
            Tags = document.Tags,
            Status = document.Status,
            CurrentVersionNumber = 1,
            CreatedAtUtc = document.CreatedAtUtc,
            UpdatedAtUtc = document.UpdatedAtUtc,
            OwnerId = user.Id,
            OwnerName = user.FullName,
            FileSizeBytes = saveResult.FileSizeBytes,
            ContentType = saveResult.DetectedContentType,
            Sha256Hash = saveResult.Sha256Hash,
            IsOwner = true,
            PermissionLevel = DocumentPermission.Edit
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetDocumentById(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeAsync(user, id, DocumentPermission.Read);
        if (!authDecision.IsAllowed)
        {
            // Uniform 404 response hides existence from IDOR attackers
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "GetDocumentById" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found." });
        }

        var doc = authDecision.Document!;

        var versions = await _dbContext.DocumentVersions
            .Where(v => v.DocumentId == id)
            .Include(v => v.UploadedByUser)
            .OrderByDescending(v => v.VersionNumber)
            .Select(v => new DocumentVersionDto
            {
                Id = v.Id,
                VersionNumber = v.VersionNumber,
                FileSizeBytes = v.FileSizeBytes,
                ContentType = v.ContentType,
                Sha256Hash = v.Sha256Hash,
                UploadedByUserId = v.UploadedByUserId,
                UploadedByName = v.UploadedByUser != null ? v.UploadedByUser.FullName : "Unknown",
                CreatedAtUtc = v.CreatedAtUtc
            })
            .ToListAsync();

        var isOwner = doc.OwnerId == user.Id;
        List<DocumentGrantDto> grants = new();

        if (isOwner)
        {
            grants = await _dbContext.DocumentGrants
                .Where(g => g.DocumentId == id)
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
        }

        var latestVersion = versions.FirstOrDefault();

        var detail = new DocumentDetailDto
        {
            Id = doc.Id,
            Title = doc.Title,
            OriginalFileName = doc.OriginalFileName,
            Description = doc.Description,
            Category = doc.Category,
            Tags = doc.Tags,
            Status = doc.Status,
            CurrentVersionNumber = doc.CurrentVersionNumber,
            CreatedAtUtc = doc.CreatedAtUtc,
            UpdatedAtUtc = doc.UpdatedAtUtc,
            OwnerId = doc.OwnerId,
            OwnerName = doc.Owner?.FullName ?? "Unknown",
            FileSizeBytes = latestVersion?.FileSizeBytes ?? 0,
            ContentType = latestVersion?.ContentType ?? "application/octet-stream",
            Sha256Hash = latestVersion?.Sha256Hash ?? string.Empty,
            IsOwner = isOwner,
            PermissionLevel = isOwner ? DocumentPermission.Edit : DocumentPermission.Read,
            Versions = versions,
            Grants = grants
        };

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentViewed,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Title = doc.Title },
            clientIp,
            correlationId);

        return Ok(detail);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> DownloadDocument(Guid id, [FromQuery] int? versionNumber = null)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeAsync(user, id, DocumentPermission.Read);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "DownloadDocument" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found." });
        }

        var doc = authDecision.Document!;

        var versionQuery = _dbContext.DocumentVersions.Where(v => v.DocumentId == id);
        var targetVersion = versionNumber.HasValue 
            ? await versionQuery.FirstOrDefaultAsync(v => v.VersionNumber == versionNumber.Value)
            : await versionQuery.OrderByDescending(v => v.VersionNumber).FirstOrDefaultAsync();

        if (targetVersion == null)
        {
            return NotFound(new { message = "Requested document version not found." });
        }

        var stream = await _storageService.OpenReadStreamAsync(targetVersion.StorageKey);
        if (stream == null)
        {
            return NotFound(new { message = "Storage file unavailable." });
        }

        // Set security and anti-caching headers
        Response.Headers.Append("Cache-Control", "private, no-store, no-cache, must-revalidate");
        Response.Headers.Append("Pragma", "no-cache");
        Response.Headers.Append("X-Content-Type-Options", "nosniff");

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentDownloaded,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Version = targetVersion.VersionNumber, Hash = targetVersion.Sha256Hash },
            clientIp,
            correlationId);

        return File(stream, targetVersion.ContentType, doc.OriginalFileName, enableRangeProcessing: false);
    }

    [HttpPost("{id:guid}/versions")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> UploadNewVersion(Guid id, IFormFile? file)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        // Requires EDIT permission
        var authDecision = await _authService.AuthorizeAsync(user, id, DocumentPermission.Edit);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "UploadNewVersion" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found or permission denied." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "No valid file payload provided." });
        }

        await using var stream = file.OpenReadStream();
        var saveResult = await _storageService.SaveFileAsync(stream, file.FileName, file.ContentType);

        if (!saveResult.Success)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.UploadValidationFailed,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Failed,
                new { FileName = file.FileName, Reason = saveResult.ErrorMessage },
                clientIp,
                correlationId);

            return BadRequest(new { message = saveResult.ErrorMessage });
        }

        var doc = await _dbContext.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        var nextVersionNumber = doc.CurrentVersionNumber + 1;
        doc.CurrentVersionNumber = nextVersionNumber;
        doc.UpdatedAtUtc = DateTime.UtcNow;

        var version = new DocumentVersion
        {
            Id = Guid.NewGuid(),
            DocumentId = doc.Id,
            VersionNumber = nextVersionNumber,
            StorageKey = saveResult.StorageKey!,
            FileSizeBytes = saveResult.FileSizeBytes,
            ContentType = saveResult.DetectedContentType,
            Sha256Hash = saveResult.Sha256Hash,
            UploadedByUserId = user.Id,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.DocumentVersions.Add(version);
        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentVersionAdded,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Version = nextVersionNumber, Hash = saveResult.Sha256Hash },
            clientIp,
            correlationId);

        return Ok(new DocumentVersionDto
        {
            Id = version.Id,
            VersionNumber = nextVersionNumber,
            FileSizeBytes = saveResult.FileSizeBytes,
            ContentType = saveResult.DetectedContentType,
            Sha256Hash = saveResult.Sha256Hash,
            UploadedByUserId = user.Id,
            UploadedByName = user.FullName,
            CreatedAtUtc = version.CreatedAtUtc
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateMetadata(Guid id, [FromBody] UpdateDocumentMetadataRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeAsync(user, id, DocumentPermission.Edit);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "UpdateMetadata" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found or permission denied." });
        }

        var doc = await _dbContext.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.Title = request.Title.Trim();
        doc.Description = request.Description?.Trim();
        doc.Category = request.Category.Trim();
        doc.Tags = request.Tags.Trim();
        doc.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentMetadataUpdated,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Title = doc.Title, Category = doc.Category },
            clientIp,
            correlationId);

        return Ok(new { message = "Document metadata updated successfully." });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        // Only OWNER can delete their own document
        var authDecision = await _authService.AuthorizeOwnershipAsync(user, id);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "DeleteDocument" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found or permission denied." });
        }

        var doc = await _dbContext.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.Status = DocumentStatus.Deleted;
        doc.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentDeleted,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Title = doc.Title },
            clientIp,
            correlationId);

        return Ok(new { message = "Document soft-deleted successfully according to retention policy." });
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> RestoreDocument(Guid id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !user.IsActive) return Unauthorized();

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var authDecision = await _authService.AuthorizeOwnershipAsync(user, id);
        if (!authDecision.IsAllowed)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AccessDenied,
                user.Id.ToString(),
                id.ToString(),
                AuditResults.Denied,
                new { Reason = authDecision.DenialReason, Endpoint = "RestoreDocument" },
                clientIp,
                correlationId);

            return NotFound(new { message = "Document not found or permission denied." });
        }

        var doc = await _dbContext.Documents.FindAsync(id);
        if (doc == null) return NotFound();

        doc.Status = DocumentStatus.Active;
        doc.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.DocumentRestored,
            user.Id.ToString(),
            doc.Id.ToString(),
            AuditResults.Success,
            new { Title = doc.Title },
            clientIp,
            correlationId);

        return Ok(new { message = "Document restored to active status." });
    }
}
