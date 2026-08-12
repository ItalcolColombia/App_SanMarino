// src/ZooSanMarino.Application/Interfaces/IEmailService.cs
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Interfaz para el servicio de envío de correos electrónicos
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Envía el correo de "olvidé mi contraseña": un ENLACE de un solo uso al frontend, nunca el
    /// secreto en el cuerpo del mensaje.
    /// </summary>
    /// <remarks>
    /// Existe separado de <see cref="SendPasswordRecoveryEmailAsync"/> porque hasta el 12-ago-2026
    /// los dos casos compartían método: el token de restablecimiento viajaba por el parámetro
    /// <c>newPassword</c> y la plantilla lo mostraba como si fuera la contraseña del usuario.
    /// </remarks>
    /// <param name="toEmail">Correo del destinatario</param>
    /// <param name="resetToken">Token de un solo uso emitido por <c>AuthService</c></param>
    /// <param name="userName">Nombre del usuario (opcional)</param>
    /// <returns>ID del correo en la cola (null si falla al agregar a la cola)</returns>
    Task<int?> SendPasswordResetLinkEmailAsync(string toEmail, string resetToken, string? userName = null);

    /// <summary>
    /// Envía el aviso de contraseña restablecida POR UN ADMINISTRADOR (asíncrono, usando cola).
    /// Acá sí viaja una contraseña real: el administrador ya la fijó en la cuenta.
    /// </summary>
    /// <param name="toEmail">Correo del destinatario</param>
    /// <param name="newPassword">Contraseña que el administrador dejó asignada</param>
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
