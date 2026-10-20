using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Services;

public class ThreatLogService : IThreatLogService
{
    private readonly SecurityDbContext _context;

    public ThreatLogService(SecurityDbContext context)
    {
        _context = context;
    }

    public async Task LogThreatAsync(
        Guid tenantId,
        string sourceIp,
        string requestPath,
        string attackType,
        SeverityLevel severity,
        string rawPayload,
        string countryCode)
    {
        var existingLog = await _context.ThreatLogs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.SourceIp == sourceIp
                                   && t.RequestPath == requestPath
                                   && t.TenantId == tenantId);

        if (existingLog != null)
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
