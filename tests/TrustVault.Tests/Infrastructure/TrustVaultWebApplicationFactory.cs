using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Tests.Infrastructure;

public class TrustVaultWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testDbName = $"trustvault_test_{Guid.NewGuid():N}.db";
    private readonly string _testStorageDir = Path.Combine(Path.GetTempPath(), $"trustvault_test_storage_{Guid.NewGuid():N}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(_testStorageDir);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var dict = new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = $"Data Source={_testDbName};",
                ["Storage:LocalPath"] = _testStorageDir,
                ["Storage:MaxFileSizeBytes"] = "26214400"
            };
            config.AddInMemoryCollection(dict);
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing DbContext registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TrustVaultDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Register SQLite test database
            services.AddDbContext<TrustVaultDbContext>(options =>
            {
                options.UseSqlite($"Data Source={_testDbName};");
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        // Ensure database schema is created and initialized
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TrustVaultDbContext>();
        db.Database.EnsureCreated();

        return host;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            try
            {
                if (File.Exists(_testDbName)) File.Delete(_testDbName);
                if (Directory.Exists(_testStorageDir)) Directory.Delete(_testStorageDir, true);
            }
            catch { /* clean up */ }
        }
    }
}
