using System;
using Application.Interfaces;

namespace API.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    private readonly AsyncLocal<Guid?> _tenantId = new();
    private readonly AsyncLocal<int> _crossTenantWriteDepth = new();

    public Guid? TenantId => _tenantId.Value;
    public bool CanWriteAcrossTenants => _crossTenantWriteDepth.Value > 0;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));

        _tenantId.Value = tenantId;
    }

    public IDisposable UseTenant(Guid tenantId)
    {
        var previous = TenantId;
        SetTenant(tenantId);
        return new Scope(() => _tenantId.Value = previous);
    }

    public IDisposable AllowCrossTenantWrites()
    {
        _crossTenantWriteDepth.Value++;
        return new Scope(() => _crossTenantWriteDepth.Value--);
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
