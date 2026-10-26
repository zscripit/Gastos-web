using System.ComponentModel.DataAnnotations;

namespace GastosApp.Web.Models;

public sealed class LoginRequest
{
    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required(ErrorMessage = "Los apellidos son obligatorios.")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "El correo es obligatorio.")]
    [EmailAddress(ErrorMessage = "El correo no tiene un formato válido.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "La contraseña es obligatoria.")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres.")]
    [RegularExpression(@"^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).+$",
        ErrorMessage = "Debe incluir al menos una mayúscula, una minúscula y un dígito.")]
    public string Password { get; set; } = string.Empty;
}

/// <summary>Payload devuelto por /Auth/Login, /Auth/Register y /Auth/Refresh (misma forma).</summary>
public sealed class AuthSession
{
    public string UserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAtUtc { get; set; }

    // Renueva el token de acceso sin pedir la contraseña. Puede faltar en sesiones guardadas antes de existir.
    public string? RefreshToken { get; set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; set; }

    public bool CanRefresh => !string.IsNullOrEmpty(RefreshToken) && RefreshTokenExpiresAtUtc > DateTimeOffset.UtcNow;
}
