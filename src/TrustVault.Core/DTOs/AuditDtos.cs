namespace TrustVault.Core.DTOs;

public class AuditEventDto
{
    public long Id { get; set; }
    public DateTime TimestampUtc { get; set; }
    public string ActorId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Result { get; set; } = string.Empty;
    public string? ClientIp { get; set; }
    public string? CorrelationId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    public string PreviousHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
}

public class AuditQuery
{
    public string? ActorId { get; set; }
    public string? Action { get; set; }
    public string? ResourceId { get; set; }
    public string? Result { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class AdminStatsDto
{
    public int TotalUsers { get; set; }
    public int TotalDocuments { get; set; }
    public int TotalActiveGrants { get; set; }
    public long TotalAuditEvents { get; set; }
    public int TotalAccessDenials { get; set; }
    public bool ChainIntegrityValid { get; set; }
}
