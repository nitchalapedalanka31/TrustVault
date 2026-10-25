using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustVault.Core.DTOs;
using TrustVault.Core.Entities;
using TrustVault.Core.Enums;
using TrustVault.Core.Interfaces;
using TrustVault.Infrastructure.Data;

namespace TrustVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly TrustVaultDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAuditService _auditService;

    public AdminController(
        TrustVaultDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _userManager = userManager;
        _auditService = auditService;
    }

    [HttpGet("stats")]
    public async Task<IActionResult> GetAdminStats()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        var totalDocuments = await _dbContext.Documents.CountAsync(d => d.Status == DocumentStatus.Active);
        var totalActiveGrants = await _dbContext.DocumentGrants.CountAsync(g => g.IsActive);
        var totalAuditEvents = await _dbContext.AuditEvents.LongCountAsync();
        var totalDenials = await _dbContext.AuditEvents.CountAsync(a => a.Result == AuditResults.Denied);

        var integrityResult = await _auditService.VerifyChainIntegrityAsync();

        return Ok(new AdminStatsDto
        {
            TotalUsers = totalUsers,
            TotalDocuments = totalDocuments,
            TotalActiveGrants = totalActiveGrants,
            TotalAuditEvents = totalAuditEvents,
            TotalAccessDenials = totalDenials,
            ChainIntegrityValid = integrityResult.IsValid
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _userManager.Users
            .OrderBy(u => u.Email)
            .Select(u => new UserProfileDto
            {
                Id = u.Id,
                Email = u.Email!,
                FullName = u.FullName,
                Role = u.Role,
                IsActive = u.IsActive,
                CreatedAtUtc = u.CreatedAtUtc
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost("users")]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var existing = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (existing != null)
        {
            return BadRequest(new { message = "A user with this email address already exists." });
        }

        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            Role = request.Role,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { message = string.Join("; ", result.Errors.Select(e => e.Description)) });
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        await _auditService.RecordEventAsync(
            AuditActionTypes.UserCreated,
            adminId,
            user.Id.ToString(),
            AuditResults.Success,
            new { user.Email, user.Role },
            clientIp,
            correlationId);

        return Ok(new UserProfileDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role,
            IsActive = user.IsActive,
            CreatedAtUtc = user.CreatedAtUtc
        });
    }

    [HttpPut("users/{id:guid}/status")]
    public async Task<IActionResult> UpdateUserStatus(Guid id, [FromBody] UpdateUserStatusRequest request)
    {
        var user = await _userManager.FindByIdAsync(id.ToString());
        if (user == null)
        {
            return NotFound(new { message = "User not found." });
        }

        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (user.Id.ToString() == adminId && !request.IsActive)
        {
            return BadRequest(new { message = "Administrators cannot deactivate their own account." });
        }

        user.IsActive = request.IsActive;
        // Updating security stamp invalidates existing cookie sessions immediately
        await _userManager.UpdateSecurityStampAsync(user);
        await _userManager.UpdateAsync(user);

        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        await _auditService.RecordEventAsync(
            AuditActionTypes.UserStatusChanged,
            adminId,
            user.Id.ToString(),
            AuditResults.Success,
            new { user.Email, NewActiveStatus = request.IsActive },
            clientIp,
            correlationId);

        return Ok(new { message = $"User status successfully updated to {(request.IsActive ? "Active" : "Deactivated")}." });
    }

    [HttpGet("audit")]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditQuery query)
    {
        var baseQuery = _dbContext.AuditEvents.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.ActorId))
        {
            baseQuery = baseQuery.Where(a => a.ActorId.ToLower().Contains(query.ActorId.Trim().ToLower()));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            baseQuery = baseQuery.Where(a => a.Action == query.Action.Trim());
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceId))
        {
            baseQuery = baseQuery.Where(a => a.ResourceId != null && a.ResourceId.Contains(query.ResourceId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Result))
        {
            baseQuery = baseQuery.Where(a => a.Result == query.Result.Trim());
        }

        if (query.FromDate.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.TimestampUtc >= query.FromDate.Value);
        }

        if (query.ToDate.HasValue)
        {
            baseQuery = baseQuery.Where(a => a.TimestampUtc <= query.ToDate.Value);
        }

        var totalCount = await baseQuery.CountAsync();
        var pageNumber = Math.Max(1, query.PageNumber);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var events = await baseQuery
            .OrderByDescending(a => a.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditEventDto
            {
                Id = a.Id,
                TimestampUtc = a.TimestampUtc,
                ActorId = a.ActorId,
                Action = a.Action,
                ResourceId = a.ResourceId,
                Result = a.Result,
                ClientIp = a.ClientIp,
                CorrelationId = a.CorrelationId,
                MetadataJson = a.MetadataJson,
                PreviousHash = a.PreviousHash,
                CurrentHash = a.CurrentHash
            })
            .ToListAsync();

        return Ok(new PagedResult<AuditEventDto>
        {
            Items = events,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        });
    }

    [HttpPost("audit/verify")]
    public async Task<IActionResult> VerifyAuditIntegrity()
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var clientIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var correlationId = HttpContext.TraceIdentifier;

        var verificationResult = await _auditService.VerifyChainIntegrityAsync();

        await _auditService.RecordEventAsync(
            AuditActionTypes.AuditIntegrityVerified,
            adminId,
            null,
            verificationResult.IsValid ? AuditResults.Success : AuditResults.Failed,
            new 
            { 
                verificationResult.IsValid, 
                verificationResult.TotalEventsChecked, 
                verificationResult.CorruptedEventId, 
                verificationResult.ErrorMessage 
            },
            clientIp,
            correlationId);

        return Ok(verificationResult);
    }
}
