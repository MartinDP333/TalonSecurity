using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class SecurityDbContext : DbContext
{
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid? _currentTenantId;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options, ICurrentTenantService currentTenantService) : base(options)
        => _currentTenantId = currentTenantService.TenantId;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options) : base(options)
        => _currentTenantId = SystemTenantId;

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ThreatLog> ThreatLogs => Set<ThreatLog>();
    public DbSet<IpReputation> IpReputations => Set<IpReputation>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AutomatedResponseAction> AutomatedResponseActions => Set<AutomatedResponseAction>();
    public DbSet<VulnerabilityReport> VulnerabilityReports => Set<VulnerabilityReport>();
    public DbSet<SimulationScenario> SimulationScenarios => Set<SimulationScenario>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();
    public DbSet<UserAccount> UserAccounts => Set<UserAccount>();
    public DbSet<ProtectedSite> ProtectedSites => Set<ProtectedSite>();
    public DbSet<SecurityAuditEvent> SecurityAuditEvents => Set<SecurityAuditEvent>();

    public override int SaveChanges() { AddEntityAuditEvents(); return base.SaveChanges(); }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { AddEntityAuditEvents(); return base.SaveChangesAsync(cancellationToken); }

    private void AddEntityAuditEvents()
    {
        ChangeTracker.DetectChanges();
        var tracked = ChangeTracker.Entries().Where(e => e.Entity is not SecurityAuditEvent && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        foreach (var entry in tracked)
        {
            var tenantId = entry.Entity switch
            {
                ThreatLog x => x.TenantId,
                IpReputation x => x.TenantId,
                AlertRule x => x.TenantId,
                Notification x => x.TenantId,
                AutomatedResponseAction x => x.TenantId,
                VulnerabilityReport x => x.TenantId,
                SimulationScenario x => x.TenantId,
                TenantMembership x => x.TenantId,
                ProtectedSite x => x.TenantId,
                _ => _currentTenantId
            };
            var name = entry.Entity.GetType().Name;
            var changes = entry.Properties.Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Select(p => p.Metadata.Name).Where(n => n is not "PasswordHash" and not "RawPayload")
                .Take(30);
            SecurityAuditEvents.Add(new SecurityAuditEvent(tenantId, null, $"Entity{entry.State}", "DatabaseChange", "INTERNAL", name, null, null, string.Join(",", changes)));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ThreatLog>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<IpReputation>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<AlertRule>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<AutomatedResponseAction>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<VulnerabilityReport>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<SimulationScenario>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<ProtectedSite>().HasQueryFilter(e => _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<SecurityAuditEvent>().HasQueryFilter(e => e.TenantId.HasValue && _currentTenantId.HasValue && e.TenantId == _currentTenantId.Value);

        modelBuilder.Entity<SecurityAuditEvent>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).IsRequired().HasMaxLength(100);
            e.Property(x => x.HttpMethod).IsRequired().HasMaxLength(20);
            e.Property(x => x.RequestPath).IsRequired().HasMaxLength(500);
            e.Property(x => x.SourceIp).HasMaxLength(45);
            e.Property(x => x.Details).IsRequired().HasMaxLength(2000);
            e.Property(x => x.Severity).IsRequired();
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });

        modelBuilder.Entity<ThreatLog>(entity =>
        {
            entity.Property(e => e.SourceIp).IsRequired().HasMaxLength(45);
            entity.Property(e => e.RequestPath).HasMaxLength(500);
            entity.Property(e => e.AttackType).HasMaxLength(100);
            entity.Property(e => e.CountryCode).HasMaxLength(5);
            entity.Property(e => e.Severity).HasConversion<string>();
            entity.HasIndex(e => new { e.TenantId, e.SourceIp });
            entity.HasIndex(e => new { e.TenantId, e.LastSeenAt });
        });
        modelBuilder.Entity<IpReputation>().HasIndex(e => new { e.TenantId, e.IpAddress }).IsUnique();
        modelBuilder.Entity<UserAccount>(e => { e.Property(x => x.UserName).IsRequired().HasMaxLength(200); e.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500); e.HasIndex(x => x.UserName).IsUnique(); });
        modelBuilder.Entity<ProtectedSite>(e => { e.Property(x => x.Hostname).IsRequired().HasMaxLength(255); e.Property(x => x.OriginUrl).IsRequired().HasMaxLength(500); e.HasIndex(x => x.Hostname).IsUnique(); e.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade); });
        modelBuilder.Entity<TenantMembership>(e => { e.Property(x => x.Role).IsRequired().HasMaxLength(50); e.HasOne(x => x.Tenant).WithMany(x => x.Memberships).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade); e.HasOne(x => x.UserAccount).WithMany(x => x.Memberships).HasForeignKey(x => x.UserAccountId).OnDelete(DeleteBehavior.Cascade); e.HasIndex(x => new { x.TenantId, x.UserAccountId }).IsUnique(); });
        modelBuilder.Entity<Tenant>().HasMany(x => x.ThreatLogs).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.IpReputations).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.AlertRules).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.Notifications).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.AutomatedResponseActions).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.VulnerabilityReports).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Tenant>().HasMany(x => x.SimulationScenarios).WithOne(x => x.Tenant).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ThreatLog>().HasOne(x => x.Notification).WithOne(x => x.ThreatLog).HasForeignKey<Notification>(x => x.ThreatLogId).OnDelete(DeleteBehavior.NoAction);
        modelBuilder.Entity<ThreatLog>().HasOne(x => x.ResponseAction).WithOne(x => x.ThreatLog).HasForeignKey<AutomatedResponseAction>(x => x.ThreatLogId).OnDelete(DeleteBehavior.NoAction);
    }
}
