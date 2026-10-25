using System.Net;
using System.Net.Http.Json;
using TrustVault.Core.DTOs;
using TrustVault.Tests.Infrastructure;

namespace TrustVault.Tests;

public class AdminSegregationAndDeactivationTests : IClassFixture<TrustVaultWebApplicationFactory>
{
    private readonly TrustVaultWebApplicationFactory _factory;

    public AdminSegregationAndDeactivationTests(TrustVaultWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Admin_CannotReadTenantDocumentContent_WithoutExplicitGrant()
    {
        var aliceHelper = new TestClientHelper(_factory);
        var adminHelper = new TestClientHelper(_factory);

        await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");
        await adminHelper.LoginAsync("admin@trustvault.local", "Admin@TrustVault2026!");

        // Alice uploads document
        var uploadContent = TestClientHelper.CreateValidPdfUploadContent("Alice Executive Confidential", "Legal");
        var uploadRes = await aliceHelper.Client.PostAsync("/api/documents", uploadContent);
        var doc = await uploadRes.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(doc);

        // Admin can access admin stats and audit logs
        var statsRes = await adminHelper.Client.GetAsync("/api/admin/stats");
        Assert.Equal(HttpStatusCode.OK, statsRes.StatusCode);

        var auditRes = await adminHelper.Client.GetAsync("/api/admin/audit");
        Assert.Equal(HttpStatusCode.OK, auditRes.StatusCode);

        // Security boundary: Admin CANNOT download Alice's document without explicit grant!
        var adminDownloadRes = await adminHelper.Client.GetAsync($"/api/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, adminDownloadRes.StatusCode);

        var adminViewRes = await adminHelper.Client.GetAsync($"/api/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.NotFound, adminViewRes.StatusCode);
    }

    [Fact]
    public async Task DeactivatedUser_ImmediatelyBlockedFromAccess()
    {
        var adminHelper = new TestClientHelper(_factory);
        var charlieHelper = new TestClientHelper(_factory);

        await adminHelper.LoginAsync("admin@trustvault.local", "Admin@TrustVault2026!");
        await charlieHelper.LoginAsync("charlie@trustvault.local", "Charlie@TrustVault2026!");

        // 1. Charlie can query documents initially
        var charlieRes1 = await charlieHelper.Client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.OK, charlieRes1.StatusCode);

        // 2. Admin retrieves Charlie's ID and deactivates Charlie
        var usersRes = await adminHelper.Client.GetAsync("/api/admin/users");
        var users = await usersRes.Content.ReadFromJsonAsync<List<UserProfileDto>>();
        var charlie = users!.First(u => u.Email == "charlie@trustvault.local");

        var deactivateRes = await adminHelper.Client.PutAsJsonAsync($"/api/admin/users/{charlie.Id}/status", new UpdateUserStatusRequest
        {
            IsActive = false
        });
        Assert.Equal(HttpStatusCode.OK, deactivateRes.StatusCode);

        // 3. Charlie's next request fails immediately (Unauthorized 401)
        var charlieRes2 = await charlieHelper.Client.GetAsync("/api/documents");
        Assert.Equal(HttpStatusCode.Unauthorized, charlieRes2.StatusCode);

        // 4. Charlie cannot log in again
        var charlieLoginRes = await charlieHelper.LoginAsync("charlie@trustvault.local", "Charlie@TrustVault2026!");
        Assert.Equal(HttpStatusCode.Unauthorized, charlieLoginRes.StatusCode);
    }
}
