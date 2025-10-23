using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services
{
    /// <summary>
    /// Servicio para manejar configuraciones del sistema desde la base de datos
    /// </summary>
    public interface IConfigurationService
    {
        Task<string?> GetValueAsync(string key);
        Task SetValueAsync(string key, string value, bool encrypt = false);
        Task<Dictionary<string, string>> GetAllConfigurationsAsync();
        Task InitializeDefaultConfigurationsAsync();
    }

    public class ConfigurationService : IConfigurationService
    {
        private readonly ZooSanMarinoContext _context;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<string, string> _cache = new();

        public ConfigurationService(ZooSanMarinoContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string?> GetValueAsync(string key)
        {
            // Primero verificar cache
            if (_cache.ContainsKey(key))
                return _cache[key];

            // Buscar en base de datos
            var config = await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Key == key);

            if (config != null)
            {
                var value = config.IsEncrypted ? DecryptValue(config.Value) : config.Value;
                _cache[key] = value;
                return value;
            }

            // Fallback a variables de entorno
            return _configuration[key];
        }

        public async Task SetValueAsync(string key, string value, bool encrypt = false)
        {
            var encryptedValue = encrypt ? EncryptValue(value) : value;
            
            var config = await _context.SystemConfigurations
                .FirstOrDefaultAsync(c => c.Key == key);

            if (config != null)
            {
                config.Value = encryptedValue;
                config.IsEncrypted = encrypt;
                config.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.SystemConfigurations.Add(new SystemConfiguration
                {
                    Key = key,
                    Value = encryptedValue,
                    IsEncrypted = encrypt,
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            
            // Actualizar cache
            _cache[key] = value;
        }

        public async Task<Dictionary<string, string>> GetAllConfigurationsAsync()
        {
            var configs = await _context.SystemConfigurations.ToListAsync();
            var result = new Dictionary<string, string>();

            foreach (var config in configs)
            {
                var value = config.IsEncrypted ? DecryptValue(config.Value) : config.Value;
                result[config.Key] = value;
            }

            return result;
        }

        public async Task InitializeDefaultConfigurationsAsync()
        {
            var defaultConfigs = new Dictionary<string, (string value, bool encrypt)>
            {
                ["SMTP_HOST"] = ("smtp.gmail.com", false),
                ["SMTP_PORT"] = ("587", false),
                ["SMTP_USERNAME"] = ("", true),
                ["SMTP_PASSWORD"] = ("", true),
                ["FROM_EMAIL"] = ("", true),
                ["FROM_NAME"] = ("Zoo San Marino", false),
                ["JWT_SECRET_KEY"] = ("", true),
                ["DB_CONNECTION_STRING"] = ("", true)
            };

            foreach (var (key, (value, encrypt)) in defaultConfigs)
            {
                var exists = await _context.SystemConfigurations
                    .AnyAsync(c => c.Key == key);

                if (!exists)
                {
                    await SetValueAsync(key, value, encrypt);
                }
            }
        }

        private string EncryptValue(string value)
        {
            // Implementar encriptación real aquí
            // Por ahora, solo base64 (NO es seguro para producción)
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(value));
        }

        private string DecryptValue(string encryptedValue)
        {
            // Implementar desencriptación real aquí
            // Por ahora, solo base64 (NO es seguro para producción)
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
        }
    }
}
