using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Configuration
{
    /// <summary>
    /// Configuración híbrida que combina variables de entorno, base de datos y archivos
    /// </summary>
    public static class SecureConfigurationExtensions
    {
        public static IServiceCollection AddSecureConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            // Registrar el servicio de configuración
            services.AddScoped<IConfigurationService, ConfigurationService>();
            
            // Configurar EmailSettings desde múltiples fuentes
            services.Configure<EmailSettings>(options =>
            {
                // Prioridad: Variables de entorno > Base de datos > Archivo de configuración
                options.SmtpHost = GetValue(configuration, "SMTP_HOST", "EmailSettings:SmtpHost", "smtp.gmail.com");
                options.SmtpPort = int.Parse(GetValue(configuration, "SMTP_PORT", "EmailSettings:SmtpPort", "587"));
                options.SmtpUsername = GetValue(configuration, "SMTP_USERNAME", "EmailSettings:SmtpUsername", "");
                options.SmtpPassword = GetValue(configuration, "SMTP_PASSWORD", "EmailSettings:SmtpPassword", "");
                options.FromEmail = GetValue(configuration, "FROM_EMAIL", "EmailSettings:FromEmail", "");
                options.FromName = GetValue(configuration, "FROM_NAME", "EmailSettings:FromName", "Zoo San Marino");
            });

            return services;
        }

        private static string GetValue(IConfiguration configuration, string envKey, string configKey, string defaultValue)
        {
            // 1. Variable de entorno (más alta prioridad)
            var envValue = Environment.GetEnvironmentVariable(envKey);
            if (!string.IsNullOrEmpty(envValue))
                return envValue;

            // 2. Archivo de configuración
            var configValue = configuration[configKey];
            if (!string.IsNullOrEmpty(configValue))
                return configValue;

            // 3. Valor por defecto
            return defaultValue;
        }
    }

    public class EmailSettings
    {
        public string SmtpHost { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 587;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }
}
