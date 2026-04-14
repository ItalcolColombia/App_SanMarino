// src/ZooSanMarino.API/Controllers/ProduccionController.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using LoteDtos = ZooSanMarino.Application.DTOs.Lotes;
using LiquidacionDto = ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProduccionController : ControllerBase
{
    private readonly IProduccionService _produccionService;
    private readonly ILiquidacionTecnicaProduccionService _liquidacionProduccionService;
    private readonly IIndicadoresProduccionService _indicadoresProduccionService;
    private readonly ILogger<ProduccionController> _logger;
    private readonly IWebHostEnvironment _env;

    public ProduccionController(
        IProduccionService produccionService,
        ILiquidacionTecnicaProduccionService liquidacionProduccionService,
        IIndicadoresProduccionService indicadoresProduccionService,
        ILogger<ProduccionController> logger,
        IWebHostEnvironment env)
    {
        _produccionService = produccionService;
        _liquidacionProduccionService = liquidacionProduccionService;
        _indicadoresProduccionService = indicadoresProduccionService;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Verifica si existe un registro inicial de producción para un lote (tabla unificada lotes).
    /// Considera: lote hijo en fase Producción o mismo lote con campos de producción llenos.
    /// </summary>
    /// <param name="loteId">ID del lote (padre o mismo lote)</param>
    /// <returns>Exists y ProduccionLoteId (LoteId del registro en producción para usar en seguimientos)</returns>
    [HttpGet("lotes/{loteId}/exists")]
    public async Task<ActionResult<ExisteProduccionLoteResponse>> ExisteProduccionLote(int loteId)
    {
        try
        {
            var existe = await _produccionService.ExisteProduccionLoteAsync(loteId);
            var produccionLoteId = existe ? await _produccionService.ObtenerProduccionLoteAsync(loteId) : null;
            
            return Ok(new ExisteProduccionLoteResponse(existe, produccionLoteId?.Id));
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Crea un nuevo registro inicial de producción para un lote (tabla unificada lotes).
    /// Inserta un nuevo registro en lotes con Fase = Produccion, LotePadreId = request.LoteId y campos de producción.
    /// </summary>
    /// <param name="request">Datos del registro inicial</param>
    /// <returns>LoteId del registro creado (lote hijo en fase Producción)</returns>
    [HttpPost("lotes")]
    public async Task<ActionResult<int>> CrearProduccionLote([FromBody] CrearProduccionLoteRequest request)
    {
        try
        {
            var id = await _produccionService.CrearProduccionLoteAsync(request);
            return CreatedAtAction(nameof(ObtenerProduccionLote), new { loteId = request.LoteId }, id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el detalle del registro inicial de producción de un lote
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>Detalle del registro inicial</returns>
    [HttpGet("lotes/{loteId}")]
    public async Task<ActionResult<ProduccionLoteDetalleDto>> ObtenerProduccionLote(int loteId)
    {
        try
        {
            var detalle = await _produccionService.ObtenerProduccionLoteAsync(loteId);
            
            if (detalle == null)
            {
                return NotFound(new { message = "No se encontró un registro inicial de producción para este lote" });
            }

            return Ok(detalle);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Crea un nuevo seguimiento diario de producción
    /// </summary>
    /// <param name="request">Datos del seguimiento diario</param>
    /// <returns>ID del seguimiento creado</returns>
    [HttpPost("seguimiento")]
    public async Task<ActionResult<int>> CrearSeguimiento([FromBody] CrearSeguimientoRequest request)
    {
        try
        {
            var id = await _produccionService.CrearSeguimientoAsync(request);
            var routeValues = request.LotePosturaProduccionId.HasValue
                ? (object)new { lotePosturaProduccionId = request.LotePosturaProduccionId }
                : new { loteId = request.ProduccionLoteId };
            return CreatedAtAction(nameof(ListarSeguimiento), routeValues, id);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            // Log del error completo para debugging
            Console.WriteLine($"ERROR en CrearSeguimiento: {ex.Message}");
            Console.WriteLine($"Stack Trace: {ex.StackTrace}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Exception: {ex.InnerException.Message}");
            }
            return StatusCode(500, new {
                message = $"Error: {ex.Message}",
                details = ex.InnerException?.Message,
                stackTrace = ex.StackTrace
            });
        }
    }

    /// <summary>
    /// Actualiza un seguimiento diario de producción existente.
    /// </summary>
    /// <param name="id">ID del seguimiento a actualizar</param>
    /// <param name="request">Datos actualizados del seguimiento</param>
    [HttpPut("seguimiento/{id}")]
    public async Task<IActionResult> ActualizarSeguimiento(int id, [FromBody] CrearSeguimientoRequest request)
    {
        try
        {
            await _produccionService.ActualizarSeguimientoAsync(id, request);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR en ActualizarSeguimiento: {ex.Message}");
            return StatusCode(500, new { message = ex.Message, details = ex.InnerException?.Message });
        }
    }

    /// <summary>
    /// Lista los seguimientos diarios de producción de un lote.
    /// Requiere loteId o lotePosturaProduccionId.
    /// </summary>
    /// <param name="loteId">ID del lote (legacy)</param>
    /// <param name="lotePosturaProduccionId">ID del lote postura producción (nuevo flujo)</param>
    /// <param name="desde">Fecha desde (opcional)</param>
    /// <param name="hasta">Fecha hasta (opcional)</param>
    /// <param name="page">Número de página (por defecto 1)</param>
    /// <param name="size">Tamaño de página. Use 0 para traer todos (sin paginación).</param>
    /// <returns>Lista paginada de seguimientos (cada ítem incluye metadata si existe)</returns>
    [HttpGet("seguimiento")]
    public async Task<ActionResult<ListaSeguimientoResponse>> ListarSeguimiento(
        [FromQuery] int? loteId = null,
        [FromQuery] int? lotePosturaProduccionId = null,
        [FromQuery] DateTime? desde = null,
        [FromQuery] DateTime? hasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int size = 100)
    {
        try
        {
            if (page < 1) page = 1;
            // Compat: antes el frontend enviaba size=100 (máximo). Ahora 0 significa "traer todos".
            if (size == 100) size = 0;
            if (size < 0) size = 0;

            var resultado = await _produccionService.ListarSeguimientoAsync(loteId, lotePosturaProduccionId, desde, hasta, page, size);
            return Ok(resultado);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Devuelve la información general del lote para el módulo Seguimiento (Postura Producción).
    /// Calcula edad en semanas de producción (desde semana 26 del encaset), totales y consumo.
    /// </summary>
    [HttpGet("seguimiento/informacion-lote")]
    public async Task<ActionResult<InformacionLoteResponse>> ObtenerInformacionLote(
        [FromQuery] int lotePosturaProduccionId)
    {
        try
        {
            var info = await _produccionService.ObtenerInformacionLoteAsync(lotePosturaProduccionId);
            return Ok(info);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el detalle completo de un seguimiento diario por su ID.
    /// Incluye todos los campos del registro y la metadata (itemsHembras, itemsMachos, consumos originales, etc.)
    /// para poder editar el formulario en el frontend con todos los datos guardados.
    /// </summary>
    /// <param name="id">ID del seguimiento</param>
    /// <returns>Registro completo (id, fechaRegistro, mortalidad, consumo, huevos, tipoAlimento, metadata, etc.)</returns>
    [HttpGet("seguimiento/{id}")]
    public async Task<ActionResult<SeguimientoItemDto>> ObtenerSeguimientoPorId(int id)
    {
        try
        {
            var seguimiento = await _produccionService.ObtenerSeguimientoPorIdAsync(id);
            
            if (seguimiento == null)
            {
                return NotFound(new { message = "No se encontró el seguimiento especificado" });
            }

            return Ok(seguimiento);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Elimina un seguimiento diario de producción
    /// </summary>
    /// <param name="id">ID del seguimiento a eliminar</param>
    /// <returns>204 NoContent si se eliminó correctamente, 404 si no se encontró</returns>
    [HttpDelete("seguimiento/{id}")]
    public async Task<IActionResult> EliminarSeguimiento(int id)
    {
        try
        {
            var deleted = await _produccionService.EliminarSeguimientoAsync(id);
            if (!deleted)
            {
                return NotFound(new { message = "Registro no encontrado o no tienes permisos para eliminarlo." });
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Error al eliminar el registro.", error = ex.Message });
        }
    }

    /// <summary>
    /// Obtiene los lotes que tienen semana 26 o superior (para módulo de producción)
    /// Solo incluye lotes que han alcanzado la semana 26 desde su fecha de encaset
    /// </summary>
    /// <returns>Lista de lotes con semana >= 26</returns>
    [HttpGet("lotes-produccion")]
    public async Task<ActionResult<IEnumerable<LoteDtos.LoteDetailDto>>> ObtenerLotesProduccion()
    {
        try
        {
            var lotes = await _produccionService.ObtenerLotesProduccionAsync();
            return Ok(lotes);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Calcula la liquidación técnica de producción para un lote
    /// Organizado por etapas: 1 (25-33), 2 (34-50), 3 (>50)
    /// </summary>
    /// <param name="request">Request con loteId y fecha opcional</param>
    /// <returns>Liquidación técnica de producción</returns>
    [HttpPost("liquidacion-tecnica")]
    public async Task<ActionResult<LiquidacionTecnicaProduccionDto>> CalcularLiquidacionProduccion(
        [FromBody] LiquidacionTecnicaProduccionRequest request)
    {
        try
        {
            var liquidacion = await _liquidacionProduccionService.CalcularLiquidacionProduccionAsync(request);
            return Ok(liquidacion);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Verifica si un lote tiene datos de producción diaria válidos para liquidación (semana >= 26)
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <returns>True si el lote es válido para liquidación</returns>
    [HttpGet("liquidacion-tecnica/validar/{loteId}")]
    public async Task<ActionResult<bool>> ValidarLoteParaLiquidacionProduccion(int loteId)
    {
        try
        {
            var esValido = await _liquidacionProduccionService.ValidarLoteParaLiquidacionProduccionAsync(loteId);
            return Ok(esValido);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene el resumen de una etapa específica para un lote
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <param name="etapa">Etapa (1, 2 o 3)</param>
    /// <returns>Resumen de la etapa</returns>
    [HttpGet("liquidacion-tecnica/etapa/{loteId}/{etapa}")]
    public async Task<ActionResult<EtapaLiquidacionDto>> ObtenerResumenEtapa(int loteId, int etapa)
    {
        try
        {
            if (etapa < 1 || etapa > 3)
                return BadRequest(new { message = "La etapa debe ser 1, 2 o 3" });

            var resumen = await _liquidacionProduccionService.ObtenerResumenEtapaAsync(loteId, etapa);
            if (resumen == null)
                return NotFound(new { message = $"No se encontró resumen para la etapa {etapa}" });

            return Ok(resumen);
        }
        catch (Exception)
        {
            return StatusCode(500, new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Obtiene indicadores semanales de producción agrupados por semana
    /// Compara con guía genética cuando está disponible
    /// </summary>
    /// <param name="request">Request con loteId y filtros opcionales</param>
    /// <returns>Indicadores semanales de producción</returns>
    [HttpPost("indicadores-semanales")]
    public async Task<ActionResult<IndicadoresProduccionResponse>> ObtenerIndicadoresSemanales(
        [FromBody] IndicadoresProduccionRequest request)
    {
        try
        {
            var indicadores = await _indicadoresProduccionService.ObtenerIndicadoresSemanalesAsync(request);
            return Ok(indicadores);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "VALIDATION" });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Indicadores semanales: operación no válida. LoteId: {LoteId}", request?.LoteId);
            return BadRequest(new { message = ex.Message, code = "INVALID_OPERATION" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener indicadores semanales. LoteId: {LoteId}", request?.LoteId);
            var detail = new
            {
                message = "Error interno del servidor al calcular indicadores semanales.",
                detail = ex.Message,
                exceptionType = ex.GetType().FullName,
                step = "ObtenerIndicadoresSemanalesAsync",
                loteId = request?.LoteId
            };
            if (_env.IsDevelopment())
            {
                return StatusCode(500, new
                {
                    detail.message,
                    detail.detail,
                    detail.exceptionType,
                    detail.step,
                    detail.loteId,
                    stackTrace = ex.StackTrace,
                    innerMessage = ex.InnerException?.Message
                });
            }
            return StatusCode(500, detail);
        }
    }

    /// <summary>
    /// Obtiene indicadores para una semana específica
    /// </summary>
    /// <param name="loteId">ID del lote</param>
    /// <param name="semana">Número de semana</param>
    /// <returns>Indicadores de la semana</returns>
    [HttpGet("indicadores-semanales/{loteId}/{semana}")]
    public async Task<ActionResult<IndicadorProduccionSemanalDto>> ObtenerIndicadorSemana(int loteId, int semana)
    {
        try
        {
            var indicador = await _indicadoresProduccionService.ObtenerIndicadorSemanaAsync(loteId, semana);
            if (indicador == null)
                return NotFound(new { message = $"No se encontraron indicadores para la semana {semana}" });

            return Ok(indicador);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "VALIDATION" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener indicador semana. LoteId: {LoteId}, Semana: {Semana}", loteId, semana);
            return StatusCode(500, new
            {
                message = "Error interno del servidor al obtener indicador de la semana.",
                detail = ex.Message,
                exceptionType = ex.GetType().FullName,
                loteId,
                semana
            });
        }
    }
}
