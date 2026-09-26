using System.ComponentModel.DataAnnotations;

namespace GastosApp.Web.Models;

public sealed class BankDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class ProviderDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class CategoryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class SaveCategoryRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "Mínimo 2 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }
}

public sealed class ChangeStatusRequest
{
    public bool IsArchived { get; set; }
}

/// <summary>
/// Elemento de un catálogo global (banco o proveedor) tal como lo ve la
/// pantalla de administración. Ambos catálogos tienen la misma forma en la API,
/// así que comparten modelo y pantalla.
/// </summary>
public sealed class CatalogItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
    public bool IsArchived { get; set; }
}

public sealed class SaveCatalogItemRequest
{
    [Required(ErrorMessage = "El nombre es obligatorio.")]
    [MinLength(2, ErrorMessage = "Mínimo 2 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [Url(ErrorMessage = "Debe ser una URL válida (https://…).")]
    public string? IconUrl { get; set; }
}
