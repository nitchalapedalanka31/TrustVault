using System.Net;
using System.Text;
using TrustVault.Tests.Infrastructure;

namespace TrustVault.Tests;

public class SecureFileUploadTests : IClassFixture<TrustVaultWebApplicationFactory>
{
    private readonly TrustVaultWebApplicationFactory _factory;

    public SecureFileUploadTests(TrustVaultWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DisallowedExtension_ExecutableUpload_Rejected()
    {
        var aliceHelper = new TestClientHelper(_factory);
        await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");

        // Malicious .exe file
        var exeBytes = Encoding.UTF8.GetBytes("MZ\x90\x00\x03\x00\x00\x00FakePEFile");
        var maliciousContent = TestClientHelper.CreateMaliciousUploadContent("malware.exe", exeBytes, "application/octet-stream");

        var response = await aliceHelper.Client.PostAsync("/api/documents", maliciousContent);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DisguisedExtension_PolyglotExecutableRenamedToPdf_RejectedByMagicByteSniffer()
    {
        var aliceHelper = new TestClientHelper(_factory);
        await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");

        // Disguised .exe file named malicious.pdf (starts with MZ header instead of %PDF)
        var exeBytes = Encoding.UTF8.GetBytes("MZ\x90\x00\x03\x00\x00\x00FakeDisguisedExecutable");
        var disguisedContent = TestClientHelper.CreateMaliciousUploadContent("malicious.pdf", exeBytes, "application/pdf");

        var response = await aliceHelper.Client.PostAsync("/api/documents", disguisedContent);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DisallowedExtension_ScriptOrHtml_Rejected()
    {
        var aliceHelper = new TestClientHelper(_factory);
        await aliceHelper.LoginAsync("alice@trustvault.local", "Alice@TrustVault2026!");

        // SVG / HTML containing script
        var htmlBytes = Encoding.UTF8.GetBytes("<script>alert('xss')</script>");
        var htmlContent = TestClientHelper.CreateMaliciousUploadContent("attack.html", htmlBytes, "text/html");

        var response = await aliceHelper.Client.PostAsync("/api/documents", htmlContent);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
