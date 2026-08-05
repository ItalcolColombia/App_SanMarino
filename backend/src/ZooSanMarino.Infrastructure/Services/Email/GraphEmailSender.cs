// src/ZooSanMarino.Infrastructure/Services/Email/GraphEmailSender.cs
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Envía correo por Microsoft Graph (<c>POST /v1.0/users/{buzón}/sendMail</c>) autenticando con
/// OAuth 2.0 client credentials. Reemplaza al SMTP con usuario/contraseña, que Exchange Online
/// dejó de aceptar (<c>550 5.7.30 Basic authentication is not supported for Client Submission</c>).
///
/// Sale por HTTPS 443, así que no depende de que el puerto 587 esté abierto desde ECS.
/// </summary>
public class GraphEmailSender : IEmailSender
{
    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GraphTokenProvider _tokenProvider;
    private readonly ILogger<GraphEmailSender> _logger;

    private readonly string _buzonRemitente;
    private readonly string _nombreRemitente;
    private readonly string _clientId;
    private readonly bool _guardarEnEnviados;

    public string Nombre => "graph";

    public GraphEmailSender(
        IHttpClientFactory httpClientFactory,
        GraphTokenProvider tokenProvider,
        IConfiguration configuration,
        ILogger<GraphEmailSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _tokenProvider = tokenProvider;
        _logger = logger;

        // El buzón desde el que se envía; por defecto el mismo remitente que ya usaba el SMTP.
        _buzonRemitente = configuration["Email:Graph:SenderMailbox"]
            ?? configuration["Email:From:Address"]
            ?? string.Empty;
        _nombreRemitente = configuration["Email:From:Name"] ?? "ItalGranja";
        _clientId = configuration["Email:Graph:ClientId"] ?? string.Empty;
        _guardarEnEnviados = bool.TryParse(configuration["Email:Graph:SaveToSentItems"], out var guardar) && guardar;
    }

    public async Task<EnvioCorreoResultado> EnviarAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        try
        {
            var resultado = await IntentarEnviarAsync(toEmail, subject, htmlBody, cancellationToken);

            // Un 401 con token cacheado suele ser un token revocado o rotado a mitad de vuelo:
            // se descarta, se pide uno nuevo y se reintenta UNA vez antes de dar el correo por fallido.
            if (resultado.TipoError == "graph_auth")
            {
                _logger.LogWarning("Token de Graph rechazado (401): se renueva y se reintenta el envío una vez");
                _tokenProvider.Invalidar();
                resultado = await IntentarEnviarAsync(toEmail, subject, htmlBody, cancellationToken);
            }

            return resultado;
        }
        catch (Exception ex)
        {
            var stackTraceLength = ex.StackTrace?.Length ?? 0;
            var stackTracePreview = stackTraceLength > 0
                ? ex.StackTrace?.Substring(0, Math.Min(500, stackTraceLength)) ?? ""
                : "";
            var errorDetails = $"Type: {ex.GetType().Name}, Message: {ex.Message}, StackTrace: {stackTracePreview}";

            _logger.LogError(ex, "Error inesperado enviando por Microsoft Graph a {ToEmail}: {Message}",
                toEmail, ex.Message);

            return EnvioCorreoResultado.Error("graph_excepcion", errorDetails);
        }
    }

    private async Task<EnvioCorreoResultado> IntentarEnviarAsync(
        string toEmail, string subject, string htmlBody, CancellationToken cancellationToken)
    {
        var token = await _tokenProvider.ObtenerAsync(cancellationToken);
        if (!token.Exitoso)
            return EnvioCorreoResultado.Error(token.TipoError ?? "graph_token", token.Detalle ?? "Sin token de Graph.");

        var payload = EnvioCorreoCalculos.ConstruirPayloadSendMail(
            _buzonRemitente, _nombreRemitente, toEmail, subject, htmlBody, _guardarEnEnviados);

        var url = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(_buzonRemitente)}/sendMail";

        var client = _httpClientFactory.CreateClient(GraphTokenProvider.HttpClientName);

        using var solicitud = new HttpRequestMessage(HttpMethod.Post, url);
        solicitud.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        solicitud.Content = new StringContent(
            JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var respuesta = await client.SendAsync(solicitud, cancellationToken);

        // Graph responde 202 Accepted (sin cuerpo) cuando aceptó el mensaje para envío.
        if (respuesta.StatusCode == HttpStatusCode.Accepted || respuesta.IsSuccessStatusCode)
            return EnvioCorreoResultado.Ok();

        var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);
        var (codigo, mensaje) = LeerError(cuerpo);
        var httpStatus = (int)respuesta.StatusCode;

        var detalle = EnvioCorreoCalculos.DiagnosticoGraph(
            httpStatus, codigo, mensaje, _buzonRemitente, _clientId);
        var tipoError = EnvioCorreoCalculos.ClasificarErrorGraph(httpStatus);

        if (EnvioCorreoCalculos.EsErrorTransitorioGraph(httpStatus))
            _logger.LogWarning("⚠️ Microsoft Graph rechazó el envío a {ToEmail} (reintentable): {Detalle}",
                toEmail, detalle);
        else
            _logger.LogError("🔴 Microsoft Graph rechazó el envío a {ToEmail}: {Detalle}", toEmail, detalle);

        return EnvioCorreoResultado.Error(tipoError, detalle);
    }

    /// <summary>Extrae <c>error.code</c> / <c>error.message</c> de Graph sin reventar si no es JSON.</summary>
    private static (string? Codigo, string? Mensaje) LeerError(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(cuerpo);

            if (!doc.RootElement.TryGetProperty("error", out var error))
                return (null, cuerpo.Length > 500 ? cuerpo[..500] : cuerpo);

            var codigo = error.TryGetProperty("code", out var c) ? c.GetString() : null;
            var mensaje = error.TryGetProperty("message", out var m) ? m.GetString() : null;

            return (codigo, mensaje);
        }
        catch (JsonException)
        {
            return (null, cuerpo.Length > 500 ? cuerpo[..500] : cuerpo);
        }
    }
}
