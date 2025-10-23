using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Infrastructure.Services;

namespace ZooSanMarino.API.Controllers
{
    /// <summary>
    /// Controlador para administrar configuraciones del sistema de forma segura
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")] // Solo administradores pueden modificar configuraciones
    public class ConfigurationController : ControllerBase
    {
        private readonly IConfigurationService _configService;
        private readonly ILogger<ConfigurationController> _logger;

        public ConfigurationController(
            IConfigurationService configService,
            ILogger<ConfigurationController> logger)
        {
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene todas las configuraciones del sistema
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAllConfigurations()
        {
            try
            {
                var configs = await _configService.GetAllConfigurationsAsync();
                return Ok(configs);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuraciones");
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Obtiene una configuración específica por clave
        /// </summary>
        [HttpGet("{key}")]
        public async Task<IActionResult> GetConfiguration(string key)
        {
            try
            {
                var value = await _configService.GetValueAsync(key);
                if (value == null)
                    return NotFound($"Configuración '{key}' no encontrada");

                return Ok(new { Key = key, Value = value });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener configuración {Key}", key);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Actualiza una configuración específica
        /// </summary>
        [HttpPut("{key}")]
        public async Task<IActionResult> UpdateConfiguration(string key, [FromBody] UpdateConfigurationRequest request)
        {
            try
            {
                if (string.IsNullOrEmpty(request.Value))
                    return BadRequest("El valor no puede estar vacío");

                await _configService.SetValueAsync(key, request.Value, request.Encrypt);
                
                _logger.LogInformation("Configuración {Key} actualizada por usuario {UserId}", key, User.Identity?.Name);
                
                return Ok(new { Message = "Configuración actualizada correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al actualizar configuración {Key}", key);
                return StatusCode(500, "Error interno del servidor");
            }
        }

        /// <summary>
        /// Inicializa las configuraciones por defecto
        /// </summary>
        [HttpPost("initialize")]
        public async Task<IActionResult> InitializeDefaultConfigurations()
        {
            try
            {
                await _configService.InitializeDefaultConfigurationsAsync();
                
                _logger.LogInformation("Configuraciones por defecto inicializadas por usuario {UserId}", User.Identity?.Name);
                
                return Ok(new { Message = "Configuraciones por defecto inicializadas correctamente" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al inicializar configuraciones por defecto");
                return StatusCode(500, "Error interno del servidor");
            }
        }
    }

    public class UpdateConfigurationRequest
    {
        public string Value { get; set; } = string.Empty;
        public bool Encrypt { get; set; } = false;
    }
}
