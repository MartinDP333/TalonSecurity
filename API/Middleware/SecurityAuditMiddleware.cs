using API.Services;
using Application.Interfaces;

namespace API.Middleware;

public sealed class SecurityAuditMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<SecurityAuditMiddleware> _logger;

    public SecurityAuditMiddleware(RequestDelegate next, ILogger<SecurityAuditMiddleware> logger)
        => (_next, _logger) = (next, logger);

    public async Task InvokeAsync(HttpContext context, SecurityAuditLogger audit, ICurrentTenantService tenant)
    {
        var path = context.Request.Path.Value ?? "";
        var loginOrTenantLookup = path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase)
            || path.Equals("/api/auth/available-tenants", StringComparison.OrdinalIgnoreCase);
        var shouldRecordRequest = path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase);
        var sensitive = path.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/memberships", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/platform-admin", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/api/protected-sites", StringComparison.OrdinalIgnoreCase);

        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled API exception for {Method} {Path}", context.Request.Method, path);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await audit.WriteAsync("UnhandledException", "ApplicationError", ex.GetType().Name, 500, tenantId: tenant.TenantId, ct: context.RequestAborted);
            throw;
        }

        var code = context.Response.StatusCode;
        var auditTenantId = tenant.TenantId;
        if (!auditTenantId.HasValue
            && context.User.Identity?.IsAuthenticated == true
            && Guid.TryParse(context.User.FindFirst("tenant_id")?.Value, out var claimedTenantId))
            auditTenantId = claimedTenantId;

        if (code == StatusCodes.Status401Unauthorized && !path.Equals("/api/auth/login", StringComparison.OrdinalIgnoreCase) && !path.Equals("/api/auth/available-tenants", StringComparison.OrdinalIgnoreCase))
            await audit.WriteAsync("AuthenticationFailure", "Authentication", "Request was unauthorized.", code, ct: context.RequestAborted);
        else if (code == StatusCodes.Status403Forbidden)
            await audit.WriteAsync("AuthorizationFailure", "Authorization", "Request was forbidden.", code, tenantId: auditTenantId, ct: context.RequestAborted);
        else if (!loginOrTenantLookup && (sensitive || code >= 400))
            await audit.WriteAsync("SensitiveEndpointAccess", code >= 400 ? "HttpError" : "SensitiveAccess", null, code, tenantId: auditTenantId, ct: context.RequestAborted);
        else if (shouldRecordRequest)
            await audit.WriteAsync("ApiRequest", "RequestTelemetry", statusCode: code, tenantId: auditTenantId, ct: context.RequestAborted);
    }
}
