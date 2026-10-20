using Application.Interfaces;

namespace API.Services;

public sealed class CurrentTenantService : ICurrentTenantService
{
    public Guid? TenantId { get; private set; }
}