using Application.Interfaces;

namespace Infrastructure.Services;

public class CurrentTenantService : ICurrentTenantService
{
    private int _crossTenantWriteDepth;

    public Guid? TenantId { get; private set; }
    public bool CanWriteAcrossTenants => _crossTenantWriteDepth > 0;

    public void SetTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));
        TenantId = tenantId;
    }

    public void ClearTenant() => TenantId = null;

    public IDisposable UseTenant(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));
        var previous = TenantId;
        TenantId = tenantId;
        return new Scope(() => TenantId = previous);
    }

    public IDisposable AllowCrossTenantWrites()
    {
        Interlocked.Increment(ref _crossTenantWriteDepth);
        return new Scope(() => Interlocked.Decrement(ref _crossTenantWriteDepth));
    }

    private sealed class Scope(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}
