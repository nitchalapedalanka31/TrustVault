using System.Net;
using System.Net.Http.Json;
using System.Text;
using TrustVault.Core.DTOs;
using TrustVault.Core.Enums;
using TrustVault.Infrastructure.Data;
using TrustVault.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace TrustVault.Tests;

public class PermissionAndRevocationTests : IClassFixture<TrustVaultWebApplicationFactory>
{
    private readonly TrustVaultWebApplicationFactory _factory;

    public PermissionAndRevocationTests(TrustVaultWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DocumentGrantLifecycle_ReadEditAndImmediateRevocation_EnforcedStrictly()
    {
        var aliceHelper = new TestClientHelper(_factory);
        var bobHelper = new TestClientHelper(_factory);

        await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");
        await bobHelper.LoginAsync("bob@trustvault.local", "Bob@TrustVault2026!");

        // Alice uploads document
        var uploadContent = TestClientHelper.CreateValidPdfUploadContent("Alice Project Blueprint", "Engineering");
        var uploadRes = await aliceHelper.Client.PostAsync("/api/documents", uploadContent);
        var doc = await uploadRes.Content.ReadFromJsonAsync<DocumentDto>();
        Assert.NotNull(doc);

        // Step 1: Alice grants READ access to Bob
        var grantRes = await aliceHelper.Client.PostAsJsonAsync($"/api/documents/{doc.Id}/grants", new GrantPermissionRequest
        {
            UserEmail = "bob@trustvault.local",
            Permission = DocumentPermission.Read
        });
        Assert.Equal(HttpStatusCode.OK, grantRes.StatusCode);
        var grantDto = await grantRes.Content.ReadFromJsonAsync<DocumentGrantDto>();
        Assert.NotNull(grantDto);

        // Step 2: Bob can now view metadata and download the document
        var bobViewRes = await bobHelper.Client.GetAsync($"/api/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.OK, bobViewRes.StatusCode);

        var bobDownloadRes = await bobHelper.Client.GetAsync($"/api/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.OK, bobDownloadRes.StatusCode);
        Assert.Contains("nosniff", bobDownloadRes.Headers.GetValues("X-Content-Type-Options").First());

        // Step 3: Bob with READ cannot edit metadata or upload new version
        var bobEditRes = await bobHelper.Client.PutAsJsonAsync($"/api/documents/{doc.Id}", new UpdateDocumentMetadataRequest
        {
            Title = "Tampered Title By Bob",
            Category = "Hacked",
            Tags = "tampered"
        });
        // Returns 404/Denied
        Assert.Equal(HttpStatusCode.NotFound, bobEditRes.StatusCode);

        // Step 4: Alice updates Bob's grant to EDIT
        var upgradeGrantRes = await aliceHelper.Client.PostAsJsonAsync($"/api/documents/{doc.Id}/grants", new GrantPermissionRequest
        {
            UserEmail = "bob@trustvault.local",
            Permission = DocumentPermission.Edit
        });
        Assert.Equal(HttpStatusCode.OK, upgradeGrantRes.StatusCode);
        var upgradedGrantDto = await upgradeGrantRes.Content.ReadFromJsonAsync<DocumentGrantDto>();
        Assert.NotNull(upgradedGrantDto);

        // Step 5: Bob can now update metadata and upload a new version
        var bobEditSuccessRes = await bobHelper.Client.PutAsJsonAsync($"/api/documents/{doc.Id}", new UpdateDocumentMetadataRequest
        {
            Title = "Alice Project Blueprint v2 (Updated by Bob)",
            Category = "Engineering",
            Tags = "v2,revised"
        });
        Assert.Equal(HttpStatusCode.OK, bobEditSuccessRes.StatusCode);

        // Bob uploads new version
        var newVersionContent = new MultipartFormDataContent();
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4\nNew version by Bob\n%%EOF");
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        newVersionContent.Add(fileContent, "file", "blueprint_v2.pdf");

        var bobVersionRes = await bobHelper.Client.PostAsync($"/api/documents/{doc.Id}/versions", newVersionContent);
        Assert.Equal(HttpStatusCode.OK, bobVersionRes.StatusCode);

        // Bob still CANNOT delete Alice's document or manage grants
        var bobDeleteRes = await bobHelper.Client.DeleteAsync($"/api/documents/{doc.Id}");
        Assert.Equal(HttpStatusCode.NotFound, bobDeleteRes.StatusCode);

        // Step 6: IMMEDIATE REVOCATION
        // Alice revokes Bob's grant
        var revokeRes = await aliceHelper.Client.DeleteAsync($"/api/documents/{doc.Id}/grants/{upgradedGrantDto.Id}");
        Assert.Equal(HttpStatusCode.OK, revokeRes.StatusCode);

        // Step 7: Bob's immediate subsequent download attempt MUST fail
        var bobDownloadAfterRevocation = await bobHelper.Client.GetAsync($"/api/documents/{doc.Id}/download");
        Assert.Equal(HttpStatusCode.NotFound, bobDownloadAfterRevocation.StatusCode);

        // Verify ACCESS_DENIED logged for post-revocation attempt
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustVaultDbContext>();
        var postRevocationDenial = await db.AuditEvents.FirstOrDefaultAsync(a =>
            a.Action == AuditActionTypes.AccessDenied &&
            a.ResourceId == doc.Id.ToString() &&
            a.Result == AuditResults.Denied);

        Assert.NotNull(postRevocationDenial);
    }
}
