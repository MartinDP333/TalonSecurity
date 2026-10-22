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
    public bool IsPlatformAdmin { get; private set; }
    public bool IsLoggedIn => !string.IsNullOrWhiteSpace(AccessToken);

    public UiAuthState(IJSRuntime js) => _js = js;

    public async Task SetAsync(TalonApiClient.LoginResponse result)
    {
        AccessToken = result.AccessToken;
        TenantId = result.TenantId;
        TenantName = result.TenantName;
        Role = result.Role;
        IsPlatformAdmin = result.IsPlatformAdmin;
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, string.Join("|", AccessToken, TenantId, TenantName, Role, IsPlatformAdmin));
    }

    public async Task RestoreAsync()
    {
        var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
        if (string.IsNullOrWhiteSpace(raw)) return;
        var parts = raw.Split('|', 5);
        if (parts.Length >= 4 && Guid.TryParse(parts[1], out var id))
        {
            AccessToken = parts[0];
            TenantId = id;
            TenantName = parts[2];
            Role = parts[3];
            IsPlatformAdmin = parts.Length == 5 && bool.TryParse(parts[4], out var admin) && admin;
        }
    }

    public async Task ClearAsync()
    {
        AccessToken = null; TenantId = null; TenantName = null; Role = null; IsPlatformAdmin = false;
        await _js.InvokeVoidAsync("localStorage.removeItem", StorageKey);
    }
}
