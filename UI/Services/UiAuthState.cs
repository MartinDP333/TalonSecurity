using Microsoft.JSInterop;

namespace UI.Services;

public sealed class UiAuthState
{
    private const string StorageKey = "talon-auth";
    private readonly IJSRuntime _js;
    public string? AccessToken { get; private set; }
    public Guid? TenantId { get; private set; }
    public string? TenantName { get; private set; }
    public string? Role { get; private set; }
    public string? ProtectedSiteHostname { get; private set; }
    public Guid? SiteTenantId { get; private set; }
    public bool IsPlatformAdmin { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);

    public UiAuthState(IJSRuntime js) => _js = js;

    public async Task SetAsync(TalonApiClient.LoginResponse result)
    {
        AccessToken = result.AccessToken;
        TenantId = result.TenantId;
        TenantName = result.TenantName;
        Role = result.Role;
        ProtectedSiteHostname = result.Hostname;
        SiteTenantId = result.SiteTenantId == Guid.Empty ? null : result.SiteTenantId;
        IsPlatformAdmin = result.IsPlatformAdmin;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, string.Join("|", AccessToken, TenantId, TenantName, Role, IsPlatformAdmin, ProtectedSiteHostname ?? string.Empty, SiteTenantId?.ToString() ?? string.Empty));
    }

    public async Task RestoreAsync()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parts = raw.Split('|');
        if (parts.Length >= 5 && Guid.TryParse(parts[1], out var id))
        {
            AccessToken = parts[0];
            TenantId = id;
            TenantName = parts[2];
            Role = parts[3];
            IsPlatformAdmin = bool.TryParse(parts[4], out var admin) && admin;
            ProtectedSiteHostname = parts.Length >= 6 ? parts[5] : null;
            SiteTenantId = parts.Length >= 7 && Guid.TryParse(parts[6], out var siteTenantId) ? siteTenantId : null;
        }
    }

    public async Task ClearAsync()
    {
        AccessToken = null; TenantId = null; TenantName = null; Role = null; ProtectedSiteHostname = null; SiteTenantId = null; IsPlatformAdmin = false;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
