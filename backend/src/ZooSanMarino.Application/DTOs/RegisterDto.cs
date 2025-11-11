// ZooSanMarino.Application/DTOs/RegisterDto.cs
using System.ComponentModel.DataAnnotations;
using ZooSanMarino.Application.Attributes;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para registrar un nuevo usuario junto a su login.
/// </summary>
public class RegisterDto
{
    // Datos de Login
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
    [NoSqlInjection(ErrorMessage = "El correo electrónico contiene caracteres no permitidos")]
    [MaxLength(255, ErrorMessage = "El correo electrónico no puede exceder 255 caracteres")]
    public string Email { get; set; } = null!;

    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    [MaxLength(100, ErrorMessage = "La contraseña no puede exceder 100 caracteres")]
    // NOTA: No se valida NoSqlInjection en contraseñas porque pueden contener cualquier carácter especial
    public string Password { get; set; } = null!;

    // Datos del Usuario
    [Required(ErrorMessage = "El apellido es obligatorio")]
    [MaxLength(100, ErrorMessage = "El apellido no puede exceder 100 caracteres")]
    [NoSqlInjection(ErrorMessage = "El apellido contiene caracteres no permitidos")]
    public string SurName { get; set; } = null!;

    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    [NoSqlInjection(ErrorMessage = "El nombre contiene caracteres no permitidos")]
    public string FirstName { get; set; } = null!;

    [Required(ErrorMessage = "La cédula es obligatoria")]
    [MaxLength(50, ErrorMessage = "La cédula no puede exceder 50 caracteres")]
    [NoSqlInjection(ErrorMessage = "La cédula contiene caracteres no permitidos")]
    public string Cedula { get; set; } = null!;

    [MaxLength(50, ErrorMessage = "El teléfono no puede exceder 50 caracteres")]
    [NoSqlInjection(ErrorMessage = "El teléfono contiene caracteres no permitidos")]
    public string Telefono { get; set; } = null!;

    [MaxLength(200, ErrorMessage = "La ubicación no puede exceder 200 caracteres")]
    [NoSqlInjection(ErrorMessage = "La ubicación contiene caracteres no permitidos")]
    public string Ubicacion { get; set; } = null!;

    // Asignación multiempresa y roles
    public int[] CompanyIds { get; set; } = Array.Empty<int>();
    public int[]? RoleIds { get; set; }
}
