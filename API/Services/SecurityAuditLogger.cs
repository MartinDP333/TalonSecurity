using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace API.Services;

public sealed class SecurityAuditLogger
{
    private readonly SecurityDbContext _db;
    private readonly ICurrentTenantService _tenant;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<SecurityAuditLogger> _logger;
    private readonly GeoIpCountryLookup _geoIp;
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public SecurityAuditLogger(SecurityDbContext db, ICurrentTenantService tenant, IHttpContextAccessor http, ILogger<SecurityAuditLogger> logger, GeoIpCountryLookup geoIp)
        => (_db, _tenant, _http, _logger, _geoIp) = (db, tenant, http, logger, geoIp);

    public async Task WriteAsync(string eventType, string category, string? details = null, int? statusCode = null, string? userName = null, Guid? tenantId = null, CancellationToken ct = default)
    {
        var context = _http.HttpContext;
        var sourceIp = API.ClientIpAddress.Get(context ?? new DefaultHttpContext());
        var countryCode = _geoIp.LookupCountryCode(sourceIp);
        var severity = Classify(eventType, category, statusCode);

        if (eventType == "LoginFailure" && !string.IsNullOrWhiteSpace(sourceIp))
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-15);
            var previousFailures = await _db.SecurityAuditEvents.IgnoreQueryFilters().CountAsync(
                x => x.EventType == "LoginFailure" && x.SourceIp == sourceIp && x.OccurredAt >= cutoff,
                ct);
            var totalFailures = previousFailures + 1;
            severity = totalFailures >= 5 ? SeverityLevel.Critical
                : totalFailures >= 3 ? SeverityLevel.High
                : SeverityLevel.Medium;
            details = $"{details}; failed attempts from source IP in last 15m: {totalFailures}";
        }

        var resolvedTenantId = tenantId ?? _tenant.TenantId ?? SystemTenantId;
        using var tenantScope = _tenant.UseTenant(resolvedTenantId);
        var audit = new SecurityAuditEvent(
            resolvedTenantId,
            userName ?? context?.User.FindFirst("sub")?.Value ?? context?.User.Identity?.Name,
            eventType,
            category,
            context?.Request.Method ?? "INTERNAL",
            context?.Request.Path.Value ?? "",
            sourceIp,
            countryCode,
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

    private static SeverityLevel Classify(string eventType, string category, int? statusCode)
    {
        if (eventType == "LoginFailure") return SeverityLevel.Medium;
        if (eventType == "AuthorizationFailure") return SeverityLevel.High;
        if (eventType == "UnhandledException" || statusCode is >= 500) return SeverityLevel.High;
        if (category == "DatabaseChange" || category == "SensitiveAccess") return SeverityLevel.Low;
        if (statusCode is >= 400) return SeverityLevel.Medium;
        return SeverityLevel.Low;
    }
}