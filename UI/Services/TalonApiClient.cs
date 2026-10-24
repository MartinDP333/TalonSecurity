using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace UI.Services;

public sealed class TalonApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly UiAuthState _auth;

    public TalonApiClient(IHttpClientFactory factory, UiAuthState auth) => (_factory, _auth) = (factory, auth);

    public async Task<IReadOnlyList<AvailableTenant>?> GetAvailableTenantsAsync(string u, string p, CancellationToken ct = default)
    {
        var r = await _factory.CreateClient("TalonApi").PostAsJsonAsync("api/auth/available-tenants", new { userName = u, password = p }, ct);
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<List<AvailableTenant>>(cancellationToken: ct) : null;
    }

    public async Task<IReadOnlyList<AvailableTenant>?> GetConnectedTenantsAsync(CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync("api/tenants", ct);
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<List<AvailableTenant>>(cancellationToken: ct) : null;
    }

    public async Task<CartResult?> AddCartItemAsync(string item, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.PostAsJsonAsync("api/cart/add", item, ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Cart API returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<CartResult>(cancellationToken: ct);
    }

    public async Task<BankingDashboard?> GetBankingDashboardAsync(CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync("api/banking/dashboard", ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Banking API returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<BankingDashboard>(cancellationToken: ct);
    }

    public async Task<BankingTransferResult?> TransferBankAsync(string fromAccount, string toAccount, decimal amount, string? description = null, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.PostAsJsonAsync("api/banking/transfer", new { fromAccount, toAccount, amount, description }, ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Banking transfer returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<BankingTransferResult>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<AdminUser>?> GetAdminUsersAsync(CancellationToken ct = default) => await GetAdminList<AdminUser>("api/platform-admin/users", ct);
    public async Task<IReadOnlyList<AdminTenant>?> GetAdminTenantsAsync(CancellationToken ct = default) => await GetAdminList<AdminTenant>("api/platform-admin/tenants", ct);
    public async Task<IReadOnlyList<AdminMembership>?> GetAdminMembershipsAsync(CancellationToken ct = default) => await GetAdminList<AdminMembership>("api/platform-admin/memberships", ct);

    private async Task<IReadOnlyList<T>?> GetAdminList<T>(string path, CancellationToken ct)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync(path, ct);
        if (!r.IsSuccessStatusCode)
        {
            await AuthFailedAsync(r);
            return null;
        }
        return await r.Content.ReadFromJsonAsync<List<T>>(cancellationToken: ct);
    }

    private async Task<bool> AuthFailedAsync(HttpResponseMessage r)
    {
        if (r.StatusCode == System.Net.HttpStatusCode.Unauthorized || r.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            SessionExpired = true;
            await _auth.ClearAsync();
            return true;
        }
        return false;
    }

    public bool SessionExpired { get; private set; }
    public async Task<bool> CreateAdminUserAsync(string userName, string password, CancellationToken ct = default) => await AdminPost("api/platform-admin/users", new { userName, password }, ct);
    public async Task<bool> CreateAdminTenantAsync(string name, string ownerUserName, CancellationToken ct = default) => await AdminPost("api/platform-admin/tenants", new { name, ownerUserName }, ct);
    public async Task<bool> AddAdminMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default) => await AdminPost("api/platform-admin/memberships", new { tenantId, userName, role }, ct);
    public async Task<bool> UpdateAdminMembershipAsync(Guid tenantId, string userName, string role, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return false;
        var r = await c.PutAsJsonAsync("api/platform-admin/memberships", new { tenantId, userName, role }, ct);
        if (!r.IsSuccessStatusCode) await AuthFailedAsync(r);
        return r.IsSuccessStatusCode;
    }

    public async Task<IReadOnlyList<string>?> GetTenantRolesAsync(CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync("api/memberships/roles", ct);
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<List<string>>(cancellationToken: ct) : null;
    }

    public async Task<IReadOnlyList<MemberView>?> GetMembershipsAsync(CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync("api/memberships", ct);
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<List<MemberView>>(cancellationToken: ct) : null;
    }

    public async Task<bool> AddMembershipAsync(string userName, string role, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return false;
        var r = await c.PostAsJsonAsync("api/memberships", new { userName, role }, ct);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> RemoveMembershipAsync(string userName, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return false;
        var r = await c.DeleteAsync($"api/memberships/{Uri.EscapeDataString(userName)}", ct);
        return r.IsSuccessStatusCode;
    }

    public async Task<bool> ActivateMembershipAsync(string userName, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return false;
        var r = await c.PostAsync($"api/memberships/{Uri.EscapeDataString(userName)}/activate", null, ct);
        return r.IsSuccessStatusCode;
    }

    public async Task<LoginResponse?> LoginAsync(string u, string p, string tenant, CancellationToken ct = default) => await Authenticate("api/auth/login", new { userName = u, password = p, tenantName = tenant }, ct);
    public async Task<LoginResponse?> SwitchTenantAsync(string tenant, CancellationToken ct = default) => await Authenticate("api/auth/switch-tenant", new { tenantName = tenant }, ct);

    private async Task<LoginResponse?> Authenticate(string path, object body, CancellationToken ct)
    {
        var c = _factory.CreateClient("TalonApi");
        if (path.Contains("switch"))
        {
            var t = _auth.AccessToken;
            if (t is null) return null;
            c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
        }
        var r = await c.PostAsJsonAsync(path, body, ct);
        if (!r.IsSuccessStatusCode) return null;
        var x = await r.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: ct);
        if (x is not null) await _auth.SetAsync(x);
        return x;
    }

    public async Task<IReadOnlyList<ThreatLogView>?> GetThreatLogsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        SetActiveTenantHeader(c);
        var query = "api/threatlogs" + (since.HasValue ? $"?since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}" : "");
        using var r = await c.GetAsync(query, ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Threat-log API returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<List<ThreatLogView>>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<TimelineItem>?> GetTimelineAsync(string type = "all", DateTime? since = null, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        SetActiveTenantHeader(c);
        var query = "api/threatlogs/timeline?type=" + Uri.EscapeDataString(type ?? "all") + (since.HasValue ? $"&since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}" : "");
        using var r = await c.GetAsync(query, ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Threat-log timeline returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<List<TimelineItem>>(cancellationToken: ct);
    }

    public async Task<IReadOnlyList<PriorityNotification>?> GetPriorityNotificationsAsync(int take = 8, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var r = await c.GetAsync($"api/threatlogs/notifications?take={Math.Max(1, take)}", ct);
        if (!r.IsSuccessStatusCode)
        {
            if (await AuthFailedAsync(r)) throw new InvalidOperationException("Your session is no longer valid. Please log in again.");
            throw new HttpRequestException($"Priority notification API returned {(int)r.StatusCode} ({r.StatusCode}).", null, r.StatusCode);
        }
        return await r.Content.ReadFromJsonAsync<List<PriorityNotification>>(cancellationToken: ct);
    }

    private void SetActiveTenantHeader(HttpClient client)
    {
        var tenantId = _auth.TenantId;
        if (!tenantId.HasValue) throw new InvalidOperationException("No active tenant is selected.");
        client.DefaultRequestHeaders.Remove("X-Tenant-Id");
        client.DefaultRequestHeaders.Add("X-Tenant-Id", tenantId.Value.ToString());
    }

    public async Task<IReadOnlyList<AuditEventView>?> GetAuditEventsAsync(DateTime? since = null, CancellationToken ct = default)
    {
        var c = Authenticated();
        if (c is null) return null;
        var query = "api/threatlogs/audit" + (since.HasValue ? $"?since={Uri.EscapeDataString(since.Value.ToUniversalTime().ToString("O"))}" : "");
        var r = await c.GetAsync(query, ct);
        return r.IsSuccessStatusCode ? await r.Content.ReadFromJsonAsync<List<AuditEventView>>(cancellationToken: ct) : null;
    }

    public async Task RestoreAuthAsync() => await _auth.RestoreAsync();
    public async Task LogoutAsync() => await _auth.ClearAsync();
    public string? AccessToken => _auth.AccessToken;
    public bool IsLoggedIn => _auth.IsLoggedIn;
    public string? CurrentTenantName => _auth.TenantName;
    public string? CurrentTenantId => _auth.TenantId?.ToString();
    public string? CurrentTenantRole => _auth.Role;
    public bool IsPlatformAdmin => _auth.IsPlatformAdmin;

    private async Task<bool> AdminPost(string path, object body, CancellationToken ct)
    {
        var c = Authenticated();
        if (c is null) return false;
        var r = await c.PostAsJsonAsync(path, body, ct);
        if (!r.IsSuccessStatusCode) await AuthFailedAsync(r);
        return r.IsSuccessStatusCode;
    }

    private HttpClient? Authenticated()
    {
        var t = _auth.AccessToken;
        if (string.IsNullOrWhiteSpace(t)) return null;
        var c = _factory.CreateClient("TalonApi");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", t);
        return c;
    }

    public sealed record AdminUser(Guid Id, string UserName, bool IsPlatformAdmin);
    public sealed record AdminTenant(Guid Id, string Name);
    public sealed record AdminMembership(Guid TenantId, string TenantName, string UserName, string Role, bool IsActive);
    public sealed record AvailableTenant(Guid TenantId, string TenantName, string Role);
    public sealed record MemberView(string UserName, string Role, bool IsActive, Guid TenantId);
    public sealed record LoginResponse(string AccessToken, Guid TenantId, string TenantName, string Role, bool IsPlatformAdmin);
    public sealed record TimelineItem(Guid Id, string RecordType, DateTime OccurredAt, string EventType, string? SourceIp, string RequestPath, int Severity, int? HitCount, string? Details);
    public sealed record PriorityNotification(Guid TenantId, string TenantName, string RecordType, string EventType, int Severity, string? SourceIp, string RequestPath, string Details, DateTime OccurredAt);
    public sealed record ThreatLogView(Guid Id, string SourceIp, string RequestPath, string AttackType, int Severity, int HitCount, DateTime FirstSeenAt, DateTime LastSeenAt, string CountryCode);
    public sealed record AuditEventView(Guid Id, string EventType, string Category, string? SourceIp, string RequestPath, int Severity, DateTime OccurredAt, string Details);
    public sealed record CartResult(string Message, string Ip, Guid? TenantId);
    public sealed record BankingDashboard(string Application, Guid? TenantId, string Message, string AccountHolder, string AccountNumber, decimal AvailableBalance, string SourceIp, DateTime Timestamp);
    public sealed record BankingTransferResult(string Application, Guid? TenantId, string Message, string SourceIp, string Status, DateTime Timestamp);
}
