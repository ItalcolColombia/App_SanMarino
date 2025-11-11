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



