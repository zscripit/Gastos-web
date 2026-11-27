using System.Net;
using System.Text.Json;
using GastosApp.Web.Models;
using GastosApp.Web.Services;

namespace GastosApp.Web.Tests;

/// <summary>
/// Método de comunicación: ApiClient.SendAsync (Services/ApiClient.cs), el único punto de contacto con la API.
///
///   D1  cacheKey != null && offline?.LikelyOffline == true && hay copia      → copia sin llamar a la red
///   T1  try { enviar
///   D2      401 && refresher != null && EnsureFreshAsync(force) != null      → reintento con sesión nueva }
///   E1  catch HttpRequestException  → copia o "No se pudo conectar…"
///   E2  catch TaskCanceledException → copia o "La petición tardó demasiado…"
///   D3  401 && hay suscriptor                                                 → avisa al layout (cierra sesión)
///   E3/E4 error al leer el cuerpo
///   D4  2xx: D5 cuerpo ilegible y no es 204 → falla; D6 guarda copia; devuelve Data
///   D7  400: mensaje ?? error ?? "Revisa los datos capturados." + errores por campo
///   D8  otro error: mensaje ?? error ?? DefaultMessage(código)
///
/// Las pruebas comparten el evento estático Unauthorized, por eso viven en una sola clase (xUnit no paraleliza dentro de una clase).
/// </summary>
public class ApiClientTests
{
    private static ApiClient Client(FakeHandler handler, SessionRefresher? refresher = null) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://api.test") }, refresher);

