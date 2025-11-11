// src/ZooSanMarino.API/Services/InputSanitizerService.cs
using System.Text.RegularExpressions;

namespace ZooSanMarino.API.Services;

/// <summary>
/// Servicio para sanitizar y validar entradas del usuario para prevenir inyección SQL
/// </summary>
public class InputSanitizerService
{
    private static readonly Regex[] DangerousPatterns = new[]
    {
        new Regex(@"<script\b[^<]*(?:(?!<\/script>)<[^<]*)*<\/script>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"<[^>]+>", RegexOptions.Compiled), // HTML tags
        new Regex(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Event handlers
        new Regex(@"eval\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"expression\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"vbscript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"data\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    // Solo los caracteres más peligrosos que deben ser eliminados
    // NO incluir @, ., -, _, + ya que son válidos en emails y contraseñas
    private static readonly char[] DangerousChars = { '<', '>', '\'', '"', ';', '\\', '/', '&', '|', '`' };

    /// <summary>
    /// Sanitiza un string eliminando caracteres y patrones peligrosos
    /// </summary>
    public string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        string sanitized = input;

        // Eliminar patrones peligrosos (scripts, XSS)
        foreach (var pattern in DangerousPatterns)
        {
            sanitized = pattern.Replace(sanitized, string.Empty);
        }

        // Para emails, NO eliminar caracteres válidos - solo eliminar patrones peligrosos ya eliminados arriba
        if (IsEmail(input))
        {
            // Los emails ya están limpios de scripts, solo hacer trim
            sanitized = sanitized.Trim();
        }
        else
        {
            // Para otros campos (como contraseñas), eliminar solo caracteres muy peligrosos
            // NO eliminar @, ., -, _, + ya que pueden ser válidos en contraseñas
            foreach (var dangerousChar in DangerousChars)
            {
                sanitized = sanitized.Replace(dangerousChar.ToString(), string.Empty);
            }
            sanitized = sanitized.Trim();
        }

        return sanitized;
    }

    /// <summary>
    /// Valida si un string parece ser un email válido
    /// </summary>
    private static bool IsEmail(string input)
    {
        return Regex.IsMatch(input, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");
    }

    /// <summary>
    /// Sanitiza un objeto completo recursivamente
    /// </summary>
    public T SanitizeObject<T>(T obj)
    {
        if (obj == null)
        {
            return obj;
        }

        // Campos que NO deben ser sanitizados (contraseñas pueden contener cualquier carácter)
        var excludedFields = new[] { "password", "Password", "currentPassword", "newPassword", "confirmPassword", "oldPassword" };

        var objType = typeof(T);
        var sanitized = Activator.CreateInstance<T>();

        foreach (var property in objType.GetProperties())
        {
            // No sanitizar campos de contraseña (mantener tal cual)
            if (excludedFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
            {
                property.SetValue(sanitized, property.GetValue(obj));
                continue;
            }

            if (property.PropertyType == typeof(string))
            {
                var value = property.GetValue(obj) as string;
                property.SetValue(sanitized, Sanitize(value));
            }
            else if (property.PropertyType.IsClass && property.PropertyType != typeof(string))
            {
                var nestedValue = property.GetValue(obj);
                if (nestedValue != null)
                {
                    var sanitizeMethod = typeof(InputSanitizerService).GetMethod(nameof(SanitizeObject))!
                        .MakeGenericMethod(property.PropertyType);
                    var sanitizedNested = sanitizeMethod.Invoke(this, new[] { nestedValue });
                    property.SetValue(sanitized, sanitizedNested);
                }
            }
            else
            {
                property.SetValue(sanitized, property.GetValue(obj));
            }
        }

        return sanitized;
    }
}

