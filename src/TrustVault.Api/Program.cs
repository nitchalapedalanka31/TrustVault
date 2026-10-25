using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TrustVault.Api.Middleware;
using TrustVault.Core.Entities;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;
using TrustVault.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=trustvault.db;";

builder.Services.AddDbContext<TrustVaultDbContext>(options =>
{
    if (connectionString.Contains("Host=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.Contains("Server=", StringComparison.OrdinalIgnoreCase))
    {
        options.UseNpgsql(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }
});

// 2. Identity configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
{
    // Password security policies
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Brute-force & lockout protection
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;
})
.AddEntityFrameworkStores<TrustVaultDbContext>()
.AddClaimsPrincipalFactory<TrustVaultClaimsPrincipalFactory>()
.AddDefaultTokenProviders();

// Immediate security stamp validation for instantaneous revocation upon deactivation
builder.Services.Configure<SecurityStampValidatorOptions>(options =>
{
    options.ValidationInterval = TimeSpan.Zero;
});

// 3. Cookie Authentication configuration
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".TrustVault.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;

    // Return pure REST HTTP status codes instead of redirecting
    options.Events.OnRedirectToLogin = context =>
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

// 4. CSRF / Antiforgery configuration
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // Readable by Angular HttpClientXsrfModule
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// 5. CORS policy for enterprise frontend integration
builder.Services.AddCors(options =>
{
    options.AddPolicy("TrustVaultClientPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:4200", "http://127.0.0.1:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// 6. Core & Security Services Registration
builder.Services.AddScoped<IDocumentAuthorizationService, DocumentAuthorizationService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddSingleton<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAiSuggestionService, AiSuggestionService>();

builder.Services.AddControllers();

var app = builder.Build();

// 7. Security Pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();

app.UseCors("TrustVaultClientPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// 8. Auto-seeding and bootstrap
await DbInitializer.InitializeDatabaseAsync(app.Services, app.Environment.IsDevelopment());

app.Run();

// Expose Program class for WebApplicationFactory integration testing
public partial class Program { }
