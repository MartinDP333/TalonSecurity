using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ThreatLogService : IThreatLogService
{
    private readonly SecurityDbContext _context;
    private readonly GeoIpCountryLookup _geoIp;

    public ThreatLogService(SecurityDbContext context, GeoIpCountryLookup geoIp)
    {
        _context = context;
        _geoIp = geoIp;
    }

    public async Task LogThreatAsync(
        Guid tenantId,
        string sourceIp,
        string requestPath,
        string attackType,
        SeverityLevel severity,
        string rawPayload)
    {
        var countryCode = _geoIp.LookupCountryCode(sourceIp);
        var existingLog = await _context.ThreatLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.SourceIp == sourceIp
                                   && t.RequestPath == requestPath
                                   && t.TenantId == tenantId);

        if (existingLog is not null)
        {
            existingLog.RegisterNewHit();
        }
        else
        {
            var newLog = new ThreatLog(
                tenantId: tenantId,
                sourceIp: sourceIp,
                requestPath: requestPath,
                attackType: attackType,
                severity: severity,
                rawPayload: rawPayload,
                countryCode: countryCode);

            _context.ThreatLogs.Add(newLog);
        }

        await _context.SaveChangesAsync();
    }
}
