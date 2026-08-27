// src/ZooSanMarino.API/Controllers/GuiaGeneticaSantaReyesController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.API.Infrastructure;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using PagedResultCommon = ZooSanMarino.Application.DTOs.Common.PagedResult<ZooSanMarino.Application.DTOs.GuiaGeneticaSantaReyesDto>;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Guía Genética <b>reducida</b> (<c>guia_genetica_santa_reyes</c>): la puerta de escritura que la
/// tabla nunca tuvo. Una línea = raza + año + semana, con tres métricas (% producción, retiro
/// acumulado de hembras y consumo gr/ave/día).
///
/// <para>
/// 🔴 <b>Toda ESCRITURA pasa por dos guardas</b> (<see cref="GuiaGeneticaEscrituraGuard"/>):
/// el permiso <c>guia_genetica.gestionar</c> y el perfil de guía de la empresa activa, que tiene
/// que ser <c>reducida</c>. Las LECTURAS quedan abiertas a cualquier sesión, igual que en el resto
/// de los módulos de configuración: consultar y exportar la guía no es escribirla.
/// </para>
/// </summary>
[ApiController]
[Route("api/guia-genetica-santa-reyes")]
[Produces("application/json")]
[Tags("GuiaGeneticaSantaReyes")]
[Authorize]
public class GuiaGeneticaSantaReyesController : ControllerBase
{
    private readonly IGuiaGeneticaSantaReyesService _service;
    private readonly IGuiaGeneticaPerfilResolver _perfilResolver;
    private readonly ILogger<GuiaGeneticaSantaReyesController> _logger;

    public GuiaGeneticaSantaReyesController(
        IGuiaGeneticaSantaReyesService service,
        IGuiaGeneticaPerfilResolver perfilResolver,
        ILogger<GuiaGeneticaSantaReyesController> logger)
    {
        _service = service;
        _perfilResolver = perfilResolver;
        _logger = logger;
    }

    /// <summary>Perfil de guía que administra este módulo.</summary>
    private const string PerfilDeLaTabla = GuiaGeneticaPerfilCalculos.Reducida;

    /// <summary>
    /// Listado paginado de la guía de la empresa activa.
    /// <para>
    /// Es <c>GET</c> con filtros por query string (no <c>POST /search</c>) para que el grid pueda
    /// linkear un estado de filtro y para que el navegador lo cachee. Pedir más de 2.000 filas
    /// devuelve 2.000, nunca el default de 20 (ver <c>PaginacionCalculos</c>).
    /// </para>
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultCommon), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultCommon>> Listar(
        [FromQuery] string? raza = null,
        [FromQuery] string? anioGuia = null,
        [FromQuery] int? edadDesde = null,
        [FromQuery] int? edadHasta = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDesc = false,
        CancellationToken ct = default)
    {
        try
        {
            var request = new GuiaGeneticaSantaReyesSearchRequest(
                raza, anioGuia, edadDesde, edadHasta, page, pageSize, sortBy, sortDesc);

            return Ok(await _service.SearchAsync(request, ct));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar la guía genética reducida");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Una línea por id.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(GuiaGeneticaSantaReyesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuiaGeneticaSantaReyesDto>> ObtenerPorId(int id, CancellationToken ct = default)
    {
        try
        {
            var item = await _service.GetByIdAsync(id, ct);
            return item is null
                ? NotFound(new { message = $"Línea de guía genética con ID {id} no encontrada" })
                : Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al obtener la línea de guía genética {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Crea una línea. La raza es texto libre (ver <c>CreateGuiaGeneticaSantaReyesDto</c>).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GuiaGeneticaSantaReyesDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<GuiaGeneticaSantaReyesDto>> Crear(
        [FromBody] CreateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido" });

        var rechazo = await this.ExigirEscrituraGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla, ct);
        if (rechazo is not null) return rechazo;

        try
        {
            var creada = await _service.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(ObtenerPorId), new { id = creada.Id }, creada);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al crear una línea de guía genética");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>Edita una línea. Cambiar raza/año/semana recalcula el código de la guía.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(GuiaGeneticaSantaReyesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuiaGeneticaSantaReyesDto>> Actualizar(
        int id, [FromBody] UpdateGuiaGeneticaSantaReyesDto dto, CancellationToken ct = default)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (dto is null) return BadRequest(new { message = "El cuerpo de la petición es requerido" });
        if (id != dto.Id) return BadRequest(new { message = "El ID de la ruta no coincide con el del cuerpo" });

        var rechazo = await this.ExigirEscrituraGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla, ct);
        if (rechazo is not null) return rechazo;

        try
        {
            return Ok(await _service.UpdateAsync(dto, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message, error = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al actualizar la línea de guía genética {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Da de baja una línea. 🔴 Es una baja <b>suave</b> (<c>deleted_at</c>): la guía es el insumo de
    /// los indicadores técnicos y un borrado en duro se llevaría la trazabilidad del histórico.
    /// </summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct = default)
    {
        var rechazo = await this.ExigirEscrituraGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla, ct);
        if (rechazo is not null) return rechazo;

        try
        {
            return await _service.DeleteAsync(id, ct)
                ? NoContent()
                : NotFound(new { message = $"Línea de guía genética con ID {id} no encontrada" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al dar de baja la línea de guía genética {Id}", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno del servidor" });
        }
    }

    /// <summary>
    /// Importa la guía desde un Excel. <b>Idempotente</b>: reimportar el mismo archivo actualiza lo
    /// que cambió y no duplica nada (la clave es raza + año + semana).
    /// </summary>
    [HttpPost("import")]
    [ProducesResponseType(typeof(GuiaGeneticaSantaReyesImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<GuiaGeneticaSantaReyesImportResultDto>> Importar(
        IFormFile file, CancellationToken ct = default)
    {
        if (file is null) return BadRequest(new { message = "No se ha proporcionado ningún archivo." });

        var rechazo = await this.ExigirEscrituraGuiaGeneticaAsync(_perfilResolver, PerfilDeLaTabla, ct);
        if (rechazo is not null) return rechazo;

        try
        {
            await using var stream = file.OpenReadStream();
            var resultado = await _service.ImportarExcelAsync(stream, file.FileName, file.Length, ct);

            _logger.LogInformation(
                "Import de guía genética reducida: {Insertados} altas, {Actualizados} cambios, " +
                "{Omitidos} sin cambios, {Errores} errores ({Archivo}).",
                resultado.Insertados, resultado.Actualizados, resultado.Omitidos,
                resultado.Errores.Count, file.FileName);

            return Ok(resultado);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al importar la guía genética desde {Archivo}", file.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error interno durante la importación del archivo." });
        }
    }

    /// <summary>
    /// Descarga la plantilla del import. Es una LECTURA: no requiere el permiso de escritura, para
    /// que quien prepara el archivo pueda hacerlo aunque no sea quien lo sube.
    /// </summary>
    [HttpGet("plantilla")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public IActionResult DescargarPlantilla()
    {
        try
        {
            var bytes = _service.GenerarPlantillaExcel();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"plantilla_guia_genetica_{DateTime.Now:yyyyMMdd}.xlsx");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar la plantilla de guía genética");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Error al generar la plantilla Excel." });
        }
    }
}
