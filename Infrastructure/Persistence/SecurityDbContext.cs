using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

public class SecurityDbContext : DbContext
{
    private static readonly Guid SystemTenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly ICurrentTenantService? _currentTenantService;
    private readonly Guid? _fallbackTenantId;
    private Guid? CurrentTenantId => _currentTenantService?.TenantId ?? _fallbackTenantId;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options, ICurrentTenantService currentTenantService) : base(options)
        => _currentTenantService = currentTenantService;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options) : base(options)
        => _fallbackTenantId = SystemTenantId;

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
                _ => CurrentTenantId
            };
            var name = entry.Entity.GetType().Name;
            var changes = entry.Properties.Where(p => entry.State != EntityState.Modified || p.IsModified)
                .Select(p => p.Metadata.Name).Where(n => n is not "PasswordHash" and not "RawPayload")
                .Take(30);
            SecurityAuditEvents.Add(new SecurityAuditEvent(tenantId, null, $"Entity{entry.State}", "DatabaseChange", "INTERNAL", name, null, "ZZ", null, string.Join(",", changes)));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ThreatLog>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<IpReputation>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AlertRule>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<AutomatedResponseAction>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<VulnerabilityReport>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SimulationScenario>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<TenantMembership>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<ProtectedSite>().HasQueryFilter(e => e.TenantId == CurrentTenantId);
        modelBuilder.Entity<SecurityAuditEvent>().HasQueryFilter(e => e.TenantId == CurrentTenantId);

        modelBuilder.Entity<SecurityAuditEvent>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(200);
            e.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            e.Property(x => x.Category).IsRequired().HasMaxLength(100);
            e.Property(x => x.HttpMethod).IsRequired().HasMaxLength(20);
            e.Property(x => x.RequestPath).IsRequired().HasMaxLength(500);
            e.Property(x => x.SourceIp).HasMaxLength(45);
            e.Property(x => x.CountryCode).HasMaxLength(5).HasDefaultValue("ZZ");
            e.HasIndex(x => new { x.TenantId, x.CountryCode, x.OccurredAt });
            e.Property(x => x.Details).IsRequired().HasMaxLength(2000);
            e.Property(e => e.Severity).HasConversion<int>();
            e.HasIndex(x => new { x.TenantId, x.OccurredAt });
        });

        modelBuilder.Entity<ThreatLog>(entity =>
        {
            entity.Property(e => e.SourceIp).IsRequired().HasMaxLength(45);
            entity.Property(e => e.RequestPath).HasMaxLength(500);
            entity.Property(e => e.AttackType).HasMaxLength(100);
            entity.Property(e => e.CountryCode).HasMaxLength(5);
            entity.Property(e => e.Severity).HasConversion<int>();
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
