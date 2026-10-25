using System.ComponentModel.DataAnnotations;
using TrustVault.Core.Enums;

namespace TrustVault.Core.DTOs;

public class GrantPermissionRequest
{
    [Required, EmailAddress]
    public string UserEmail { get; set; } = string.Empty;

    [Required]
    public DocumentPermission Permission { get; set; } = DocumentPermission.Read;
}

public class DocumentGrantDto
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DocumentPermission Permission { get; set; }
    public DateTime GrantedAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public bool IsActive { get; set; }
}
