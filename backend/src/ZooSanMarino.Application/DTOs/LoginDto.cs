// ZooSanMarino.Application/DTOs/LoginDto.cs
using System.ComponentModel.DataAnnotations;
using ZooSanMarino.Application.Attributes;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para login del usuario.
/// </summary>
public class LoginDto
{
    /// <summary>Correo electrónico</summary>
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
    [NoSqlInjection(ErrorMessage = "El correo electrónico contiene caracteres no permitidos")]
    [MaxLength(255, ErrorMessage = "El correo electrónico no puede exceder 255 caracteres")]
    public string Email { get; set; } = null!;

    /// <summary>Contraseña</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(6, ErrorMessage = "La contraseña debe tener al menos 6 caracteres")]
    [MaxLength(100, ErrorMessage = "La contraseña no puede exceder 100 caracteres")]
    // NOTA: No se valida NoSqlInjection en contraseñas porque pueden contener cualquier carácter especial
    public string Password { get; set; } = null!;

    /// <summary>ID de la empresa desde la cual inicia sesión (opcional)</summary>
    public int? CompanyId { get; set; }

    /// <summary>Token de reCAPTCHA (opcional, solo requerido en producción)</summary>
    [NoSqlInjection(ErrorMessage = "El token de reCAPTCHA contiene caracteres no permitidos")]
    public string? RecaptchaToken { get; set; }
}
