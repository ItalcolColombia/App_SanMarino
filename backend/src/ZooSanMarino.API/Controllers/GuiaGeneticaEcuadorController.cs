using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/guia-genetica-ecuador")]
[Produces("application/json")]
[Tags("GuiaGeneticaEcuador")]
public class GuiaGeneticaEcuadorController : ControllerBase
{
    private readonly IGuiaGeneticaEcuadorService _service;
    private readonly ILogger<GuiaGeneticaEcuadorController> _logger;

    public GuiaGeneticaEcuadorController(
        IGuiaGeneticaEcuadorService service,
        ILogger<GuiaGeneticaEcuadorController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("filters")]
    [ProducesResponseType(typeof(GuiaGeneticaEcuadorFiltersDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GuiaGeneticaEcuadorFiltersDto>> GetFilters(CancellationToken ct)
    {
        var dto = await _service.GetFiltersAsync(ct);
        return Ok(dto);
    }

    /// <summary>Lista años disponibles para una raza (solo guías activas).</summary>
    [HttpGet("anos")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<int>>> GetAnos(
        [FromQuery] string raza,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raza))
            return BadRequest("raza es requerida.");

        var anos = await _service.GetAnosPorRazaAsync(raza, ct);
        return Ok(anos);
    }

    [HttpGet("sexos")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> GetSexos(
        [FromQuery] string raza,
        [FromQuery] int anioGuia,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 0)
            return BadRequest("raza y anioGuia son requeridos.");
        var sexos = await _service.GetSexosCreadosAsync(raza, anioGuia, ct);
        return Ok(sexos);
    }

    [HttpGet("datos")]
    [ProducesResponseType(typeof(IEnumerable<GuiaGeneticaEcuadorDetalleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GuiaGeneticaEcuadorDetalleDto>>> GetDatos(
        [FromQuery] string raza,
        [FromQuery] int anioGuia,
        [FromQuery] string sexo,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 0 || string.IsNullOrWhiteSpace(sexo))
            return BadRequest("raza, anioGuia y sexo son requeridos.");
        var data = await _service.GetDatosAsync(raza, anioGuia, sexo, ct);
        return Ok(data);
    }

    /// <summary>
    /// Datos para el tab Indicadores (seguimiento pollo engorde / levante): curva Ecuador <strong>mixto</strong> agrupada por semanas de 7 días, mismo shape que <c>/api/guia-genetica/rango</c>.
    /// </summary>
    [HttpGet("indicadores-rango")]
    [ProducesResponseType(typeof(IEnumerable<GuiaGeneticaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GuiaGeneticaDto>>> GetIndicadoresRango(
        [FromQuery] string raza,
        [FromQuery] int anioGuia,
        [FromQuery] int semanaDesde = 1,
        [FromQuery] int semanaHasta = 25,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(raza) || anioGuia <= 0)
            return BadRequest("raza y anioGuia son requeridos.");
        var data = await _service.GetIndicadoresRangoSemanasAsync(raza, anioGuia, semanaDesde, semanaHasta, ct);
        return Ok(data);
    }

    /// <summary>Carga masiva: Excel con hojas mixto / hembra / macho + raza, año y estado en el formulario.</summary>
    [HttpPost("import")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [ProducesResponseType(typeof(GuiaGeneticaEcuadorImportResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<GuiaGeneticaEcuadorImportResultDto>> Import(
        IFormFile file,
        [FromForm] string raza,
        [FromForm] int anioGuia,
        [FromForm] string? estado,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.ImportExcelAsync(file, raza, anioGuia, estado ?? "active", ct);
            if (!result.Success)
                _logger.LogWarning("Import guía Ecuador con incidencias: {Errores}", string.Join("; ", result.Errors));
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importando guía genética Ecuador");
            return StatusCode(500, new GuiaGeneticaEcuadorImportResultDto(false, 0, 0, 1, new[] { ex.Message }));
        }
    }

    /// <summary>Alta/actualización manual de filas por sexo (reemplaza el detalle de ese sexo).</summary>
    [HttpPost("manual")]
    [ProducesResponseType(typeof(GuiaGeneticaEcuadorHeaderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuiaGeneticaEcuadorHeaderDto>> Manual(
        [FromBody] GuiaGeneticaEcuadorManualRequestDto body,
        CancellationToken ct)
    {
        try
        {
            var dto = await _service.UpsertManualAsync(body, ct);
            return Ok(dto);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
