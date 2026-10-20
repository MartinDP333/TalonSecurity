using Domain.Enums;

namespace Application.Interfaces
{
    public interface IThreatLogService
    {
        Task LogThreatAsync(Guid tenantId, string sourceIp, string requestPath, string attackType, SeverityLevel severity, string rawPayload, string countryCode);
    }
}