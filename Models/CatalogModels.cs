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
