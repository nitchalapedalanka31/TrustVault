using System.Net;
using System.Net.Http.Json;
using TrustVault.Core.DTOs;
using TrustVault.Core.Enums;
using TrustVault.Infrastructure.Data;
using TrustVault.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace TrustVault.Tests;

public class SecurityIsolationTests : IClassFixture<TrustVaultWebApplicationFactory>
{
    private readonly TrustVaultWebApplicationFactory _factory;

    public SecurityIsolationTests(TrustVaultWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessDocuments_ReturnsUnauthorized()
    {
        var helper = new TestClientHelper(_factory);
        var response = await helper.Client.GetAsync("/api/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AliceAndBob_TenantIsolation_BobCannotAccessAlicesDocument()
    {
        var aliceHelper = new TestClientHelper(_factory);
        var bobHelper = new TestClientHelper(_factory);

        // 1. Log in Alice (Owner) and Bob (Viewer)
        var aliceLogin = await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");
        Assert.Equal(HttpStatusCode.OK, aliceLogin.StatusCode);

        var bobLogin = await bobHelper.LoginAsync("bob@trustvault.local", "Bob@TrustVault2026!");
        Assert.Equal(HttpStatusCode.OK, bobLogin.StatusCode);

        // 2. Alice uploads a confidential document
        var uploadContent = TestClientHelper.CreateValidPdfUploadContent("Alice Confidential Q3 Strategy", "Finance");
        var uploadRes = await aliceHelper.Client.PostAsync("/api/documents", uploadContent);
        Assert.Equal(HttpStatusCode.Created, uploadRes.StatusCode);

        var createdDoc = await uploadRes.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(createdDoc);
        Assert.NotEqual(Guid.Empty, createdDoc.Id);

        // 3. Alice can view and download her own document
        var aliceViewRes = await aliceHelper.Client.GetAsync($"/api/documents/{createdDoc.Id}");
        Assert.Equal(HttpStatusCode.OK, aliceViewRes.StatusCode);

        var aliceDownloadRes = await aliceHelper.Client.GetAsync($"/api/documents/{createdDoc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, aliceDownloadRes.StatusCode);

        // 4. Bob attempts to access Alice's document directly by ID
        // Security requirement: Must return 404 (Anti-Oracle / Anti-IDOR defense)
        var bobViewRes = await bobHelper.Client.GetAsync($"/api/documents/{createdDoc.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobViewRes.StatusCode);

        var bobDownloadRes = await bobHelper.Client.GetAsync($"/api/documents/{createdDoc.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, bobDownloadRes.StatusCode);

        // 5. Bob searches for documents -> Alice's document must NEVER appear in Bob's search results
        var bobSearchRes = await bobHelper.Client.GetAsync("/api/documents?query=Alice");
        Assert.Equal(HttpStatusCode.OK, bobSearchRes.StatusCode);

        var bobResults = await bobSearchRes.Content.ReadFromJsonAsync<PagedResult<DocumentDto>>();
        Assert.NotNull(bobResults);
        Assert.DoesNotContain(bobResults.Items, d => d.Id == createdDoc.Id);

        // 6. Verify that an ACCESS_DENIED audit event was recorded in the database
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustVaultDbContext>();
        var denialEvent = await db.AuditEvents.FirstOrDefaultAsync(a => 
            a.Action == AuditActionTypes.AccessDenied && 
            a.ResourceId == createdDoc.Id.ToString() && 
            a.Result == AuditResults.Denied);

        Assert.NotNull(denialEvent);
    }
}
