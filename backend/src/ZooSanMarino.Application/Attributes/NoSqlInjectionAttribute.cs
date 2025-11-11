// src/ZooSanMarino.Application/Attributes/NoSqlInjectionAttribute.cs
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace ZooSanMarino.Application.Attributes;

/// <summary>
/// Atributo de validación personalizado para prevenir inyección SQL
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public class NoSqlInjectionAttribute : ValidationAttribute
{
    // Patrones de inyección SQL comunes
    // NOTA: No incluir @, ., -, _ en los patrones básicos ya que son válidos en emails
    private static readonly Regex[] SqlInjectionPatterns = new[]
    {
        new Regex(@"(\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|SCRIPT)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(--|;|\*|'|""|`|%|\||&|!|#|\$|\\|\/|\?|>|<|=)", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Removido @ de este patrón
        new Regex(@"(OR|AND)\s+\d+\s*=\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(OR|AND)\s+['""]?\w+['""]?\s*=\s*['""]?\w+['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(UNION\s+(ALL\s+)?SELECT)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(INSERT\s+INTO)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(UPDATE\s+\w+\s+SET)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(DELETE\s+FROM)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(DROP\s+(TABLE|DATABASE|INDEX|VIEW))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(CREATE\s+(TABLE|DATABASE|INDEX|VIEW|PROCEDURE|FUNCTION))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(ALTER\s+(TABLE|DATABASE|INDEX|VIEW))", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(EXEC(UTE)?\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(xp_|sp_)", RegexOptions.IgnoreCase | RegexOptions.Compiled), // SQL Server stored procedures
        new Regex(@"(script\s*:)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(javascript\s*:)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(on\w+\s*=)", RegexOptions.IgnoreCase | RegexOptions.Compiled), // Event handlers
        new Regex(@"(<script|<\/script>)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(eval\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(expression\s*\()", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(vbscript\s*:)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(iframe|object|embed)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(base64)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        new Regex(@"(data\s*:)", RegexOptions.IgnoreCase | RegexOptions.Compiled)
    };

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var stringValue = value.ToString();
        if (string.IsNullOrWhiteSpace(stringValue))
        {
            return ValidationResult.Success;
        }

        // Verificar si es un email válido (más permisivo para emails)
        bool isEmail = System.Text.RegularExpressions.Regex.IsMatch(stringValue, @"^[^\s@]+@[^\s@]+\.[^\s@]+$");

        // Para emails, solo buscar comandos SQL reales, no caracteres comunes
        if (isEmail)
        {
            // Solo comandos SQL peligrosos como palabras completas, no caracteres comunes como @, ., -, _
            var emailSafePatterns = new[]
            {
                // Comandos SQL peligrosos (deben estar como palabras completas)
                new Regex(@"\b(SELECT|INSERT|UPDATE|DELETE|DROP|CREATE|ALTER|EXEC|EXECUTE|UNION|SCRIPT)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // Comentarios SQL (dos guiones seguidos)
                new Regex(@"--", RegexOptions.Compiled),
                // Punto y coma seguido de espacio o al final (puede ser peligroso en SQL)
                new Regex(@";\s", RegexOptions.Compiled),
                new Regex(@";$", RegexOptions.Compiled),
                // Comillas seguidas de operadores SQL
                new Regex(@"['""]\s*(OR|AND|UNION)\s*['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // Operadores SQL con condiciones sospechosas
                new Regex(@"\b(OR|AND)\s+\d+\s*=\s*\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"\b(OR|AND)\s+['""]?\w+['""]?\s*=\s*['""]?\w+['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // UNION SELECT
                new Regex(@"\bUNION\s+(ALL\s+)?SELECT\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // INSERT INTO
                new Regex(@"\bINSERT\s+INTO\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // UPDATE SET
                new Regex(@"\bUPDATE\s+\w+\s+SET\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // DELETE FROM
                new Regex(@"\bDELETE\s+FROM\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // DROP statements
                new Regex(@"\bDROP\s+(TABLE|DATABASE|INDEX|VIEW)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // CREATE statements
                new Regex(@"\bCREATE\s+(TABLE|DATABASE|INDEX|VIEW|PROCEDURE|FUNCTION)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // ALTER statements
                new Regex(@"\bALTER\s+(TABLE|DATABASE|INDEX|VIEW)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // EXEC statements
                new Regex(@"\bEXEC(UTE)?\s*\(", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                // SQL Server stored procedures
                new Regex(@"\b(xp_|sp_)\w+", RegexOptions.IgnoreCase | RegexOptions.Compiled)
            };

            foreach (var pattern in emailSafePatterns)
            {
                if (pattern.IsMatch(stringValue))
                {
                    return new ValidationResult(
                        $"El campo '{validationContext.DisplayName}' contiene caracteres o patrones no permitidos por seguridad.",
                        new[] { validationContext.MemberName! }
                    );
                }
            }

            // Combinaciones sospechosas en emails
            var emailSuspicious = new[]
            {
                new Regex(@"'.*OR.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@""".*OR.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"'.*AND.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@""".*AND.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"'.*UNION.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@""".*UNION.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
                new Regex(@"'\s*=\s*'", RegexOptions.Compiled),
                new Regex(@"""\s*=\s*""", RegexOptions.Compiled)
            };

            foreach (var pattern in emailSuspicious)
            {
                if (pattern.IsMatch(stringValue))
                {
                    return new ValidationResult(
                        $"El campo '{validationContext.DisplayName}' contiene patrones sospechosos.",
                        new[] { validationContext.MemberName! }
                    );
                }
            }

            return ValidationResult.Success;
        }

        // Para otros campos, validación completa
        // Verificar cada patrón de inyección SQL
        foreach (var pattern in SqlInjectionPatterns)
        {
            if (pattern.IsMatch(stringValue))
            {
                return new ValidationResult(
                    $"El campo '{validationContext.DisplayName}' contiene caracteres o patrones no permitidos por seguridad.",
                    new[] { validationContext.MemberName! }
                );
            }
        }

        // Verificar combinaciones sospechosas adicionales
        var suspiciousPatterns = new[]
        {
            new Regex(@"'.*OR.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@""".*OR.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"'.*AND.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@""".*AND.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"'.*UNION.*'", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@""".*UNION.*""", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"'\s*=\s*'", RegexOptions.Compiled),
            new Regex(@"""\s*=\s*""", RegexOptions.Compiled)
        };

        foreach (var pattern in suspiciousPatterns)
        {
            if (pattern.IsMatch(stringValue))
            {
                return new ValidationResult(
                    $"El campo '{validationContext.DisplayName}' contiene patrones sospechosos.",
                    new[] { validationContext.MemberName! }
                );
            }
        }

        return ValidationResult.Success;
    }
}

