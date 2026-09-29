using Microsoft.EntityFrameworkCore;
using Application.Interfaces;
using Domain.Entities;

namespace Infrastructure.Persistence;

public class SecurityDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    private readonly Guid? _currentTenantId;

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options, ICurrentTenantService currentTenantService)
        : base(options)
    {
        _currentTenantId = currentTenantService.TenantId;
    }

    public SecurityDbContext(DbContextOptions<SecurityDbContext> options)
        : base(options)
    {
        _currentTenantId = null;
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

        // FELTKONFIGURATIONER OG LÆNGDER

        modelBuilder.Entity<ThreatLog>(entity =>
        {
            entity.Property(e => e.SourceIp).IsRequired().HasMaxLength(45);
            entity.Property(e => e.RequestPath).HasMaxLength(500);
            entity.Property(e => e.AttackType).HasMaxLength(100);
            entity.Property(e => e.CountryCode).HasMaxLength(5);
            entity.Property(e => e.Severity).HasConversion<string>();

            // INDEX
            entity.HasIndex(e => new { e.TenantId, e.SourceIp });
            entity.HasIndex(e => new { e.TenantId, e.LastSeenAt });
        });

        modelBuilder.Entity<IpReputation>(entity =>
        {
            entity.Property(e => e.IpAddress).IsRequired().HasMaxLength(45);
            entity.HasIndex(e => new { e.TenantId, e.IpAddress }).IsUnique();
        });

        modelBuilder.Entity<AlertRule>(entity =>
        {
            entity.Property(e => e.RuleName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.TargetSeverity).HasConversion<string>();
        });

        modelBuilder.Entity<VulnerabilityReport>(entity =>
        {
            entity.Property(e => e.ProjectName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.PackageName).IsRequired().HasMaxLength(150);
            entity.Property(e => e.Severity).HasConversion<string>();
        });

        // RELATIONER

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasMany(t => t.ThreatLogs)
                .WithOne(l => l.Tenant)
                .HasForeignKey(l => l.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.IpReputations)
                .WithOne(i => i.Tenant)
                .HasForeignKey(i => i.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.AlertRules)
                .WithOne(a => a.Tenant)
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.Notifications)
                .WithOne(n => n.Tenant)
                .HasForeignKey(n => n.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.AutomatedResponseActions)
                .WithOne(a => a.Tenant)
                .HasForeignKey(a => a.TenantId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(t => t.VulnerabilityReports)
                .WithOne(v => v.Tenant)
                .HasForeignKey(v => v.TenantId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ThreatLog>()
            .HasOne(l => l.Notification)
            .WithOne(n => n.ThreatLog)
            .HasForeignKey<Notification>(n => n.ThreatLogId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<ThreatLog>()
            .HasOne(l => l.ResponseAction)
            .WithOne(a => a.ThreatLog)
            .HasForeignKey<AutomatedResponseAction>(a => a.ThreatLogId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}