// src/ZooSanMarino.Infrastructure/Services/Email/GraphTokenProvider.cs
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Obtiene y cachea el token de aplicación de Entra ID (flujo <c>client_credentials</c>) para
/// hablar con Microsoft Graph. Sin dependencias de SDK: es un POST form-urlencoded.
///
/// El token dura ~1 h y se renueva con 5 min de margen (<see cref="EnvioCorreoCalculos.MargenRenovacionToken"/>).
/// El <see cref="SemaphoreSlim"/> evita que dos correos del mismo ciclo pidan token en paralelo.
/// </summary>
public class GraphTokenProvider
{
    public const string HttpClientName = "graph-email";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<GraphTokenProvider> _logger;

    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;

    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _tokenCacheado;
    private DateTimeOffset _expiraUtc = DateTimeOffset.MinValue;

    /// <summary>Token listo para usar, o el diagnóstico de por qué no se pudo obtener.</summary>
    public sealed record TokenResultado(string? Token, string? TipoError, string? Detalle)
    {
        public bool Exitoso => !string.IsNullOrEmpty(Token);
    }

    public GraphTokenProvider(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GraphTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _tenantId = configuration["Email:Graph:TenantId"] ?? string.Empty;
        _clientId = configuration["Email:Graph:ClientId"] ?? string.Empty;
        _clientSecret = configuration["Email:Graph:ClientSecret"] ?? string.Empty;
    }

    /// <summary>Descarta el token cacheado para forzar su renovación (p. ej. tras un 401 de Graph).</summary>
    public void Invalidar()
    {
        _gate.Wait();
        try
        {
            _tokenCacheado = null;
            _expiraUtc = DateTimeOffset.MinValue;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<TokenResultado> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        if (_tokenCacheado is not null && EnvioCorreoCalculos.TokenVigente(_expiraUtc, DateTimeOffset.UtcNow))
            return new TokenResultado(_tokenCacheado, null, null);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Otro hilo pudo renovarlo mientras esperábamos el semáforo.
            if (_tokenCacheado is not null && EnvioCorreoCalculos.TokenVigente(_expiraUtc, DateTimeOffset.UtcNow))
                return new TokenResultado(_tokenCacheado, null, null);

            return await SolicitarTokenAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<TokenResultado> SolicitarTokenAsync(CancellationToken cancellationToken)
    {
        var url = $"https://login.microsoftonline.com/{Uri.EscapeDataString(_tenantId)}/oauth2/v2.0/token";

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);

            using var contenido = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            });

            using var respuesta = await client.PostAsync(url, contenido, cancellationToken);
            var cuerpo = await respuesta.Content.ReadAsStringAsync(cancellationToken);

            if (!respuesta.IsSuccessStatusCode)
            {
                var (codigo, descripcion) = LeerError(cuerpo);
                var detalle = EnvioCorreoCalculos.DiagnosticoTokenGraph(
                    (int)respuesta.StatusCode, codigo, descripcion, _tenantId, _clientId);

                _logger.LogError("🔴 No se pudo obtener el token de Entra ID: {Detalle}", detalle);
                return new TokenResultado(null, "graph_token", detalle);
            }

            using var doc = JsonDocument.Parse(cuerpo);
            var raiz = doc.RootElement;

            if (!raiz.TryGetProperty("access_token", out var tokenProp) ||
                tokenProp.GetString() is not { Length: > 0 } token)
            {
                var detalle = EnvioCorreoCalculos.DiagnosticoTokenGraph(
                    (int)respuesta.StatusCode, "respuesta_sin_token",
                    "Entra ID respondió 200 pero sin access_token.", _tenantId, _clientId);

                _logger.LogError("🔴 {Detalle}", detalle);
                return new TokenResultado(null, "graph_token", detalle);
            }

            var expiresIn = raiz.TryGetProperty("expires_in", out var expProp) && expProp.TryGetInt32(out var segundos)
                ? segundos
                : 3600;

            _tokenCacheado = token;
            _expiraUtc = EnvioCorreoCalculos.CalcularVencimientoToken(expiresIn, DateTimeOffset.UtcNow);

            _logger.LogInformation(
                "🔑 Token de Microsoft Graph obtenido (vence {Vence:yyyy-MM-dd HH:mm:ss} UTC)", _expiraUtc.UtcDateTime);

            return new TokenResultado(token, null, null);
        }
        catch (Exception ex)
        {
            var detalle = EnvioCorreoCalculos.DiagnosticoTokenGraph(
                0, ex.GetType().Name, ex.Message, _tenantId, _clientId);

            _logger.LogError(ex, "Error de red obteniendo el token de Entra ID: {Message}", ex.Message);
            return new TokenResultado(null, "graph_token_red", detalle);
        }
    }

    /// <summary>Extrae <c>error</c> / <c>error_description</c> del cuerpo de Entra ID sin reventar si no es JSON.</summary>
    private static (string? Codigo, string? Descripcion) LeerError(string cuerpo)
    {
        if (string.IsNullOrWhiteSpace(cuerpo))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(cuerpo);
            var raiz = doc.RootElement;

            var codigo = raiz.TryGetProperty("error", out var e) ? e.GetString() : null;
            var descripcion = raiz.TryGetProperty("error_description", out var d) ? d.GetString() : null;

            return (codigo, descripcion);
        }
        catch (JsonException)
        {
            return (null, cuerpo.Length > 500 ? cuerpo[..500] : cuerpo);
        }
    }
}
