using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text.Json;
using GastosApp.Web.Models;
using GastosApp.Web.Services;

namespace GastosApp.Web.Tests;

/// <summary>
/// Prácticas 13 y 14 · función crítica del móvil: guardar un movimiento (OperationsService.SaveTransactionAsync).
/// Alta = POST con clientId; edición = PUT con la versión leída (corrección de D-03). Cada prueba cubre uno de los
/// casos que pide la práctica: entrada válida, nula, fuera de rango, resultado esperado y manejo de excepciones.
/// </summary>
public class SaveTransactionTests
{
    private static readonly Guid Account = Guid.NewGuid(), Category = Guid.NewGuid();

    private static (OperationsService Service, FakeHandler Handler) Create(FakeHandler handler) =>
        (new OperationsService(new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://api.test") })), handler);

    private static TransactionForm Form(decimal amount = 250m, long? version = null) => new()
    {
        Name = "Despensa", Amount = amount, Date = new DateTime(2026, 11, 17), TransactionType = TransactionType.Expense,
        FinanceAccountId = Account, CategoryId = Category, Version = version
    };

    private static string Envelope(object? data, string? message = null, string? error = null) =>
        JsonSerializer.Serialize(new { error, message, data }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    private static async Task<JsonElement> SentBodyAsync(FakeHandler handler) =>
        JsonDocument.Parse(await handler.Requests[0].Content!.ReadAsStringAsync(TestContext.Current.CancellationToken)).RootElement;

    // AUT-01 · Entrada válida: la edición viaja como PUT a la URL del movimiento y lleva la versión leída.
    [Fact]
    public async Task EntradaValida_EdicionEnviaPutConLaVersion()
    {
        var id = Guid.NewGuid();
        var (service, handler) = Create(new FakeHandler().Reply(HttpStatusCode.OK, Envelope(new { id, name = "Despensa" })));

        var result = await service.SaveTransactionAsync(Form(version: 4242), id);

        Assert.True(result.Success);
        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal($"/api/Transaction/{id}", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Equal(4242, (await SentBodyAsync(handler)).GetProperty("version").GetInt64());
    }

    // AUT-02 · Entrada nula: sin versión (cliente anterior) se envía null y la API la acepta; en un alta no se
    // manda versión y el clientId de idempotencia nunca es nulo.
    [Fact]
    public async Task EntradaNula_SinVersionSeEnviaNull_YElAltaLlevaClientId()
    {
        var (edit, editHandler) = Create(new FakeHandler().Reply(HttpStatusCode.OK, Envelope(new { id = Guid.NewGuid() })));
        await edit.SaveTransactionAsync(Form(version: null), Guid.NewGuid());
        Assert.Equal(JsonValueKind.Null, (await SentBodyAsync(editHandler)).GetProperty("version").ValueKind);

        var clientId = Guid.NewGuid();
        var (add, addHandler) = Create(new FakeHandler().Reply(HttpStatusCode.Created, Envelope(new { id = Guid.NewGuid() })));
        await add.SaveTransactionAsync(Form(), null, clientId);
        var body = await SentBodyAsync(addHandler);
        Assert.False(body.TryGetProperty("version", out _));
        Assert.Equal(clientId, body.GetProperty("clientId").GetGuid());
    }

    // AUT-03 · Valores fuera de rango: el formulario rechaza montos ≤ 0 antes de llamar a la API, y si la API los
    // rechaza (400) los errores por campo llegan a la pantalla.
    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-0.01")]
    public void FueraDeRango_MontoNoPositivo_ElFormularioLoRechaza(string amount)
    {
        var form = Form(decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture));
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(form, new ValidationContext(form), errors, validateAllProperties: true));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(TransactionForm.Amount)));
    }

    [Fact]
    public async Task FueraDeRango_LaApiResponde400_SeExponenLosErroresPorCampo()
    {
        var body = Envelope(new Dictionary<string, string[]> { ["Amount"] = ["El monto debe ser mayor que cero."] }, error: "ValidationError");
        var (service, _) = Create(new FakeHandler().Reply(HttpStatusCode.BadRequest, body));

        var result = await service.SaveTransactionAsync(Form(0m), null, Guid.NewGuid());

        Assert.Equal(400, result.StatusCode);
        Assert.Equal("El monto debe ser mayor que cero.", result.ValidationErrors["Amount"][0]);
    }

    // AUT-04 · Resultado esperado: un alta exitosa devuelve el movimiento creado con el código 201.
    [Fact]
    public async Task ResultadoEsperado_AltaDevuelveElMovimientoCreado()
    {
        var id = Guid.NewGuid();
        var (service, handler) = Create(new FakeHandler().Reply(HttpStatusCode.Created, Envelope(new { id, name = "Despensa", amount = 250m })));

        var result = await service.SaveTransactionAsync(Form(), null, Guid.NewGuid());

        Assert.Equal(201, result.StatusCode);
        Assert.Equal(id, result.Data!.Id);
        Assert.Equal(250m, result.Data.Amount);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);
    }

    // AUT-05 · Manejo de excepciones: sin servidor la operación no lanza; devuelve código 0, que la pantalla usa
    // para guardar el movimiento en la cola sin conexión en lugar de darlo por guardado.
    [Fact]
    public async Task Excepcion_ServidorNoResponde_NoLanzaYDevuelveCodigo0()
    {
        var (service, _) = Create(new FakeHandler().Throw(new HttpRequestException("No such host is known.")));

        var result = await service.SaveTransactionAsync(Form(), null, Guid.NewGuid());

        Assert.False(result.Success);
        Assert.Equal(0, result.StatusCode);
        Assert.Equal("No se pudo conectar con el servidor. Revisa tu conexión.", result.Error);
    }

    // Conflicto de concurrencia (D-03): la API responde 409 y su mensaje llega intacto a la pantalla.
    [Fact]
    public async Task Excepcion_EdicionConVersionVieja_ExponeElConflicto()
    {
        var (service, _) = Create(new FakeHandler().Reply(HttpStatusCode.Conflict,
            Envelope(null, "El movimiento cambió desde que lo abriste; vuelve a cargarlo antes de guardar", "TransactionConcurrencyConflict")));

        var result = await service.SaveTransactionAsync(Form(version: 1), Guid.NewGuid());

        Assert.Equal(409, result.StatusCode);
        Assert.StartsWith("El movimiento cambió", result.Error);
    }
}
