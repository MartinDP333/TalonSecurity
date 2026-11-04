using System;

namespace Application.Interfaces;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }
    bool CanWriteAcrossTenants { get; }
    void SetTenant(Guid tenantId);
    IDisposable UseTenant(Guid tenantId);
    IDisposable AllowCrossTenantWrites();
}
