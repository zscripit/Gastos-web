using System.Text.Json;

namespace GastosApp.Web.Models;

/// <summary>Opciones WebAuthn de la API y el estado cifrado que hay que devolverle.</summary>
public sealed class PasskeyCeremony
{
    public JsonElement Options { get; set; }
    public string State { get; set; } = "";
}

public sealed class PasskeyDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
}
