using System;
using System.Collections.Generic;

namespace Domain.Entities;

public class UserAccount : BaseEntity
{
    public string UserName { get; private set; }
    public string PasswordHash { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public ICollection<TenantMembership> Memberships { get; private set; } = new List<TenantMembership>();

    private UserAccount() { }

    public UserAccount(string userName, string passwordHash, bool isPlatformAdmin = false)
    {
        UserName = !string.IsNullOrWhiteSpace(userName) ? userName : throw new ArgumentException("Username is required.", nameof(userName));
        PasswordHash = !string.IsNullOrWhiteSpace(passwordHash) ? passwordHash : throw new ArgumentException("Password hash is required.", nameof(passwordHash));
        IsPlatformAdmin = isPlatformAdmin;
        CreatedAt = DateTime.UtcNow;
    }
}
