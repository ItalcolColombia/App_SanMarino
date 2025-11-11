// src/ZooSanMarino.Infrastructure/Services/RecaptchaService.cs
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para validar tokens de reCAPTCHA con Google
/// </summary>
public class RecaptchaService : IRecaptchaService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RecaptchaService> _logger;
    private readonly string? _secretKey;
    private readonly bool _isEnabled;

    public RecaptchaService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RecaptchaService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        
        _secretKey = _configuration["Recaptcha:SecretKey"];
        var enabledValue = _configuration["Recaptcha:Enabled"];
        _isEnabled = !string.IsNullOrWhiteSpace(enabledValue) && bool.TryParse(enabledValue, out var enabled) && enabled;
        
        // En desarrollo local, deshabilitar reCAPTCHA si no hay clave configurada
        var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
        if (isDevelopment && string.IsNullOrWhiteSpace(_secretKey))
        {
            _isEnabled = false;
            _logger.LogInformation("reCAPTCHA deshabilitado en desarrollo local (no hay clave configurada)");
        }
    }

    public async Task<bool> ValidateRecaptchaAsync(string recaptchaToken, string? clientIp = null)
    {
        // Si está deshabilitado o no hay clave, permitir en desarrollo
        if (!_isEnabled || string.IsNullOrWhiteSpace(_secretKey))
        {
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            if (isDevelopment)
            {
                _logger.LogDebug("reCAPTCHA omitido en desarrollo local");
                return true; // Permitir en desarrollo
            }
            
            // En producción, si está deshabilitado pero no hay token, rechazar
            if (string.IsNullOrWhiteSpace(recaptchaToken))
            {
                _logger.LogWarning("reCAPTCHA está deshabilitado pero no se recibió token en producción");
                return false;
            }
            
            return true;
        }

        // Validar que el token no esté vacío
        if (string.IsNullOrWhiteSpace(recaptchaToken))
        {
            _logger.LogWarning("Token de reCAPTCHA vacío o inválido");
            return false;
        }

        try
        {
            // Preparar la petición a Google reCAPTCHA API
            var requestBody = new Dictionary<string, string>
            {
                { "secret", _secretKey },
                { "response", recaptchaToken }
            };

            // Agregar IP del cliente si está disponible (para reCAPTCHA v3 es recomendado)
            if (!string.IsNullOrWhiteSpace(clientIp))
            {
                requestBody.Add("remoteip", clientIp);
            }

            var content = new FormUrlEncodedContent(requestBody);
            var response = await _httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", content);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Error al validar reCAPTCHA: {StatusCode}", response.StatusCode);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<RecaptchaResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (result == null)
            {
                _logger.LogError("No se pudo deserializar la respuesta de reCAPTCHA");
                return false;
            }

            // Validar el resultado
            if (!result.Success)
            {
                _logger.LogWarning(
                    "reCAPTCHA falló para IP {ClientIp}. Errores: {Errors}",
                    clientIp ?? "unknown",
                    string.Join(", ", result.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }

            // Para reCAPTCHA v3, también validar el score (si está disponible)
            // Score recomendado: >= 0.5 para acciones como login
            if (result.Score.HasValue && result.Score.Value < 0.5m)
            {
                _logger.LogWarning(
                    "reCAPTCHA score muy bajo: {Score} para IP {ClientIp}",
                    result.Score.Value,
                    clientIp ?? "unknown");
                return false;
            }

            _logger.LogDebug("reCAPTCHA validado exitosamente para IP {ClientIp}", clientIp ?? "unknown");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al validar reCAPTCHA");
            // En caso de error, en producción rechazar, en desarrollo permitir
            var isDevelopment = _configuration["ASPNETCORE_ENVIRONMENT"] == "Development";
            return isDevelopment;
        }
    }

    private class RecaptchaResponse
    {
        public bool Success { get; set; }
        public decimal? Score { get; set; } // Para reCAPTCHA v3
        public string? Action { get; set; } // Para reCAPTCHA v3
        public string? ChallengeTs { get; set; }
        public string? Hostname { get; set; }
        public string[]? ErrorCodes { get; set; }
    }
}

