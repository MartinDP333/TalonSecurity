using Application.Interfaces;
using API.Controllers;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence;
using Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using MassTransit;
using Shared.Contracts;
using StackExchange.Redis;
using Worker.Consumers;
using Xunit;

namespace Isolation.Tests;

public sealed class TenantIsolationTests
{
    private static readonly Guid AlphaId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid BetaId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Query_filters_only_return_active_tenant_rows()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        Assert.Equal(2, await fixture.Db.ThreatLogs.CountAsync());
        Assert.Single(await fixture.Db.IpReputations.ToListAsync());
        Assert.Single(await fixture.Db.AlertRules.ToListAsync());
        Assert.Single(await fixture.Db.Notifications.ToListAsync());
        Assert.Single(await fixture.Db.AutomatedResponseActions.ToListAsync());
        Assert.Single(await fixture.Db.VulnerabilityReports.ToListAsync());
        Assert.Single(await fixture.Db.SimulationScenarios.ToListAsync());
        Assert.Single(await fixture.Db.TenantMemberships.ToListAsync());
        Assert.Single(await fixture.Db.ProtectedSites.ToListAsync());
        var alphaAudits = await fixture.Db.SecurityAuditEvents.ToListAsync();
        Assert.NotEmpty(alphaAudits);
        Assert.All(alphaAudits, audit => Assert.Equal(AlphaId, audit.TenantId));

        fixture.Tenant.UseTenant(BetaId);
        Assert.Equal(2, await fixture.Db.ThreatLogs.CountAsync());
        Assert.Contains(await fixture.Db.ThreatLogs.ToListAsync(), log => log.SourceIp == "203.0.113.20");
        var betaAudits = await fixture.Db.SecurityAuditEvents.ToListAsync();
        Assert.NotEmpty(betaAudits);
        Assert.All(betaAudits, audit => Assert.Equal(BetaId, audit.TenantId));
        Assert.Single(await fixture.Db.TenantMemberships.ToListAsync());
        Assert.Equal("bank-user", (await fixture.Db.TenantMemberships.Include(x => x.UserAccount).SingleAsync()).UserAccount.UserName);

