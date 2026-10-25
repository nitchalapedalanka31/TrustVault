using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustVault.Core.Entities;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Infrastructure.Services;

public class AuditService : IAuditService
{
    private const string GenesisHash = "0000000000000000000000000000000000000000000000000000000000000000";
    private readonly TrustVaultDbContext _dbContext;
    private static readonly SemaphoreSlim ChainWriteLock = new(1, 1);

    public AuditService(TrustVaultDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AuditEvent> RecordEventAsync(
        string action, 
        string? actorId, 
        string? resourceId, 
        string result, 
        object? metadata = null, 
        string? clientIp = null, 
        string? correlationId = null, 
        CancellationToken cancellationToken = default)
    {
        var metadataJson = metadata != null 
            ? JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = false }) 
            : "{}";

        var now = DateTimeOffset.UtcNow;
        var auditEvent = new AuditEvent
        {
            TimestampEpochMs = now.ToUnixTimeMilliseconds(),
            TimestampUtc = now.UtcDateTime,
            ActorId = string.IsNullOrWhiteSpace(actorId) ? "ANONYMOUS" : actorId,
            Action = action,
            ResourceId = resourceId,
            Result = result,
            ClientIp = clientIp,
            CorrelationId = correlationId,
            MetadataJson = metadataJson
        };

        await ChainWriteLock.WaitAsync(cancellationToken);
        try
        {
            // Authoritative lookup of the most recent committed hash in the chain
            var lastEvent = await _dbContext.AuditEvents
                .OrderByDescending(a => a.Id)
                .Select(a => new { a.Id, a.CurrentHash })
                .FirstOrDefaultAsync(cancellationToken);

            auditEvent.PreviousHash = lastEvent?.CurrentHash ?? GenesisHash;
            auditEvent.CurrentHash = ComputeCanonicalHash(auditEvent);

            _dbContext.AuditEvents.Add(auditEvent);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return auditEvent;
        }
        finally
        {
            ChainWriteLock.Release();
        }
    }

    public async Task<AuditVerificationResult> VerifyChainIntegrityAsync(CancellationToken cancellationToken = default)
    {
        var result = new AuditVerificationResult
        {
            IsValid = true,
            CheckedAtUtc = DateTime.UtcNow
        };

        var events = await _dbContext.AuditEvents
            .OrderBy(a => a.Id)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        result.TotalEventsChecked = events.Count;

        if (events.Count == 0)
        {
            return result;
        }

        string expectedPreviousHash = GenesisHash;
        long previousId = 0;

        foreach (var auditEvent in events)
        {
            // 1. Verify sequence ordering
            if (auditEvent.Id <= previousId)
            {
                result.IsValid = false;
                result.CorruptedEventId = auditEvent.Id;
                result.ErrorMessage = $"Audit sequence violation: Event Id {auditEvent.Id} is not strictly greater than previous Id {previousId}.";
                return result;
            }

            // 2. Verify previous hash pointer link
            if (!string.Equals(auditEvent.PreviousHash, expectedPreviousHash, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.CorruptedEventId = auditEvent.Id;
                result.ErrorMessage = $"Chain broken at Event Id {auditEvent.Id}. Expected PreviousHash '{expectedPreviousHash}' but found '{auditEvent.PreviousHash}'.";
                return result;
            }

            // 3. Recompute and verify current canonical hash
            var computedHash = ComputeCanonicalHash(auditEvent);
            if (!string.Equals(computedHash, auditEvent.CurrentHash, StringComparison.OrdinalIgnoreCase))
            {
                result.IsValid = false;
                result.CorruptedEventId = auditEvent.Id;
                result.ErrorMessage = $"Tampered record detected at Event Id {auditEvent.Id}. Computed hash '{computedHash}' does not match stored hash '{auditEvent.CurrentHash}'.";
                return result;
            }

            expectedPreviousHash = auditEvent.CurrentHash;
            previousId = auditEvent.Id;
        }

        return result;
    }

    public static string ComputeCanonicalHash(AuditEvent ev)
    {
        // Canonical deterministic serialization string using invariant epoch timestamp
        var epoch = ev.TimestampEpochMs > 0 ? ev.TimestampEpochMs : new DateTimeOffset(ev.TimestampUtc).ToUnixTimeMilliseconds();
        var canonicalString = $"{epoch}|{ev.ActorId}|{ev.Action}|{ev.ResourceId ?? ""}|{ev.Result}|{ev.CorrelationId ?? ""}|{ev.MetadataJson}|{ev.PreviousHash}";
        
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonicalString));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
