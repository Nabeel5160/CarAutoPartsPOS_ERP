using Blazored.LocalStorage;
using Blazored.Toast;
using CarAutoParts.Web;
using CarAutoParts.Web.Auth;
using CarAutoParts.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBase = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5280";
// When opened via LAN IP (not localhost), point API at the same host so remote devices work.
try
{
    var webUri = new Uri(builder.HostEnvironment.BaseAddress);
    if (webUri.Host is not ("localhost" or "127.0.0.1") &&
        apiBase.Contains("localhost", StringComparison.OrdinalIgnoreCase))
    {
        apiBase = $"{webUri.Scheme}://{webUri.Host}:5280";
    }
}
catch { /* keep configured ApiBaseUrl */ }

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddBlazoredToast();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<JwtAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<JwtAuthStateProvider>());
builder.Services.AddScoped<PermissionService>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<AuthorizedHandler>();

builder.Services.AddScoped(sp =>
{
    // In Blazor WASM, use the browser handler as the innermost handler.
    var authHandler = sp.GetRequiredService<AuthorizedHandler>();
    authHandler.InnerHandler = new HttpClientHandler();
    return new HttpClient(authHandler)
    {
        BaseAddress = new Uri(apiBase.TrimEnd('/') + "/")
    };
});

builder.Services.AddScoped<ApiClient>();
builder.Services.AddScoped<AuthApiService>();
builder.Services.AddScoped<CapApiService>();
builder.Services.AddScoped<LocaleService>();

await builder.Build().RunAsync();
