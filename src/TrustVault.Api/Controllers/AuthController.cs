using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using TrustVault.Core.DTOs;
using TrustVault.Core.Entities;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;

namespace TrustVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;
    private readonly IAntiforgery _antiforgery;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService,
        IAntiforgery antiforgery)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditService = auditService;
        _antiforgery = antiforgery;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AuthLoginFailed,
                request.Email,
                null,
                AuditResults.Failed,
                new { Reason = "User not found" },
                clientIp,
                correlationId);

            return Unauthorized(new { message = "Invalid email or password." });
        }

        if (!user.IsActive)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AuthLoginFailed,
                user.Id.ToString(),
                null,
                AuditResults.Denied,
                new { Reason = "Account deactivated" },
                clientIp,
                correlationId);

            return Unauthorized(new { message = "Account has been deactivated. Please contact your administrator." });
        }

        var result = await _signInManager.PasswordSignInAsync(user, request.Password, isPersistent: true, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AuthLoginSuccess,
                user.Id.ToString(),
                null,
                AuditResults.Success,
                new { Role = user.Role.ToString() },
                clientIp,
                correlationId);

            // Also refresh CSRF token cookie on successful login
            var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
            Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
            {
                HttpOnly = false, // Client-side Angular reads this to set X-XSRF-TOKEN header
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Strict
            });

            return Ok(new UserProfileDto
            {
                Id = user.Id,
                Email = user.Email!,
                FullName = user.FullName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAtUtc = user.CreatedAtUtc
            });
        }

        if (result.IsLockedOut)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AuthLoginFailed,
                user.Id.ToString(),
                null,
                AuditResults.Denied,
                new { Reason = "Account locked out" },
                clientIp,
                correlationId);

            return Unauthorized(new { message = "Account locked out due to multiple failed login attempts." });
        }

        await _auditService.RecordEventAsync(
            AuditActionTypes.AuthLoginFailed,
            user.Id.ToString(),
            null,
            AuditResults.Failed,
            new { Reason = "Invalid password" },
            clientIp,
            correlationId);

        return Unauthorized(new { message = "Invalid email or password." });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        await _signInManager.SignOutAsync();

        if (userId != null)
        {
            await _auditService.RecordEventAsync(
                AuditActionTypes.AuthLogout,
                userId,
                null,
                AuditResults.Success,
                null,
                clientIp,
                correlationId);
        }

        return Ok(new { message = "Logged out successfully." });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Unauthorized();
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            await _signInManager.SignOutAsync();
            return Unauthorized();
        }

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email!,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc
        });
    }

    [HttpGet("csrf")]
    [AllowAnonymous]
    public IActionResult GetCsrfToken()
    {
        var tokens = _antiforgery.GetAndStoreTokens(HttpContext);
        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict
        });

        return Ok(new { token = tokens.RequestToken });
    }
}
