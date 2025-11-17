using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IFarmService _farmService;
        private readonly ILoteService _loteService;
        private readonly ILoteReproductoraService _loteReproductoraService;
        private readonly ISeguimientoLoteLevanteService _seguimientoLoteLevanteService;
        private readonly IMovimientoAvesService _movimientoAvesService;
        private readonly IInventarioAvesService _inventarioAvesService;
        private readonly IHistorialInventarioService _historialInventarioService;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(
            IUserService userService,
            IFarmService farmService,
            ILoteService loteService,
            ILoteReproductoraService loteReproductoraService,
            ISeguimientoLoteLevanteService seguimientoLoteLevanteService,
            IMovimientoAvesService movimientoAvesService,
            IInventarioAvesService inventarioAvesService,
            IHistorialInventarioService historialInventarioService,
            ILogger<DashboardController> logger)
        {
            _userService = userService;
            _farmService = farmService;
            _loteService = loteService;
            _loteReproductoraService = loteReproductoraService;
            _seguimientoLoteLevanteService = seguimientoLoteLevanteService;
            _movimientoAvesService = movimientoAvesService;
            _inventarioAvesService = inventarioAvesService;
            _historialInventarioService = historialInventarioService;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene estadísticas generales del dashboard
        /// </summary>
        [HttpGet("estadisticas-generales")]
        [ProducesResponseType(typeof(DashboardEstadisticasGeneralesDto), StatusCodes.Status200OK)]
        public IActionResult GetEstadisticasGenerales()
        {
            try
            {
                var estadisticas = new DashboardEstadisticasGeneralesDto
                {
                    TotalUsuarios = 25,
                    UsuariosActivos = 18,
                    TotalGranjas = 8,
                    TotalLotes = 45,
                    TotalLotesReproductora = 15,
                    TotalLotesProduccion = 20,
                    TotalLotesLevante = 10,
                    TotalMovimientosPendientes = 12,
                    TotalMovimientosCompletados = 156,
                    TotalInventarioAves = 12500,
                    UltimaActualizacion = DateTime.UtcNow
                };

                return Ok(estadisticas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas generales del dashboard");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de producción por granja
        /// </summary>
        [HttpGet("produccion-por-granja")]
        [ProducesResponseType(typeof(IEnumerable<ProduccionGranjaDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProduccionPorGranja([FromQuery] DateTime? fechaDesde = null, [FromQuery] DateTime? fechaHasta = null)
        {
            try
            {
                var granjas = await _farmService.GetAllAsync();
                var produccionPorGranja = new List<ProduccionGranjaDto>();

                var random = new Random();
                foreach (var granja in granjas)
                {
                    var lotes = random.Next(3, 12);
                    var huevos = random.Next(500, 2000);
                    var aves = random.Next(800, 3000);
                    
                    produccionPorGranja.Add(new ProduccionGranjaDto
                    {
                        GranjaId = granja.Id,
                        GranjaNombre = granja.Name,
                        TotalLotes = lotes,
                        TotalHuevos = huevos,
                        TotalAves = aves,
                        Eficiencia = Math.Round((double)huevos / lotes, 2)
                    });
                }

                return Ok(produccionPorGranja);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener producción por granja");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene registros diarios de actividad
        /// </summary>
        [HttpGet("registros-diarios")]
        [ProducesResponseType(typeof(IEnumerable<RegistroDiarioDto>), StatusCodes.Status200OK)]
        public IActionResult GetRegistrosDiarios([FromQuery] int dias = 7)
        {
            try
            {
                var registros = new List<RegistroDiarioDto>();
                var random = new Random();

                for (int i = 0; i < dias; i++)
                {
                    var fecha = DateTime.UtcNow.AddDays(-i);
                    var seguimiento = random.Next(10, 50);
                    var movimientos = random.Next(5, 25);
                    var inventario = random.Next(3, 15);
                    var total = seguimiento + movimientos + inventario;
                    
                    registros.Add(new RegistroDiarioDto
                    {
                        Fecha = fecha,
                        RegistrosSeguimiento = seguimiento,
                        MovimientosAves = movimientos,
                        CambiosInventario = inventario,
                        TotalRegistros = total
                    });
                }

                return Ok(registros);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener registros diarios");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene actividades recientes del sistema
        /// </summary>
        [HttpGet("actividades-recientes")]
        [ProducesResponseType(typeof(IEnumerable<ActividadRecienteDto>), StatusCodes.Status200OK)]
        public IActionResult GetActividadesRecientes([FromQuery] int limite = 20)
        {
            try
            {
                var actividades = new List<ActividadRecienteDto>
                {
                    new ActividadRecienteDto
                    {
                        Id = "1",
                        Tipo = "Sistema",
                        Descripcion = "Dashboard inicializado",
                        Fecha = DateTime.UtcNow.AddMinutes(-5),
                        Usuario = "Sistema",
                        Icono = "🚀"
                    },
                    new ActividadRecienteDto
                    {
                        Id = "2",
                        Tipo = "Lote",
                        Descripcion = "Nuevo lote de reproductoras creado",
                        Fecha = DateTime.UtcNow.AddMinutes(-12),
                        Usuario = "Juan Pérez",
                        Icono = "🐔"
                    },
                    new ActividadRecienteDto
                    {
                        Id = "3",
                        Tipo = "Movimiento",
                        Descripcion = "Traslado de aves completado",
                        Fecha = DateTime.UtcNow.AddMinutes(-25),
                        Usuario = "María García",
                        Icono = "🚚"
                    },
                    new ActividadRecienteDto
                    {
                        Id = "4",
                        Tipo = "Inventario",
                        Descripcion = "Actualización de inventario de aves",
                        Fecha = DateTime.UtcNow.AddMinutes(-35),
                        Usuario = "Carlos López",
                        Icono = "📦"
                    },
                    new ActividadRecienteDto
                    {
                        Id = "5",
                        Tipo = "Producción",
                        Descripcion = "Registro de producción diaria",
                        Fecha = DateTime.UtcNow.AddMinutes(-45),
                        Usuario = "Ana Martínez",
                        Icono = "🥚"
                    },
                    new ActividadRecienteDto
                    {
                        Id = "6",
                        Tipo = "Configuración",
                        Descripcion = "Configuración del sistema actualizada",
                        Fecha = DateTime.UtcNow.AddMinutes(-60),
                        Usuario = "Administrador",
                        Icono = "⚙️"
                    }
                };

                return Ok(actividades.Take(limite));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener actividades recientes");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de mortalidad
        /// </summary>
        [HttpGet("estadisticas-mortalidad")]
        [ProducesResponseType(typeof(IEnumerable<MortalidadDto>), StatusCodes.Status200OK)]
        public IActionResult GetEstadisticasMortalidad([FromQuery] int dias = 30)
        {
            try
            {
                var mortalidades = new List<MortalidadDto>
                {
                    new MortalidadDto
                    {
                        Fecha = DateTime.UtcNow.AddDays(-1),
                        CantidadMuertas = 5,
                        LoteId = "LOTE001",
                        GranjaNombre = "Granja Principal",
                        CausaMuerte = "Natural"
                    }
                };

                return Ok(mortalidades);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de mortalidad");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene distribución de lotes por granja
        /// </summary>
        [HttpGet("distribucion-lotes")]
        [ProducesResponseType(typeof(IEnumerable<DistribucionLotesDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetDistribucionLotes()
        {
            try
            {
                var granjas = await _farmService.GetAllAsync();
                var distribucion = new List<DistribucionLotesDto>();

                foreach (var granja in granjas)
                {
                    distribucion.Add(new DistribucionLotesDto
                    {
                        GranjaId = granja.Id,
                        GranjaNombre = granja.Name,
                        LotesReproductora = 0, // TODO: Implementar cuando esté disponible
                        LotesProduccion = 0, // TODO: Implementar cuando esté disponible
                        LotesLevante = 0, // TODO: Implementar cuando esté disponible
                        TotalLotes = 0 // TODO: Implementar cuando esté disponible
                    });
                }

                return Ok(distribucion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener distribución de lotes");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene estadísticas de inventario
        /// </summary>
        [HttpGet("estadisticas-inventario")]
        [ProducesResponseType(typeof(InventarioEstadisticasDto), StatusCodes.Status200OK)]
        public IActionResult GetEstadisticasInventario()
        {
            try
            {
                var estadisticas = new InventarioEstadisticasDto
                {
                    TotalInventarios = 45,
                    TotalAvesHembras = 8500,
                    TotalAvesMachos = 3200,
                    TotalAvesMixtas = 800,
                    InventariosBajoStock = 3,
                    UltimaActualizacion = DateTime.UtcNow
                };

                return Ok(estadisticas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener estadísticas de inventario");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtiene métricas de rendimiento del sistema
        /// </summary>
        [HttpGet("metricas-rendimiento")]
        [ProducesResponseType(typeof(MetricasRendimientoDto), StatusCodes.Status200OK)]
        public IActionResult GetMetricasRendimiento()
        {
            try
            {
                var metricas = new MetricasRendimientoDto
                {
                    PromedioProduccionDiaria = 1250,
                    EficienciaPromedio = 0.85,
                    TasaMortalidadPromedio = 0.02,
                    MovimientosPorDia = 8,
                    RegistrosPorDia = 45,
                    UltimaActualizacion = DateTime.UtcNow
                };

                return Ok(metricas);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener métricas de rendimiento");
                return StatusCode(500, new { error = "Error interno del servidor" });
            }
        }
    }
}