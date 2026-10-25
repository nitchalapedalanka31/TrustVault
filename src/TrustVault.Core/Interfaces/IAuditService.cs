using TrustVault.Core.Entities;

namespace TrustVault.Core.Interfaces;

public class AuditVerificationResult
{
    public bool IsValid { get; set; }
    public long TotalEventsChecked { get; set; }
    public string? ErrorMessage { get; set; }
    public long? CorruptedEventId { get; set; }
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}

public interface IAuditService
{
    Task<AuditEvent> RecordEventAsync(
        string action, 
        string? actorId, 
        string? resourceId, 
        string result, 
        object? metadata = null, 
        string? clientIp = null, 
        string? correlationId = null, 
        CancellationToken cancellationToken = default);

    Task<AuditVerificationResult> VerifyChainIntegrityAsync(CancellationToken cancellationToken = default);
}
