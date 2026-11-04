namespace Domain.Entities;

/// <summary>Marks an entity whose writes must belong to the active tenant context.</summary>
public interface ITenantOwnedEntity
{
    Guid? TenantOwnerId { get; }
}
