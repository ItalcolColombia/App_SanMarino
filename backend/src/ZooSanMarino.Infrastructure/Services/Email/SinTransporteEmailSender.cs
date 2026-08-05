// src/ZooSanMarino.Infrastructure/Services/Email/SinTransporteEmailSender.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Transporte nulo: se resuelve cuando no hay configuración de correo utilizable. Cada intento
/// falla con un diagnóstico accionable y el correo vuelve a la cola.
///
/// Existe para que una configuración incompleta NO tumbe el arranque de la aplicación (antes el
/// constructor del procesador de cola lanzaba y, al ser un <c>HostedService</c>, se llevaba puesto
/// el arranque en ECS). El síntoma pasa a ser visible y recuperable: filas en <c>email_queue</c>
/// con el motivo exacto.
/// </summary>
public class SinTransporteEmailSender : IEmailSender
{
    private readonly string _detalle;

    public string Nombre => "no-configurado";

    public SinTransporteEmailSender(string? providerSolicitado)
    {
        _detalle = EnvioCorreoCalculos.DiagnosticoSinProveedor(providerSolicitado);
    }

    public Task<EnvioCorreoResultado> EnviarAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        Task.FromResult(EnvioCorreoResultado.Error("sin_transporte", _detalle));
}
