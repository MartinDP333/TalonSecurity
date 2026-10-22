using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public sealed class SecurityAuditLogger
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<SecurityAuditLogger> _logger;

    public SecurityAuditLogger(SecurityDbContext db, ICurrentTenantService tenant, IHttpContextAccessor http, ILogger<SecurityAuditLogger> logger)
        => (_db, _tenant, _http, _logger) = (db, tenant, http, logger);

    public async Task WriteAsync(string eventType, string category, string? details = null, int? statusCode = null, string? userName = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        var context = _http.HttpContext;
        var sourceIp = API.ClientIpAddress.Get(context ?? new DefaultHttpContext());
        var severity = Classify(eventType, category, statusCode);

        if (eventType == "LoginFailure" && !string.IsNullOrWhiteSpace(sourceIp))
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var previousFailures = await _db.SecurityAuditEvents.IgnoreQueryFilters().CountAsync(
                x => x.EventType == "LoginFailure" && x.SourceIp == sourceIp && x.OccurredAt >= cutoff,
                ct);
            var totalFailures = previousFailures + 1;
            severity = totalFailures >= 5 ? (int)SeverityLevel.Critical
                : totalFailures >= 3 ? (int)SeverityLevel.High
                : (int)SeverityLevel.Medium;
            details = $"{details}; failed attempts from source IP in last 15m: {totalFailures}";
        }

        var audit = new SecurityAuditEvent(
            tenantId ?? _tenant.TenantId,
            userName ?? context?.User.FindFirst("sub")?.Value ?? context?.User.Identity?.Name,
            eventType,
            category,
            context?.Request.Method ?? "INTERNAL",
            context?.Request.Path.Value ?? "",
            sourceIp,
            statusCode,
            details,
            severity);

        try
        {
            _db.SecurityAuditEvents.Add(audit);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unable to save security audit event {EventType}", eventType);
            _db.Entry(audit).State = EntityState.Detached;
        }
    }

    private static int Classify(string eventType, string category, int? statusCode)
    {
        if (eventType == "LoginFailure") return (int)SeverityLevel.Medium;
        if (eventType == "AuthorizationFailure") return (int)SeverityLevel.High;
        if (eventType == "UnhandledException" || statusCode is >= 500) return (int)SeverityLevel.High;
        if (category == "DatabaseChange" || category == "SensitiveAccess") return (int)SeverityLevel.Low;
        if (statusCode is >= 400) return (int)SeverityLevel.Medium;
        return (int)SeverityLevel.Low;
    }
}
