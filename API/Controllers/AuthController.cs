using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using API.Services;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace API.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly SecurityDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly SecurityAuditLogger _audit;
    public AuthController(SecurityDbContext db, IConfiguration configuration, SecurityAuditLogger audit) => (_db, _configuration, _audit) = (db, configuration, audit);

    [HttpPost("users")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> CreateUser(CreateUserRequest request, CancellationToken ct)
    {
        if (await _db.UserAccounts.IgnoreQueryFilters().AnyAsync(x => x.UserName == request.UserName, ct))
            return Conflict("Username already exists.");

        var user = new UserAccount(request.UserName, PasswordService.Hash(request.Password), request.IsPlatformAdmin);
        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(AvailableTenants), new { userName = user.UserName }, new { user.Id, user.UserName, user.IsPlatformAdmin });
    }

    [HttpPost("tenants")]
    [Authorize(Roles = "PlatformAdmin")]
    public async Task<IActionResult> CreateTenant(CreateTenantRequest request, CancellationToken ct)
    {
        var owner = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == request.OwnerUserName, ct);
        if (owner is null) return BadRequest("Owner user does not exist. Create the user first.");
        var tenant = new Tenant(request.TenantName);
        _db.Tenants.Add(tenant);
        _db.TenantMemberships.Add(new TenantMembership(tenant.Id, owner.Id, "Admin"));
        await _db.SaveChangesAsync(ct);
        return Created("/api/tenants", new { tenant.Id, tenant.Name, owner.UserName, OwnerRole = "Admin" });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == request.UserName, ct);
        if (user is null || !PasswordService.Verify(request.Password, user.PasswordHash))
        {
            await _audit.WriteAsync("LoginFailure", "Authentication", "Invalid username or password.", StatusCodes.Status401Unauthorized, request.UserName, ct: ct);
            return Unauthorized();
        }
        var membership = await MembershipAsync(user.Id, request.TenantName, ct);
        if (membership is null)
        {
            await _audit.WriteAsync("LoginFailure", "Authentication", "No active membership for selected tenant.", StatusCodes.Status401Unauthorized, user.UserName, ct: ct);
            return Unauthorized("No active membership exists for this tenant.");
        }
        await _audit.WriteAsync("LoginSuccess", "Authentication", $"Tenant: {membership.Tenant.Name}", StatusCodes.Status200OK, user.UserName, membership.TenantId, ct);
        return Ok(CreateToken(user, membership));
    }

    [HttpPost("switch-tenant")]
    [Authorize]
    public async Task<IActionResult> SwitchTenant(SwitchTenantRequest request, CancellationToken ct)
    {
        var userName = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == userName, ct);
        if (user is null) return Unauthorized();
        var membership = await MembershipAsync(user.Id, request.TenantName, ct);
        return membership is null ? Forbid() : Ok(CreateToken(user, membership));
    }

    [HttpPost("available-tenants")]
    [AllowAnonymous]
    public async Task<IActionResult> AvailableTenants(CredentialsRequest request, CancellationToken ct)
    {
        var user = await _db.UserAccounts.IgnoreQueryFilters().SingleOrDefaultAsync(x => x.UserName == request.UserName, ct);
        if (user is null || !PasswordService.Verify(request.Password, user.PasswordHash)) return Unauthorized();
        return Ok(await _db.TenantMemberships.IgnoreQueryFilters().Where(x => x.UserAccountId == user.Id && x.IsActive).OrderBy(x => x.Tenant.Name).Select(x => new { x.TenantId, TenantName = x.Tenant.Name, x.Role }).ToListAsync(ct));
    }

    private Task<TenantMembership?> MembershipAsync(Guid userId, string tenantName, CancellationToken ct) =>
        _db.TenantMemberships.IgnoreQueryFilters().Include(x => x.Tenant).SingleOrDefaultAsync(x => x.UserAccountId == userId && x.Tenant.Name == tenantName && x.IsActive, ct);

    private object CreateToken(UserAccount user, TenantMembership membership)
    {
        var key = _configuration["Jwt:Key"] ?? Environment.GetEnvironmentVariable("JWT_KEY") ?? throw new InvalidOperationException("JWT_KEY is missing.");
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.UserName), new Claim(ClaimTypes.NameIdentifier, user.UserName), new Claim("tenant_id", membership.TenantId.ToString()), new Claim("role", membership.Role) };
        if (user.IsPlatformAdmin) claims = claims.Append(new Claim("role", "PlatformAdmin")).ToArray();
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(claims: claims, expires: DateTime.UtcNow.AddHours(8), signingCredentials: credentials);
        return new { accessToken = new JwtSecurityTokenHandler().WriteToken(token), tenantId = membership.TenantId, tenantName = membership.Tenant.Name, role = membership.Role, isPlatformAdmin = user.IsPlatformAdmin };
    }

    public sealed record CreateUserRequest(string UserName, string Password, bool IsPlatformAdmin = false);
    public sealed record CreateTenantRequest(string TenantName, string OwnerUserName);
    public sealed record CredentialsRequest(string UserName, string Password);
    public sealed record LoginRequest(string UserName, string Password, string TenantName);
    public sealed record SwitchTenantRequest(string TenantName);
}
