namespace GastosApp.Web.Models;

/// <summary>
/// Envoltura estándar con la que responde gastos-api (error / message / data).
/// </summary>
public sealed class ApiResponse<T>
{
    public string? Error { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
}

/// <summary>
/// Resultado ya interpretado de una llamada: evita que las páginas tengan que
/// tocar HttpResponseMessage o capturar excepciones de red.
/// </summary>
public sealed class ApiResult<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    /// <summary>Errores de validación agrupados por campo (respuestas 400).</summary>
    public IDictionary<string, string[]> ValidationErrors { get; init; }
        = new Dictionary<string, string[]>();

    public static ApiResult<T> Ok(T? data, int status) =>
        new() { Success = true, Data = data, StatusCode = status };

    public static ApiResult<T> Fail(string error, int status,
        IDictionary<string, string[]>? validation = null) =>
        new()
        {
            Success = false,
            Error = error,
            StatusCode = status,
            ValidationErrors = validation ?? new Dictionary<string, string[]>()
        };
}
