using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TrustVault.Core.Entities;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;

namespace TrustVault.Infrastructure.Data;

public static class DbInitializer
{
    public static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider, bool isDevelopment)
    {
        using var scope = serviceProvider.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILogger<TrustVaultDbContext>>();
        var dbContext = services.GetRequiredService<TrustVaultDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var auditService = services.GetRequiredService<IAuditService>();

        await dbContext.Database.EnsureCreatedAsync();

        // 1. Production bootstrap: Check if any Admin user exists
        var hasAdmin = await userManager.Users.AnyAsync(u => u.Role == UserRole.Admin);
        if (!hasAdmin)
        {
            var adminEmail = Environment.GetEnvironmentVariable("TRUSTVAULT_ADMIN_EMAIL") ?? "admin@trustvault.local";
            var adminPassword = Environment.GetEnvironmentVariable("TRUSTVAULT_ADMIN_PASSWORD") ?? "Admin@TrustVault2026!";

            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "Security Administrator",
                Role = UserRole.Admin,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                logger.LogInformation("Initial administrator account bootstrapped: {Email}", adminEmail);
                await auditService.RecordEventAsync(
                    AuditActionTypes.UserCreated, 
                    "SYSTEM_BOOTSTRAP", 
                    adminUser.Id.ToString(), 
                    AuditResults.Success, 
                    new { Role = "Admin", Email = adminEmail });
            }
        }

        // 2. Development demonstration accounts (Only seeded if in Development environment)
        if (isDevelopment)
        {
            var demoUsers = new[]
            {
                new { Email = "alice@trustvault.local", Name = "Alice Morgan", Password = "Alice@TrustVault2026!", Role = UserRole.Owner },
                new { Email = "bob@trustvault.local", Name = "Bob Jenkins", Password = "Bob@TrustVault2026!", Role = UserRole.Viewer },
                new { Email = "charlie@trustvault.local", Name = "Charlie Davis", Password = "Charlie@TrustVault2026!", Role = UserRole.Viewer }
            };

            foreach (var demo in demoUsers)
            {
                var existing = await userManager.FindByEmailAsync(demo.Email);
                if (existing == null)
                {
                    var user = new ApplicationUser
                    {
                        UserName = demo.Email,
                        Email = demo.Email,
                        FullName = demo.Name,
                        Role = demo.Role,
                        IsActive = true,
                        EmailConfirmed = true,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    var res = await userManager.CreateAsync(user, demo.Password);
                    if (res.Succeeded)
                    {
                        await auditService.RecordEventAsync(
                            AuditActionTypes.UserCreated, 
                            "SYSTEM_SEED", 
                            user.Id.ToString(), 
                            AuditResults.Success, 
                            new { demo.Role, demo.Email });
                    }
                }
            }
        }
    }
}
