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
builder.Services.AddScoped<SessionRefresher>();
// Tiempo límite de las peticiones a la API (práctica 12, RES-03): sin él, HttpClient espera 100 s y un servidor
// que no responde dejaba "Guardando…" casi dos minutos. A los 20 s ApiClient lo reporta como tiempo agotado.
var apiTimeout = TimeSpan.FromSeconds(20);
builder.Services.AddHttpClient(SessionRefresher.ClientName, client => { client.BaseAddress = new Uri(apiBaseUrl); client.Timeout = apiTimeout; });

builder.Services.AddHttpClient<ApiClient>(client => { client.BaseAddress = new Uri(apiBaseUrl); client.Timeout = apiTimeout; })
    .AddHttpMessageHandler<AuthHeaderHandler>();

// Comprobantes: las URLs prefirmadas del storage van sin AuthHeaderHandler para no filtrar el JWT.
builder.Services.AddHttpClient<StorageClient>();

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
builder.Services.AddScoped<OperationsService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<AttachmentService>();
builder.Services.AddScoped<PushService>();
builder.Services.AddScoped<PasskeyService>();
builder.Services.AddScoped<OfflineStore>();
builder.Services.AddScoped<OfflineSync>();

await builder.Build().RunAsync();
