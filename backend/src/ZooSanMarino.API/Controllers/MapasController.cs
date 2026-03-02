using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Mapas;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/mapas")]
[Authorize]
[Produces("application/json")]
public class MapasController : ControllerBase
{
    private readonly IMapaService _mapaService;

    public MapasController(IMapaService mapaService)
    {
        _mapaService = mapaService;
    }

    /// <summary>Lista todos los mapas de la compañía.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<MapaListDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MapaListDto>>> GetAll(CancellationToken ct = default)
    {
        var list = await _mapaService.GetAllAsync(ct);
        return Ok(list);
    }

    /// <summary>Obtiene un mapa por id con sus pasos.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MapaDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapaDetailDto>> GetById(int id, CancellationToken ct = default)
    {
        var mapa = await _mapaService.GetByIdAsync(id, ct);
        if (mapa == null) return NotFound(new { message = "Mapa no encontrado" });
        return Ok(mapa);
    }

    /// <summary>Historial de ejecuciones de un mapa (últimas N).</summary>
    [HttpGet("{id:int}/ejecuciones")]
    [ProducesResponseType(typeof(IEnumerable<MapaEjecucionHistorialDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<MapaEjecucionHistorialDto>>> GetEjecucionesByMapa(int id, [FromQuery] int limit = 20, CancellationToken ct = default)
    {
        var list = await _mapaService.GetEjecucionesByMapaAsync(id, limit, ct);
        return Ok(list);
    }

    /// <summary>Crea un nuevo mapa.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(MapaDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MapaDetailDto>> Create([FromBody] CreateMapaDto dto, CancellationToken ct = default)
    {
        try
        {
            var created = await _mapaService.CreateAsync(dto, ct);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("UserGuid") == true || ex.Message?.Contains("identificar al usuario") == true)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Actualiza un mapa.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(MapaDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<MapaDetailDto>> Update(int id, [FromBody] UpdateMapaDto dto, CancellationToken ct = default)
    {
        try
        {
            var updated = await _mapaService.UpdateAsync(id, dto, ct);
            if (updated == null) return NotFound(new { message = "Mapa no encontrado" });
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("UserGuid") == true || ex.Message?.Contains("identificar al usuario") == true)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    /// <summary>Elimina (soft) un mapa.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete(int id, CancellationToken ct = default)
    {
        var ok = await _mapaService.DeleteAsync(id, ct);
        if (!ok) return NotFound(new { message = "Mapa no encontrado" });
        return NoContent();
    }

    /// <summary>Guarda los pasos de un mapa (reemplaza todos).</summary>
    [HttpPut("{id:int}/pasos")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult> SavePasos(int id, [FromBody] List<MapaPasoDto> pasos, CancellationToken ct = default)
    {
        try
        {
            await _mapaService.SavePasosAsync(id, pasos ?? new List<MapaPasoDto>(), ct);
            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("no encontrado") == true)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("UserGuid") == true || ex.Message?.Contains("identificar al usuario") == true)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = ex.Message, detail = ex.InnerException?.Message });
        }
    }

    /// <summary>Ejecuta un mapa con los parámetros indicados. La ejecución es asíncrona; usar GET ejecuciones/{ejecucionId} para consultar estado.</summary>
    [HttpPost("{id:int}/ejecutar")]
    [ProducesResponseType(typeof(EjecutarMapaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<EjecutarMapaResponse>> Ejecutar(int id, [FromBody] EjecutarMapaRequest request, CancellationToken ct = default)
    {
        try
        {
            var response = await _mapaService.EjecutarAsync(id, request ?? new EjecutarMapaRequest(), ct);
            return Ok(response);
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("no encontrado") == true)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex) when (ex.Message?.Contains("UserGuid") == true || ex.Message?.Contains("identificar al usuario") == true)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Obtiene el estado de una ejecución (para polling hasta completado/error).</summary>
    [HttpGet("ejecuciones/{ejecucionId:int}")]
    [ProducesResponseType(typeof(MapaEjecucionEstadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MapaEjecucionEstadoDto>> GetEjecucionEstado(int ejecucionId, CancellationToken ct = default)
    {
        var estado = await _mapaService.GetEjecucionEstadoAsync(ejecucionId, ct);
        if (estado == null) return NotFound(new { message = "Ejecución no encontrada" });
        return Ok(estado);
    }

    /// <summary>Descarga el archivo generado por una ejecución completada (Excel).</summary>
    [HttpGet("ejecuciones/{ejecucionId:int}/descargar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DescargarEjecucion(int ejecucionId, CancellationToken ct = default)
    {
        var result = await _mapaService.GetEjecucionArchivoAsync(ejecucionId, ct);
        if (result == null || result.Value.Stream == null)
            return NotFound(new { message = "No hay archivo disponible para esta ejecución" });
        var (stream, fileName) = result.Value;
        const string contentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        return File(stream, contentType, fileName);
    }
}
