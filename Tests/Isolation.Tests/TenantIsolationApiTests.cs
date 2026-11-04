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

    [Fact]
    public async Task Tenant_admins_manage_only_their_selected_tenant_members_and_sites()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("alpha-user", TenantIsolationApiFactory.AlphaId,
            TenantIsolationApiFactory.AlphaId, "webshop.talon.test", role: "Admin");
        client.DefaultRequestHeaders.Host = "webshop.talon.test";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var membersResponse = await client.GetAsync("/api/memberships");
        Assert.Equal(HttpStatusCode.OK, membersResponse.StatusCode);
        using var membersJson = JsonDocument.Parse(await membersResponse.Content.ReadAsStringAsync());
        Assert.Contains(membersJson.RootElement.EnumerateArray(), member => member.GetProperty("tenantId").GetGuid() == TenantIsolationApiFactory.AlphaId);
        Assert.DoesNotContain(membersJson.RootElement.EnumerateArray(), member => member.GetProperty("tenantId").GetGuid() == TenantIsolationApiFactory.BetaId);

        var siteResponse = await client.GetAsync("/api/protected-sites");
        Assert.Equal(HttpStatusCode.OK, siteResponse.StatusCode);
        using var sitesJson = JsonDocument.Parse(await siteResponse.Content.ReadAsStringAsync());
        Assert.Contains(sitesJson.RootElement.EnumerateArray(), site => site.GetProperty("hostname").GetString() == "webshop.talon.test");
        Assert.DoesNotContain(sitesJson.RootElement.EnumerateArray(), site => site.GetProperty("hostname").GetString() == "bank.talon.test");

        var inviteResponse = await client.PostAsJsonAsync("/api/memberships", new { userName = "beta-user", role = "Viewer" });
        Assert.Equal(HttpStatusCode.Created, inviteResponse.StatusCode);

        var addSiteResponse = await client.PostAsJsonAsync("/api/protected-sites", new { hostname = "alpha-secondary.talon.test", originUrl = "http://alpha-secondary.talon.test" });
        Assert.Equal(HttpStatusCode.Created, addSiteResponse.StatusCode);
    }

    [Fact]
    public async Task Viewers_are_denied_member_and_site_management()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("beta-user", TenantIsolationApiFactory.BetaId,
            TenantIsolationApiFactory.BetaId, "bank.talon.test", role: "Viewer");
        client.DefaultRequestHeaders.Host = "bank.talon.test";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var listResponse = await client.GetAsync("/api/memberships");
        Assert.Equal(HttpStatusCode.Forbidden, listResponse.StatusCode);

        var inviteResponse = await client.PostAsJsonAsync("/api/memberships", new { userName = "alpha-user", role = "Admin" });
        Assert.Equal(HttpStatusCode.Forbidden, inviteResponse.StatusCode);

        var siteResponse = await client.PostAsJsonAsync("/api/protected-sites", new { hostname = "viewer-forbidden.talon.test", originUrl = "http://viewer-forbidden.talon.test" });
        Assert.Equal(HttpStatusCode.Forbidden, siteResponse.StatusCode);
    }

    [Fact]
    public async Task Platform_admin_can_manage_memberships_across_tenants_via_authenticated_http_requests()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var token = TenantIsolationApiFactory.CreateToken("alpha-user", TenantIsolationApiFactory.AlphaId,
            TenantIsolationApiFactory.AlphaId, "webshop.talon.test", platformAdmin: true);
        client.DefaultRequestHeaders.Host = "webshop.talon.test";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using (_factory.TenantContext.UseTenant(TenantIsolationApiFactory.AlphaId))
        using (_factory.TenantContext.AllowCrossTenantWrites())
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
            db.UserAccounts.Add(new UserAccount("cross-tenant-user", "hash-cross-tenant"));
            await db.SaveChangesAsync();
        }

        var addMembershipResponse = await client.PostAsJsonAsync("/api/platform-admin/memberships", new { tenantId = TenantIsolationApiFactory.BetaId, userName = "cross-tenant-user", role = "Viewer" });
        Assert.Equal(HttpStatusCode.OK, addMembershipResponse.StatusCode);
        using var addMembershipJson = JsonDocument.Parse(await addMembershipResponse.Content.ReadAsStringAsync());
        Assert.Equal(TenantIsolationApiFactory.BetaId, addMembershipJson.RootElement.GetProperty("tenantId").GetGuid());

        var updateMembershipResponse = await client.PutAsJsonAsync("/api/platform-admin/memberships", new { tenantId = TenantIsolationApiFactory.BetaId, userName = "cross-tenant-user", role = "Admin" });
        Assert.Equal(HttpStatusCode.OK, updateMembershipResponse.StatusCode);
        using var updateMembershipJson = JsonDocument.Parse(await updateMembershipResponse.Content.ReadAsStringAsync());
        Assert.Equal("Admin", updateMembershipJson.RootElement.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Tampered_tenant_claims_are_rejected_for_tenant_data_endpoints()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var tamperedToken = TenantIsolationApiFactory.CreateToken("alpha-user", TenantIsolationApiFactory.AlphaId,
            TenantIsolationApiFactory.BetaId, "webshop.talon.test", role: "Admin");
        client.DefaultRequestHeaders.Host = "webshop.talon.test";
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tamperedToken);

        var response = await client.GetAsync("/api/tenants/current");
        Assert.Equal(HttpStatusCode.MisdirectedRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unauthenticated_requests_are_rejected_for_every_tenant_data_endpoint()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Host = "webshop.talon.test";

        var endpointPairs = new[]
        {
            "/api/tenants/current",
            "/api/tenants",
            "/api/memberships",
            "/api/memberships/roles",
            "/api/protected-sites",
            "/api/threatlogs",
            "/api/threatlogs/audit"
        };

        foreach (var endpoint in endpointPairs)
        {
            var response = await client.GetAsync(endpoint);
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.MisdirectedRequest);
        }

        var postResponses = new[]
        {
            await client.PostAsJsonAsync("/api/memberships", new { userName = "alpha-user", role = "Admin" }),
            await client.PostAsJsonAsync("/api/protected-sites", new { hostname = "unauth.talon.test", originUrl = "http://unauth.talon.test" }),
            await client.PostAsJsonAsync("/api/auth/switch-tenant", new { tenantName = "Alpha" })
        };

        foreach (var response in postResponses)
        {
            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.MisdirectedRequest);
        }

        var deleteResponse = await client.DeleteAsync("/api/protected-sites/00000000-0000-0000-0000-000000000001");
        Assert.True(deleteResponse.StatusCode == HttpStatusCode.Unauthorized || deleteResponse.StatusCode == HttpStatusCode.MisdirectedRequest);
    }
}
