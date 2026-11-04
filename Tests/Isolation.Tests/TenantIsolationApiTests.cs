using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Domain.Entities;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Isolation.Tests;

public sealed class TenantIsolationApiTests : IClassFixture<TenantIsolationApiFactory>
{
    private readonly TenantIsolationApiFactory _factory;

    public TenantIsolationApiTests(TenantIsolationApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Tenant_switch_changes_data_tenant_and_keeps_audit_and_site_tenant_scoped()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("alpha-user", TenantIsolationApiFactory.AlphaId,
            TenantIsolationApiFactory.AlphaId, "webshop.talon.test", platformAdmin: true);
        client.DefaultRequestHeaders.Host = "webshop.talon.test";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var switchResponse = await client.PostAsJsonAsync("/api/auth/switch-tenant", new { tenantName = "Beta" });
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);
        using var switchJson = JsonDocument.Parse(await switchResponse.Content.ReadAsStringAsync());
        var switchRoot = switchJson.RootElement;
        Assert.Equal(TenantIsolationApiFactory.BetaId, switchRoot.GetProperty("tenantId").GetGuid());
        Assert.Equal("webshop.talon.test", switchRoot.GetProperty("hostname").GetString());
        Assert.Equal(TenantIsolationApiFactory.AlphaId, switchRoot.GetProperty("siteTenantId").GetGuid());

        var switchedToken = switchRoot.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(switchedToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", switchedToken);
        var currentResponse = await client.GetAsync("/api/tenants/current");
        Assert.Equal(HttpStatusCode.OK, currentResponse.StatusCode);
        using var currentJson = JsonDocument.Parse(await currentResponse.Content.ReadAsStringAsync());
        Assert.Equal(TenantIsolationApiFactory.BetaId, currentJson.RootElement.GetProperty("id").GetGuid());

        var auditResponse = await client.GetAsync("/api/threatlogs/audit");
        Assert.Equal(HttpStatusCode.OK, auditResponse.StatusCode);
        using var auditJson = JsonDocument.Parse(await auditResponse.Content.ReadAsStringAsync());
        Assert.Contains(auditJson.RootElement.EnumerateArray(), audit => audit.GetProperty("eventType").GetString() == "BetaAudit");
        Assert.DoesNotContain(auditJson.RootElement.EnumerateArray(), audit => audit.GetProperty("eventType").GetString() == "AlphaAudit");

        var threatResponse = await client.GetAsync("/api/threatlogs");
        Assert.Equal(HttpStatusCode.OK, threatResponse.StatusCode);
        using var threatJson = JsonDocument.Parse(await threatResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, threatJson.RootElement.GetArrayLength());
        Assert.Equal("/beta-only", threatJson.RootElement[0].GetProperty("requestPath").GetString());

        var alphaAuditResponse = await client.GetAsync("/api/threatlogs/audit");
        Assert.Equal(HttpStatusCode.OK, alphaAuditResponse.StatusCode);
    }

    [Fact]
    public async Task Hostname_bound_token_is_rejected_on_another_protected_site()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("alpha-user", TenantIsolationApiFactory.BetaId,
            TenantIsolationApiFactory.AlphaId, "webshop.talon.test", role: "Viewer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Host = "bank.talon.test";

        var response = await client.GetAsync("/api/tenants/current");
        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
    }

    [Fact]
    public async Task User_cannot_select_tenant_without_active_membership()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("beta-user", TenantIsolationApiFactory.BetaId,
            TenantIsolationApiFactory.BetaId, "bank.talon.test", role: "Viewer");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Host = "bank.talon.test";
        var body = new { TenantName = "Alpha" };
        var response = await client.PostAsJsonAsync("/api/auth/switch-tenant", body);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
        Assert.Empty(await db.SecurityAuditEvents.IgnoreQueryFilters().Where(x => x.RequestPath == "/api/tenants/current" && x.TenantId == TenantIsolationApiFactory.BetaId).ToListAsync());
    }
}