    private static string Envelope(object? data, string? message = null, string? error = null) =>
        JsonSerializer.Serialize(new { error, message, data }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public sealed record Item(int Id, string Name);

    // ---- Caminos exitosos (D4) -------------------------------------------------------------------------

    [Fact]
    public async Task Ok200_DevuelveDatosYCodigo()
    {
        var result = await Client(new FakeHandler().Reply(HttpStatusCode.OK, Envelope(new Item(1, "Nómina"))))
            .GetAsync<Item>("/api/FinanceAccount/1");
        Assert.True(result.Success);
        Assert.Equal(200, result.StatusCode);
        Assert.Equal("Nómina", result.Data!.Name);
    }

    [Fact]
    public async Task Created201_EnPost_EnviaCuerpoJson()
    {
        var handler = new FakeHandler().Reply(HttpStatusCode.Created, Envelope(new Item(7, "Despensa")));
        var result = await Client(handler).PostAsync<Item>("/api/Transaction", new { name = "Despensa" });
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
        Assert.Equal("{\"name\":\"Despensa\"}", await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // D5 = F por ser 204: sin cuerpo no es un error.
    [Fact]
    public async Task NoContent204_SinCuerpo_EsExito()
    {
        var result = await Client(new FakeHandler().Reply(HttpStatusCode.NoContent)).DeleteAsync<object>("/api/Transaction/1");
        Assert.True(result.Success);
        Assert.Null(result.Data);
    }

    // D5 = V: un 200 con HTML (proxy, página de error) no se muestra como éxito.
    [Fact]
    public async Task Ok200_ConCuerpoIlegible_SeReportaComoFalla()
    {
        var result = await Client(new FakeHandler().Reply(HttpStatusCode.OK, "<html>Bad gateway</html>")).GetAsync<Item>("/api/Bank");
        Assert.False(result.Success);
        Assert.Equal("El servidor devolvió una respuesta que no se pudo interpretar.", result.Error);
    }

    // Parse: cuerpo vacío en un 200 (no 204) también es ilegible.
    [Fact]
    public async Task Ok200_SinCuerpo_SeReportaComoFalla() =>
        Assert.False((await Client(new FakeHandler().Reply(HttpStatusCode.OK, "")).GetAsync<Item>("/api/Bank")).Success);

    [Fact]
    public async Task PutYPatch_UsanElMetodoCorrecto()
    {
        var handler = new FakeHandler().Reply(HttpStatusCode.OK, Envelope(1)).Reply(HttpStatusCode.OK, Envelope(2));
        var api = Client(handler);
        await api.PutAsync<int>("/api/Bank/1", new { });
        await api.PatchAsync<int>("/api/Bank/1", new { });
        Assert.Equal([HttpMethod.Put, HttpMethod.Patch], handler.Requests.Select(r => r.Method));
    }

    // ---- Errores HTTP (D7, D8) ---------------------------------------------------------------------------

    [Fact]
    public async Task BadRequest400_ConErroresPorCampo_LosExpone()
    {
        var body = Envelope(new Dictionary<string, string[]> { ["Amount"] = ["El monto debe ser mayor que cero."] }, message: "Datos inválidos");
        var result = await Client(new FakeHandler().Reply(HttpStatusCode.BadRequest, body)).PostAsync<Item>("/api/Transaction", new { });
        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Datos inválidos", result.Error);
        Assert.Equal("El monto debe ser mayor que cero.", result.ValidationErrors["Amount"][0]);
    }

    // D7: sin message usa error; sin ninguno, el texto por defecto.
    [Fact]
    public async Task BadRequest400_SoloConCodigoDeError_UsaElCodigo() =>
        Assert.Equal("TransferSameAccount", (await Client(new FakeHandler().Reply(HttpStatusCode.BadRequest, Envelope(null, error: "TransferSameAccount")))
            .PostAsync<Item>("/api/Transaction", new { })).Error);

    [Fact]
    public async Task BadRequest400_SinCuerpo_MensajePorDefecto() =>
        Assert.Equal("Revisa los datos capturados.", (await Client(new FakeHandler().Reply(HttpStatusCode.BadRequest)).PostAsync<Item>("/api/x", new { })).Error);

    // D8 con mensaje de la API.
    [Fact]
    public async Task NotFound404_ConMensaje_UsaElMensajeDeLaApi() =>
        Assert.Equal("La cuenta no existe", (await Client(new FakeHandler().Reply(HttpStatusCode.NotFound, Envelope(null, message: "La cuenta no existe")))
            .GetAsync<Item>("/api/FinanceAccount/x")).Error);

    // D8 sin cuerpo: DefaultMessage cubre cada código conocido y el caso general.
    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "No tienes permiso para realizar esta acción.")]
    [InlineData(HttpStatusCode.NotFound, "No se encontró el recurso solicitado.")]
    [InlineData(HttpStatusCode.Conflict, "El registro ya existe.")]
    [InlineData(HttpStatusCode.Locked, "La cuenta está bloqueada temporalmente.")]
    [InlineData(HttpStatusCode.TooManyRequests, "Demasiadas solicitudes seguidas. Espera un momento e inténtalo de nuevo.")]
    [InlineData(HttpStatusCode.InternalServerError, "Ocurrió un error inesperado.")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "Ocurrió un error inesperado.")]
    public async Task ErrorSinCuerpo_MensajePorCodigo(HttpStatusCode code, string expected)
    {
        var result = await Client(new FakeHandler().Reply(code)).GetAsync<Item>("/api/x");
        Assert.False(result.Success);
        Assert.Equal((int)code, result.StatusCode);
        Assert.Equal(expected, result.Error);
    }

    // ---- Excepciones de red (E1, E2) ----------------------------------------------------------------------

    [Fact]
    public async Task SinRed_HttpRequestException_NoLanzaYDevuelveFalla()
    {
        var result = await Client(new FakeHandler().Throw(new HttpRequestException("DNS"))).PostAsync<Item>("/api/Transaction", new { });
        Assert.False(result.Success);
        Assert.Equal(0, result.StatusCode);
        Assert.Equal("No se pudo conectar con el servidor. Revisa tu conexión.", result.Error);
    }

    [Fact]
    public async Task TiempoAgotado_TaskCanceledException_NoLanzaYDevuelveFalla()
    {
        var result = await Client(new FakeHandler().Throw(new TaskCanceledException())).GetAsync<Item>("/api/Bank");
        Assert.Equal(0, result.StatusCode);
        Assert.Equal("La petición tardó demasiado. Inténtalo de nuevo.", result.Error);
    }

