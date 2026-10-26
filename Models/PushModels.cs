namespace GastosApp.Web.Models;

public sealed record PushPublicKeyDto(string PublicKey);

/// <summary>Suscripción Web Push del navegador tal como la guarda la API.</summary>
public sealed record PushSubscriptionDto(string Endpoint, string P256dh, string Auth);
