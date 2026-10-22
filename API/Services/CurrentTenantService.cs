using System;
using Application.Interfaces;

namespace API.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));

        TenantId = tenantId;
    }
}