        fixture.Tenant.ClearTenant();
        Assert.Empty(await fixture.Db.ThreatLogs.ToListAsync());
        Assert.Empty(await fixture.Db.TenantMemberships.ToListAsync());
    }

    [Fact]
    public async Task Tenantless_audit_event_is_rejected_without_an_explicit_system_scope()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.ClearTenant();
        fixture.Db.SecurityAuditEvents.Add(new SecurityAuditEvent(null, "anonymous", "LoginFailure", "Authentication", "POST", "/login", "203.0.113.1", "ZZ", 401, null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("Tenant ownership is required", exception.Message);
    }

    [Fact]
    public async Task Security_audit_event_cannot_be_written_to_another_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        fixture.Db.SecurityAuditEvents.Add(new SecurityAuditEvent(BetaId, "webshop-user", "Forged", "Test", "POST", "/", "203.0.113.1", "ZZ", 200, null));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("Cross-tenant write rejected", exception.Message);
    }

    [Fact]
    public async Task Added_entity_from_another_tenant_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        fixture.Db.ThreatLogs.Add(new ThreatLog(BetaId, "203.0.113.21", "/cross-write", "test", SeverityLevel.Low, "payload", "ZZ"));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("Cross-tenant write rejected", exception.Message);
    }

    [Fact]
    public async Task Disconnected_update_that_changes_tenant_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        var row = await fixture.Db.ThreatLogs.AsNoTracking().SingleAsync(x => x.RequestPath == "/alpha");
        fixture.Db.Entry(row).Property(nameof(ThreatLog.TenantId)).CurrentValue = BetaId;
        fixture.Db.Entry(row).Property(nameof(ThreatLog.TenantId)).IsModified = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Cross_tenant_delete_is_rejected_even_with_bypass_filter()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        var betaRow = await fixture.Db.ThreatLogs.IgnoreQueryFilters().SingleAsync(x => x.TenantId == BetaId && x.RequestPath == "/beta");
        fixture.Db.ThreatLogs.Remove(betaRow);

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
    }

    [Fact]
    public async Task Explicit_admin_scope_can_write_to_a_different_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        using (fixture.Tenant.AllowCrossTenantWrites())
        using (fixture.Tenant.UseTenant(BetaId))
        {
            fixture.Db.TenantMemberships.Add(new TenantMembership(BetaId, fixture.AlphaUserId, "Viewer"));
            await fixture.Db.SaveChangesAsync();
        }

        Assert.True(await fixture.Db.TenantMemberships.IgnoreQueryFilters().AnyAsync(x => x.TenantId == BetaId && x.UserAccountId == fixture.AlphaUserId));
    }

    [Fact]
    public async Task Notification_cannot_reference_another_tenants_threat()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        var betaThreat = await fixture.Db.ThreatLogs.IgnoreQueryFilters().SingleAsync(x => x.TenantId == BetaId && x.RequestPath == "/beta");
        fixture.Db.Notifications.Add(new Notification(AlphaId, "ops@example.test", "cross-tenant link", betaThreat.Id));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Db.SaveChangesAsync());
        Assert.Contains("Cross-tenant relationship rejected", exception.Message);
    }

    [Fact]
    public async Task Membership_admin_list_is_limited_to_selected_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Tenant.UseTenant(AlphaId);
        var service = new MembershipService(fixture.Db);
        var controller = new MembershipsController(service, fixture.Tenant)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.ControllerContext.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim("role", "Admin") }, "test", "sub", "role"));

        var result = Assert.IsType<OkObjectResult>(await controller.List(CancellationToken.None));
        var members = Assert.IsAssignableFrom<IReadOnlyList<Application.Interfaces.MembershipOverviewItem>>(result.Value);
        Assert.Single(members);
        Assert.All(members, member => Assert.Equal(AlphaId, member.TenantId));
    }

    [Fact]
    public void Platform_admin_controller_requires_platform_admin_role()
    {
        var authorize = Assert.Single(typeof(PlatformAdminController)
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), inherit: true)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>());
        Assert.Equal("PlatformAdmin", authorize.Roles);
    }

    [Fact]
    public async Task Traffic_worker_attributes_to_registered_host_not_spoofed_tenant_id()
    {
        await using var fixture = await Fixture.CreateAsync();
        var redisDatabase = new Mock<IDatabase>();
        redisDatabase.Setup(x => x.StringIncrementAsync(It.IsAny<RedisKey>(), It.IsAny<long>(), It.IsAny<CommandFlags>()))
            .ReturnsAsync(1);
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(redisDatabase.Object);
        var threatLog = new Mock<IThreatLogService>();
        fixture.Db.Tenants.Add(new Tenant(Guid.Parse("11111111-1111-1111-1111-111111111111"), "System"));
        using (fixture.Tenant.AllowCrossTenantWrites())
            await fixture.Db.SaveChangesAsync();
        fixture.Db.ChangeTracker.Clear();
        var consumer = new TrafficEventConsumer(NullLogger<TrafficEventConsumer>.Instance, multiplexer.Object, fixture.Db, threatLog.Object, fixture.Tenant);
        var message = new TrafficEvent
        {
            IpAddress = "198.51.100.90",
            TenantId = BetaId,
            Hostname = "webshop.talon.test",
            Endpoint = "/api/cart/add",
            UserAgent = "isolation-test",
            Timestamp = DateTime.UtcNow
        };
        var consume = new Mock<ConsumeContext<TrafficEvent>>();
        consume.SetupGet(x => x.Message).Returns(message);

        await consumer.Consume(consume.Object);

        Assert.True(await fixture.Db.IpReputations.IgnoreQueryFilters().AnyAsync(x => x.TenantId == AlphaId && x.IpAddress == message.IpAddress));
        Assert.False(await fixture.Db.IpReputations.IgnoreQueryFilters().AnyAsync(x => x.TenantId == BetaId && x.IpAddress == message.IpAddress));
    }

    [Fact]
    public async Task Waf_worker_uses_registered_hostname_not_event_tenant_name()
    {
        await using var fixture = await Fixture.CreateAsync();
        var threatLog = new Mock<IThreatLogService>();
        var consumer = new WafThreatEventConsumer(threatLog.Object, fixture.Db, NullLogger<WafThreatEventConsumer>.Instance, fixture.Tenant);
        var message = new WafThreatEvent
        {
            EventId = "event-1",
            SourceIp = "198.51.100.92",
            RequestPath = "/attack",
            AttackType = "test",
            RuleId = "942100",
            RuleMessage = "test",
            RawPayload = "payload",
            TenantName = "Talon Security Beta",
            Hostname = "webshop.talon.test",
            Timestamp = DateTime.UtcNow
        };
        var consume = new Mock<ConsumeContext<WafThreatEvent>>();
        consume.SetupGet(x => x.Message).Returns(message);

        await consumer.Consume(consume.Object);

        threatLog.Verify(x => x.LogThreatAsync(AlphaId, message.SourceIp, message.RequestPath, message.AttackType,
            SeverityLevel.High, message.RawPayload), Times.Once);
    }

    [Fact]
    public async Task Traffic_worker_discards_unregistered_host_even_if_event_claims_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var redisDatabase = new Mock<IDatabase>();
        var multiplexer = new Mock<IConnectionMultiplexer>();
        multiplexer.Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(redisDatabase.Object);
        var threatLog = new Mock<IThreatLogService>();
        var consumer = new TrafficEventConsumer(NullLogger<TrafficEventConsumer>.Instance, multiplexer.Object, fixture.Db, threatLog.Object, fixture.Tenant);
        var message = new TrafficEvent
        {
            IpAddress = "198.51.100.91",
            TenantId = BetaId,
            SiteTenantId = BetaId,
            Hostname = "unregistered.example.test",
            Endpoint = "/api/cart/add",
            UserAgent = "isolation-test",
            Timestamp = DateTime.UtcNow
        };
        var consume = new Mock<ConsumeContext<TrafficEvent>>();
        consume.SetupGet(x => x.Message).Returns(message);

        await consumer.Consume(consume.Object);

        multiplexer.Verify(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object?>()), Times.Never);
        Assert.False(await fixture.Db.IpReputations.IgnoreQueryFilters().AnyAsync(x => x.IpAddress == message.IpAddress));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        public SecurityDbContext Db { get; }
        public CurrentTenantService Tenant { get; }
        public Guid BankUserId { get; }
        public Guid AlphaUserId { get; }

        private Fixture(SqliteConnection connection, SecurityDbContext db, CurrentTenantService tenant, Guid bankUserId, Guid alphaUserId)
            => (_connection, Db, Tenant, BankUserId, AlphaUserId) = (connection, db, tenant, bankUserId, alphaUserId);

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<SecurityDbContext>().UseSqlite(connection).Options;
            var tenantService = new CurrentTenantService();
            var db = new SecurityDbContext(options, tenantService);
            await db.Database.EnsureCreatedAsync();

            var alpha = new Tenant(AlphaId, "Alpha");
            var beta = new Tenant(BetaId, "Beta");
            var alphaUser = new UserAccount("webshop-user", "hash-alpha");
            var bankUser = new UserAccount("bank-user", "hash-beta");
            db.Tenants.AddRange(alpha, beta);
            db.UserAccounts.AddRange(alphaUser, bankUser);
            db.TenantMemberships.AddRange(
                new TenantMembership(AlphaId, alphaUser.Id, "Admin"),
                new TenantMembership(BetaId, bankUser.Id, "Viewer"));
            db.ProtectedSites.AddRange(
                new ProtectedSite(AlphaId, "webshop.talon.test", "http://webshop.talon.test"),
                new ProtectedSite(BetaId, "bank.talon.test", "http://bank.talon.test"));
            db.ThreatLogs.AddRange(
                new ThreatLog(AlphaId, "203.0.113.10", "/alpha", "test", SeverityLevel.Low, "payload", "ZZ"),
                new ThreatLog(BetaId, "203.0.113.20", "/beta", "test", SeverityLevel.Low, "payload", "ZZ"));
            db.IpReputations.AddRange(new IpReputation(AlphaId, "203.0.113.10", 10), new IpReputation(BetaId, "203.0.113.20", 10));
            db.AlertRules.AddRange(new AlertRule(AlphaId, "alpha rule", "always", SeverityLevel.Low), new AlertRule(BetaId, "beta rule", "always", SeverityLevel.Low));
            db.Notifications.AddRange(new Notification(AlphaId, "alpha@example.test", "alpha notification"), new Notification(BetaId, "beta@example.test", "beta notification"));
            db.SimulationScenarios.AddRange(new SimulationScenario(AlphaId, "alpha", "test", "/", ""), new SimulationScenario(BetaId, "beta", "test", "/", ""));
            db.VulnerabilityReports.AddRange(
                new VulnerabilityReport(AlphaId, "alpha", "CVE-a", "alpha-package", SeverityLevel.Low),
                new VulnerabilityReport(BetaId, "beta", "CVE-b", "beta-package", SeverityLevel.Low));
            var alphaThreat = new ThreatLog(AlphaId, "203.0.113.11", "/alpha-action", "test", SeverityLevel.Low, "payload", "ZZ");
            var betaThreat = new ThreatLog(BetaId, "203.0.113.21", "/beta-action", "test", SeverityLevel.Low, "payload", "ZZ");
            db.ThreatLogs.AddRange(alphaThreat, betaThreat);
            db.AutomatedResponseActions.AddRange(
                new AutomatedResponseAction(AlphaId, alphaThreat.Id, "test", true),
                new AutomatedResponseAction(BetaId, betaThreat.Id, "test", true));
            db.SecurityAuditEvents.AddRange(
                new SecurityAuditEvent(AlphaId, "webshop-user", "LoginSuccess", "Authentication", "POST", "/login", "203.0.113.10", "ZZ", 200, null),
                new SecurityAuditEvent(BetaId, "bank-user", "LoginSuccess", "Authentication", "POST", "/login", "203.0.113.20", "ZZ", 200, null));

            tenantService.UseTenant(AlphaId);
            using (tenantService.AllowCrossTenantWrites())
                await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new Fixture(connection, db, tenantService, bankUser.Id, alphaUser.Id);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
