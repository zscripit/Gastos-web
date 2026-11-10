using System.Net;
using System.Text;
using Microsoft.JSInterop;

namespace GastosApp.Web.Tests;

/// <summary>
/// HttpMessageHandler programable: cada petición recibe la siguiente respuesta (o excepción) de la cola
/// y queda registrada para poder verificar cuántas veces se llamó al servidor.
/// </summary>
public sealed class FakeHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _replies = new();
    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHandler Reply(HttpStatusCode status, string? body = null)
    {
        _replies.Enqueue(_ => Response(status, body));
        return this;
    }

    /// <summary>Encabezados con éxito, pero la conexión se corta al leer el cuerpo.</summary>
    public FakeHandler ReplyAndDropBody(HttpStatusCode status, Exception exception)
    {
        _replies.Enqueue(_ => new HttpResponseMessage(status) { Content = new DroppedContent(exception) });
        return this;
    }

    private sealed class DroppedContent(Exception exception) : HttpContent
    {
        protected override Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context) => throw exception;
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }

    public FakeHandler Throw(Exception exception)
    {
        _replies.Enqueue(_ => throw exception);
        return this;
    }

    public static HttpResponseMessage Response(HttpStatusCode status, string? body) => new(status)
    {
        Content = body is null ? new ByteArrayContent([]) : new StringContent(body, Encoding.UTF8, "application/json")
    };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        if (_replies.Count == 0) throw new InvalidOperationException("La prueba no programó más respuestas.");
        return Task.FromResult(_replies.Dequeue()(request));
    }
}

/// <summary>Sustituye a window.localStorage: lo que se guarda vive en un diccionario.</summary>
public sealed class FakeJsRuntime : IJSRuntime
{
    public Dictionary<string, string> Storage { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = args?.FirstOrDefault()?.ToString() ?? "";
        switch (identifier)
        {
            case "localStorage.getItem":
                return ValueTask.FromResult((TValue)(object?)Storage.GetValueOrDefault(key)!);
            case "localStorage.setItem":
                Storage[key] = args![1]!.ToString()!;
                break;
            case "localStorage.removeItem":
                Storage.Remove(key);
                break;
        }
        return ValueTask.FromResult(default(TValue)!);
    }
}

public sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.test") };
}
