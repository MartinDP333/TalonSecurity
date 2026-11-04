using System;

namespace Domain.Entities;

public class TenantMembership : BaseEntity, ITenantOwnedEntity
{
    public Guid TenantId { get; private set; }
    public Guid? TenantOwnerId => TenantId;
    public Tenant Tenant { get; private set; } = null!;
    public Guid UserAccountId { get; private set; }
    public UserAccount UserAccount { get; private set; } = null!;
    public string Role { get; private set; }
    public bool IsActive { get; private set; }

    private TenantMembership() { }

    public TenantMembership(Guid tenantId, Guid userAccountId, string role)
    {
        TenantId = tenantId != Guid.Empty ? tenantId : throw new ArgumentException("Invalid tenant ID.", nameof(tenantId));
        UserAccountId = userAccountId != Guid.Empty ? userAccountId : throw new ArgumentException("Invalid user account ID.", nameof(userAccountId));
        Role = !string.IsNullOrWhiteSpace(role) ? role : throw new ArgumentException("Role is required.", nameof(role));
        IsActive = true;
    }

    public void SetRole(string role)
    {
        if (string.IsNullOrWhiteSpace(role)) throw new ArgumentException("Role is required.", nameof(role));
        Role = role;
    }

    public void Deactivate() => IsActive = false;
    public void Activate() => IsActive = true;
}