    // El servidor respondió 201 pero la conexión se cayó al leer el cuerpo. No debe reportarse como éxito.
    // Hallazgo: HttpClient descarga el cuerpo dentro de SendAsync (ResponseContentRead por defecto), así que el
    // corte cae en E1 (status 0) y nunca llega a E3/E4 (ApiClient.cs:91-93): esas líneas son inaccesibles.
    [Fact]
    public async Task ConexionCortadaAlLeerElCuerpo_NoSeReportaComoExito()
    {
        var result = await Client(new FakeHandler().ReplyAndDropBody(HttpStatusCode.Created, new HttpRequestException("reset")))
            .PostAsync<Item>("/api/Transaction", new { });
        Assert.False(result.Success);
        Assert.Equal(0, result.StatusCode);
        Assert.Equal("No se pudo conectar con el servidor. Revisa tu conexión.", result.Error);
    }

    [Fact]
    public async Task TiempoAgotadoAlLeerElCuerpo_NoSeReportaComoExito() =>
        Assert.Equal("La petición tardó demasiado. Inténtalo de nuevo.",
            (await Client(new FakeHandler().ReplyAndDropBody(HttpStatusCode.OK, new TaskCanceledException())).GetAsync<Item>("/api/Bank")).Error);

    // ---- Autenticación (D2, D3) ---------------------------------------------------------------------------

    // D2 = F (sin refresher), D3 = V: el layout recibe el aviso para cerrar la sesión.
    [Fact]
    public async Task Unauthorized401_SinRenovacion_AvisaAlLayout()
    {
        var api = Client(new FakeHandler().Reply(HttpStatusCode.Unauthorized));
        var notified = 0;
        Func<Task> onUnauthorized = () => { notified++; return Task.CompletedTask; };
        api.Unauthorized += onUnauthorized;
        try
        {
            var result = await api.GetAsync<Item>("/api/Bank");
            Assert.Equal(401, result.StatusCode);
            Assert.Equal("Tu sesión expiró. Inicia sesión de nuevo.", result.Error);
            Assert.Equal(1, notified);
        }
        finally { api.Unauthorized -= onUnauthorized; }
    }

    // D2 = V: el refresh token renueva la sesión y la petición se repite una sola vez, con éxito.
    [Fact]
    public async Task Unauthorized401_ConRenovacionExitosa_ReintentaUnaVez()
    {
        var js = new FakeJsRuntime();
        var tokens = new TokenStore(new LocalStorageService(js));
        await tokens.SetSessionAsync(new AuthSession
        {
            UserId = "u1", AccessToken = "viejo", ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            RefreshToken = "rt", RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7)
        });
        var renewed = new AuthSession { UserId = "u1", AccessToken = "nuevo", ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(1), RefreshToken = "rt2", RefreshTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(7) };
        var authApi = new FakeHandler().Reply(HttpStatusCode.OK, JsonSerializer.Serialize(new { data = renewed }));
        var refresher = new SessionRefresher(new FakeHttpClientFactory(authApi), tokens);

        var handler = new FakeHandler().Reply(HttpStatusCode.Unauthorized).Reply(HttpStatusCode.OK, Envelope(new Item(1, "Nómina")));
        var result = await Client(handler, refresher).GetAsync<Item>("/api/FinanceAccount/1");

        Assert.True(result.Success);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal("nuevo", (await tokens.GetSessionAsync())!.AccessToken);
    }

    // D2 con EnsureFreshAsync = null (no hay sesión guardada): no se reintenta y se trata como 401.
    [Fact]
    public async Task Unauthorized401_SinSesionParaRenovar_NoReintenta()
    {
        var refresher = new SessionRefresher(new FakeHttpClientFactory(new FakeHandler()), new TokenStore(new LocalStorageService(new FakeJsRuntime())));
        var handler = new FakeHandler().Reply(HttpStatusCode.Unauthorized);
        var result = await Client(handler, refresher).GetAsync<Item>("/api/Bank");
        Assert.Equal(401, result.StatusCode);
        Assert.Single(handler.Requests);
    }
}
