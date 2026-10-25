namespace TrustVault.Core.Entities;

public class AuditEvent
{
    public long Id { get; set; }
    public long TimestampEpochMs { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public string ActorId { get; set; } = "ANONYMOUS";
    public string Action { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string Result { get; set; } = string.Empty; // SUCCESS, DENIED, FAILED
    public string? ClientIp { get; set; }
    public string? CorrelationId { get; set; }
    public string MetadataJson { get; set; } = "{}";
    
    public string PreviousHash { get; set; } = string.Empty;
    public string CurrentHash { get; set; } = string.Empty;
}
