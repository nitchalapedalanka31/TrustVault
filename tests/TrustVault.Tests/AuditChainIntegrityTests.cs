using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;
using TrustVault.Tests.Infrastructure;

namespace TrustVault.Tests;

public class AuditChainIntegrityTests : IClassFixture<TrustVaultWebApplicationFactory>
{
    private readonly TrustVaultWebApplicationFactory _factory;

    public AuditChainIntegrityTests(TrustVaultWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AuditChain_InitialIntegrity_IsValid()
    {
        var adminHelper = new TestClientHelper(_factory);
        await adminHelper.LoginAsync("admin@trustvault.local", "Admin@TrustVault2026!");

        var verifyRes = await adminHelper.Client.PostAsync("/api/admin/audit/verify", null);
        Assert.Equal(HttpStatusCode.OK, verifyRes.StatusCode);

        var report = await verifyRes.Content.ReadFromJsonAsync<AuditVerificationResult>();
        Assert.NotNull(report);
        Assert.True(report.IsValid);
        Assert.True(report.TotalEventsChecked > 0);
    }

    [Fact]
    public async Task AuditChain_TamperingDetected_WhenDatabaseRecordModified()
    {
        var adminHelper = new TestClientHelper(_factory);
        await adminHelper.LoginAsync("admin@trustvault.local", "Admin@TrustVault2026!");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustVaultDbContext>();
        var auditService = scope.ServiceProvider.GetRequiredService<IAuditService>();

        // Find an audit event in the chain
        var targetEvent = await db.AuditEvents.OrderBy(a => a.Id).FirstOrDefaultAsync();
        Assert.NotNull(targetEvent);

        // Adversarial attack: Attacker silently edits action or metadata in database
        var originalAction = targetEvent.Action;
        targetEvent.Action = "SILENTLY_TAMPERED_ACTION";
        await db.SaveChangesAsync();

        // Integrity verification must detect the tampering!
        var tamperedReport = await auditService.VerifyChainIntegrityAsync();
        Assert.False(tamperedReport.IsValid);
        Assert.Equal(targetEvent.Id, tamperedReport.CorruptedEventId);
        Assert.Contains("Tampered record detected", tamperedReport.ErrorMessage);

        // Revert tampering
        targetEvent.Action = originalAction;
        await db.SaveChangesAsync();
    }
}
