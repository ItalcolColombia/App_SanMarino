// src/ZooSanMarino.Application/Interfaces/IRecaptchaService.cs
namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Servicio para validar tokens de reCAPTCHA de Google
/// </summary>
public interface IRecaptchaService
{
    /// <summary>
    /// Valida un token de reCAPTCHA con Google
    /// </summary>
    /// <param name="recaptchaToken">Token de reCAPTCHA enviado por el frontend</param>
    /// <param name="clientIp">IP del cliente (opcional, para validación adicional)</param>
    /// <returns>True si el token es válido, false en caso contrario</returns>
    Task<bool> ValidateRecaptchaAsync(string recaptchaToken, string? clientIp = null);
}



