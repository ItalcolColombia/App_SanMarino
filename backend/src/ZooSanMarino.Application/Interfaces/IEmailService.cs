// src/ZooSanMarino.Application/Interfaces/IEmailService.cs
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Interfaz para el servicio de envío de correos electrónicos
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía un correo de recuperación de contraseña (asíncrono, usando cola)
    /// </summary>
    /// <param name="toEmail">Correo del destinatario</param>
    /// <param name="newPassword">Nueva contraseña generada</param>
    /// <param name="userName">Nombre del usuario (opcional)</param>
    /// <returns>ID del correo en la cola (null si falla al agregar a la cola)</returns>
    Task<int?> SendPasswordRecoveryEmailAsync(string toEmail, string newPassword, string? userName = null);

    /// <summary>
    /// Envía un correo de bienvenida con credenciales a un nuevo usuario (asíncrono, usando cola)
    /// </summary>
    /// <param name="toEmail">Correo del destinatario</param>
    /// <param name="password">Contraseña asignada</param>
    /// <param name="userName">Nombre completo del usuario</param>
    /// <param name="applicationUrl">URL de la aplicación</param>
    /// <returns>ID del correo en la cola (null si falla al agregar a la cola)</returns>
    Task<int?> SendWelcomeEmailAsync(string toEmail, string password, string userName, string applicationUrl);
}
