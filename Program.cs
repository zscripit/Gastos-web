using GastosApp.Web;
using GastosApp.Web.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;

// Toda la interfaz esta en espanol de Mexico: fechas y montos se formatean
// con esa cultura sin depender de la configuracion del navegador.
var culture = new CultureInfo("es-MX");
CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// La URL de gastos-api vive en wwwroot/appsettings.json para poder cambiarla
// entre desarrollo y publicación sin recompilar.
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5123";

builder.Services.AddScoped<LocalStorageService>();
builder.Services.AddScoped<TokenStore>();
builder.Services.AddScoped<AuthHeaderHandler>();

builder.Services.AddHttpClient<ApiClient>(client => client.BaseAddress = new Uri(apiBaseUrl))
    .AddHttpMessageHandler<AuthHeaderHandler>();

builder.Services.AddScoped<AuthenticationStateProvider, JwtAuthenticationStateProvider>();
builder.Services.AddAuthorizationCore(options =>
{
    // Las reglas viven en AppRoles para que las páginas, la navegación y las
    // redirecciones decidan exactamente igual.
    options.AddPolicy(Policies.Operational, policy =>
        policy.RequireAssertion(ctx => ctx.User.IsOperationalUser()));

    options.AddPolicy(Policies.SystemAdmin, policy =>
        policy.RequireAssertion(ctx => ctx.User.IsSystemAdmin()));
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<CatalogService>();

await builder.Build().RunAsync();
