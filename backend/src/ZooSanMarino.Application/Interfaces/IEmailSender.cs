// src/ZooSanMarino.Application/Interfaces/IEmailSender.cs
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Resultado de un intento de envío. <see cref="Detalle"/> es el texto de diagnóstico que
/// termina en <c>email_queue.error_message</c>, y <see cref="TipoError"/> el valor estable
/// que se guarda en <c>email_queue.error_type</c>.
/// </summary>
public sealed record EnvioCorreoResultado(bool Exitoso, string? TipoError, string? Detalle)
{
    public static EnvioCorreoResultado Ok() => new(true, null, null);

    public static EnvioCorreoResultado Error(string tipoError, string detalle) =>
        new(false, tipoError, detalle);
}

/// <summary>
/// Transporte de correo saliente. Existe para que cambiar de proveedor (SMTP, Microsoft Graph,
/// el que venga) sea agregar una implementación y una variable de entorno, en vez de reescribir
/// el procesador de la cola.
/// </summary>
public interface IEmailSender
{
    /// <summary>Nombre del transporte activo, para logs y diagnóstico (p. ej. "graph", "smtp").</summary>
    string Nombre { get; }

    /// <summary>Envía un correo HTML. Nunca lanza: los fallos vuelven en el resultado.</summary>
    Task<EnvioCorreoResultado> EnviarAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default);
}
