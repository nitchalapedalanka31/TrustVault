using System.ComponentModel.DataAnnotations;
using TrustVault.Core.Enums;

namespace TrustVault.Core.DTOs;

public class DocumentDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public DocumentStatus Status { get; set; }
    public int CurrentVersionNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    public Guid OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public DocumentPermission? PermissionLevel { get; set; }
}

public class DocumentDetailDto : DocumentDto
{
    public List<DocumentVersionDto> Versions { get; set; } = new();
    public List<DocumentGrantDto> Grants { get; set; } = new();
}

public class DocumentVersionDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public long FileSizeBytes { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string Sha256Hash { get; set; } = string.Empty;
    public Guid UploadedByUserId { get; set; }
    public string UploadedByName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateDocumentRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(200)]
    public string Tags { get; set; } = string.Empty;
}

public class UpdateDocumentMetadataRequest
{
    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(100)]
    public string Category { get; set; } = "General";

    [MaxLength(200)]
    public string Tags { get; set; } = string.Empty;
}

public class DocumentSearchQuery
{
    public string? Query { get; set; }
    public string? Category { get; set; }
    public string? Tag { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
