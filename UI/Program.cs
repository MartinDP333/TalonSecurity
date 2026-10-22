using UI.Components;
using UI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddHttpClient("TalonApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["TalonApi:BaseUrl"] ?? "http://talon-api-service/");
});
builder.Services.AddScoped<TalonApiClient>();
builder.Services.AddScoped<UiAuthState>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();
app.UseSession();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
