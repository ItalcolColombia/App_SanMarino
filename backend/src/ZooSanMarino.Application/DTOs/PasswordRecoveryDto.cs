// src/ZooSanMarino.Application/DTOs/PasswordRecoveryDto.cs
using System.ComponentModel.DataAnnotations;
using ZooSanMarino.Application.Attributes;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para solicitar recuperación de contraseña
/// </summary>
public class PasswordRecoveryRequestDto
{
    /// <summary>Correo electrónico del usuario</summary>
    [Required(ErrorMessage = "El correo electrónico es obligatorio")]
    [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
    [NoSqlInjection(ErrorMessage = "El correo electrónico contiene caracteres no permitidos")]
    [MaxLength(255, ErrorMessage = "El correo electrónico no puede exceder 255 caracteres")]
    public string Email { get; set; } = null!;
}

/// <summary>
/// DTO para respuesta de recuperación de contraseña
/// </summary>
public class PasswordRecoveryResponseDto
{
    /// <summary>Indica si la solicitud fue procesada exitosamente</summary>
    public bool Success { get; set; }
    
    /// <summary>Mensaje descriptivo del resultado</summary>
    public string Message { get; set; } = null!;
    
    /// <summary>Indica si se encontró el usuario</summary>
    public bool UserFound { get; set; }
    
    /// <summary>Indica si se envió el email</summary>
    public bool EmailSent { get; set; }
    
    /// <summary>ID del correo en la cola (para consultar estado)</summary>
    public int? EmailQueueId { get; set; }
}

/// <summary>
/// DTO para validar e usar un token de restablecimiento de contraseña de un solo uso
/// </summary>
public class ValidatePasswordResetTokenDto
{
    /// <summary>Token de restablecimiento de contraseña</summary>
    [Required(ErrorMessage = "El token es obligatorio")]
    [MaxLength(512, ErrorMessage = "El token no puede exceder 512 caracteres")]
    public string Token { get; set; } = null!;

    /// <summary>Nueva contraseña</summary>
    [Required(ErrorMessage = "La contraseña es obligatoria")]
    [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres")]
    [MaxLength(100, ErrorMessage = "La contraseña no puede exceder 100 caracteres")]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$",
        ErrorMessage = "La contraseña debe incluir al menos una letra y un número")]
    public string NewPassword { get; set; } = null!;
}

/// <summary>
/// DTO para respuesta de validación de token
/// </summary>
public class ValidatePasswordResetTokenResponseDto
{
    /// <summary>Indica si el token es válido y fue consumido exitosamente</summary>
    public bool Success { get; set; }

    /// <summary>Mensaje descriptivo del resultado</summary>
    public string Message { get; set; } = null!;
}
