using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;


namespace Infrastructure.SecurityDbContext;

public class SecurityDbContext : DbContext
{
    private readonly Guid? _currentTenantId;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantId = currentTenantService.TenantId;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ThreatLog> ThreatLogs => Set<ThreatLog>();
    public DbSet<IpReputation> IpReputations => Set<IpReputation>();
    public DbSet<AlertRule> AlertRules => Set<AlertRule>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<AutomatedResponseAction> AutomatedResponseActions => Set<AutomatedResponseAction>();
    public DbSet<VulnerabilityReport> VulnerabilityReports => Set<VulnerabilityReport>();
    public DbSet<SimulationScenario> SimulationScenarios => Set<SimulationScenario>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<ThreatLog>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<IpReputation>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<AlertRule>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<Notification>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<AutomatedResponseAction>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);
        modelBuilder.Entity<VulnerabilityReport>().HasQueryFilter(e => !_currentTenantId.HasValue || e.TenantId == _currentTenantId.Value);

        // ThreatLog Indexes (Crucial for high-throughput security logging)
        modelBuilder.Entity<ThreatLog>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.SourceIp });
            entity.HasIndex(e => new { e.TenantId, e.LastSeenAt });
            entity.Property(e => e.Severity).HasConversion<string>();
        });

        // IpReputation Unique Constraint per Tenant
        modelBuilder.Entity<IpReputation>(entity =>
        {
            entity.HasIndex(e => new { e.TenantId, e.IpAddress }).IsUnique();
        });

        // AlertRule Configuration
        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.Property(e => e.TargetSeverity).HasConversion<string>();
        });

        // VulnerabilityReport Configuration
        modelBuilder.Entity<VulnerabilityReport>(entity =>
        {
            entity.Property(e => e.Severity).HasConversion<string>();
        });

        // Relationships Setup
        modelBuilder.Entity<Tenant>()
            .HasMany(t => t.ThreatLogs)
            .WithOne(l => l.Tenant)
            .HasForeignKey(l => l.TenantId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ThreatLog>()
            .HasOne(l => l.Notification)
            .WithOne(n => n.ThreatLog)
            .HasForeignKey<Notification>(n => n.ThreatLogId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<ThreatLog>()
            .HasOne(l => l.ResponseAction)
            .WithOne(a => a.ThreatLog)
            .HasForeignKey<AutomatedResponseAction>(a => a.ThreatLogId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}