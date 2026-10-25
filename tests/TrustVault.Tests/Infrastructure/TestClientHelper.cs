using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using TrustVault.Core.DTOs;

namespace TrustVault.Tests.Infrastructure;

public class TestClientHelper
{
    private readonly HttpClient _client;

    public HttpClient Client => _client;

    public TestClientHelper(TrustVaultWebApplicationFactory factory)
    {
        var options = new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true
        };

        _client = factory.CreateClient(options);
    }

    public async Task<HttpResponseMessage> LoginAsync(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = email,
            Password = password
        });

        // Extract XSRF-TOKEN from Set-Cookie header if present
        if (response.Headers.TryGetValues("Set-Cookie", out var cookieHeaders))
        {
            foreach (var header in cookieHeaders)
            {
                if (header.StartsWith("XSRF-TOKEN="))
                {
                    var token = header.Substring("XSRF-TOKEN=".Length).Split(';')[0];
                    _client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
                    _client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
                }
            }
        }

        return response;
    }

    public static MultipartFormDataContent CreateValidPdfUploadContent(string title = "Test Confidential Document", string category = "Finance")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(title), "Title");
        content.Add(new StringContent("Confidential test description"), "Description");
        content.Add(new StringContent(category), "Category");
        content.Add(new StringContent("confidential,test"), "Tags");

        // Valid minimal PDF bytes (%PDF-1.4 ... %%EOF)
        var pdfBytes = Encoding.UTF8.GetBytes("%PDF-1.4\n1 0 obj\n<< /Type /Catalog >>\nendobj\nxref\n0 1\n0000000000 65535 f \ntrailer\n<< /Root 1 0 R >>\n%%EOF");
        var fileContent = new ByteArrayContent(pdfBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", "financial_report.pdf");

        return content;
    }

    public static MultipartFormDataContent CreateMaliciousUploadContent(string fileName, byte[] rawBytes, string declaredMime)
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent("Malicious Test"), "Title");
        content.Add(new StringContent("Malicious file payload test"), "Description");
        content.Add(new StringContent("General"), "Category");
        content.Add(new StringContent("malware"), "Tags");

        var fileContent = new ByteArrayContent(rawBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(declaredMime);
        content.Add(fileContent, "file", fileName);

        return content;
    }
}
