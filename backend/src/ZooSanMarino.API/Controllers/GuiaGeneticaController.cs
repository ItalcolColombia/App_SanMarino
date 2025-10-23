using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/guia-genetica")]
public class GuiaGeneticaController : ControllerBase
{
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public GuiaGeneticaController(IGuiaGeneticaService guiaGeneticaService)
    {
        _guiaGeneticaService = guiaGeneticaService;
    }

    /// <summary>
    /// Obtiene datos de guía genética para una edad específica
    /// </summary>
    /// <param name="raza">Raza de la guía genética</param>
    /// <param name="anoTabla">Año de la tabla genética</param>
    /// <param name="edad">Edad en semanas</param>
    /// <returns>Datos de la guía genética</returns>
    [HttpGet("obtener")]
    [ProducesResponseType(typeof(GuiaGeneticaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GuiaGeneticaResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GuiaGeneticaResponse>> ObtenerGuiaGenetica(
        [FromQuery] string raza, 
        [FromQuery] int anoTabla, 
        [FromQuery] int edad)
    {
        if (string.IsNullOrEmpty(raza) || anoTabla <= 0 || edad <= 0)
        {
            return BadRequest(new GuiaGeneticaResponse(
                Existe: false,
                Datos: null,
                Mensaje: "Parámetros inválidos. Raza, año y edad son requeridos."
            ));
        }

        var request = new GuiaGeneticaRequest(raza, anoTabla, edad);
        var response = await _guiaGeneticaService.ObtenerGuiaGeneticaAsync(request);

        if (!response.Existe)
        {
            return NotFound(response);
        }

        return Ok(response);
    }

    /// <summary>
    /// Obtiene datos de guía genética para un rango de edades
    /// </summary>
    /// <param name="raza">Raza de la guía genética</param>
    /// <param name="anoTabla">Año de la tabla genética</param>
    /// <param name="edadDesde">Edad inicial en semanas</param>
    /// <param name="edadHasta">Edad final en semanas</param>
    /// <returns>Lista de datos de la guía genética</returns>
    [HttpGet("rango")]
    [ProducesResponseType(typeof(IEnumerable<GuiaGeneticaDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<GuiaGeneticaDto>>> ObtenerGuiaGeneticaRango(
        [FromQuery] string raza, 
        [FromQuery] int anoTabla, 
        [FromQuery] int edadDesde, 
        [FromQuery] int edadHasta)
    {
        if (string.IsNullOrEmpty(raza) || anoTabla <= 0 || edadDesde <= 0 || edadHasta <= 0 || edadDesde > edadHasta)
        {
            return BadRequest("Parámetros inválidos. Raza, año y rango de edades son requeridos.");
        }

        var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(raza, anoTabla, edadDesde, edadHasta);
        return Ok(guias);
    }

    /// <summary>
    /// Verifica si existe una guía genética
    /// </summary>
    /// <param name="raza">Raza de la guía genética</param>
    /// <param name="anoTabla">Año de la tabla genética</param>
    /// <returns>True si existe la guía genética</returns>
    [HttpGet("existe")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<bool>> ExisteGuiaGenetica(
        [FromQuery] string raza, 
        [FromQuery] int anoTabla)
    {
        if (string.IsNullOrEmpty(raza) || anoTabla <= 0)
        {
            return BadRequest("Parámetros inválidos. Raza y año son requeridos.");
        }

        var existe = await _guiaGeneticaService.ExisteGuiaGeneticaAsync(raza, anoTabla);
        return Ok(existe);
    }

    /// <summary>
    /// Obtiene las razas disponibles en las guías genéticas
    /// </summary>
    /// <returns>Lista de razas disponibles</returns>
    [HttpGet("razas")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> ObtenerRazasDisponibles()
    {
        try
        {
            var razas = await _guiaGeneticaService.ObtenerRazasDisponiblesAsync();
            return Ok(razas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    /// <summary>
    /// Endpoint temporal para probar las razas sin autenticación
    /// </summary>
    [HttpGet("test-razas")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> TestRazas()
    {
        try
        {
            var razas = await _guiaGeneticaService.ObtenerRazasDisponiblesAsync();
            return Ok(razas);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    /// <summary>
    /// Endpoint de depuración para ver datos de la tabla
    /// </summary>
    [HttpGet("debug-data")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> DebugData()
    {
        try
        {
            using var scope = HttpContext.RequestServices.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<ZooSanMarino.Infrastructure.Persistence.ZooSanMarinoContext>();
            
            var rawData = await ctx.ProduccionAvicolaRaw
                .Where(p => !string.IsNullOrEmpty(p.Raza))
                .Take(10)
                .Select(p => new { p.Id, p.Raza, p.AnioGuia, p.Edad })
                .ToListAsync();

            var razas = await ctx.ProduccionAvicolaRaw
                .Where(p => !string.IsNullOrEmpty(p.Raza))
                .Select(p => p.Raza!)
                .Distinct()
                .ToListAsync();

            return Ok(new { 
                rawData, 
                razasDistintas = razas,
                totalRegistros = await ctx.ProduccionAvicolaRaw.CountAsync()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene información completa de una raza (incluyendo años disponibles)
    /// </summary>
    /// <param name="raza">Raza de la guía genética</param>
    /// <returns>Información de la raza con años disponibles</returns>
    [HttpGet("info-raza")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> ObtenerInformacionRaza([FromQuery] string raza)
    {
        if (string.IsNullOrEmpty(raza))
        {
            return BadRequest("Raza es requerida.");
        }

        try
        {
            var anos = await _guiaGeneticaService.ObtenerAnosDisponiblesAsync(raza);
            var existe = await _guiaGeneticaService.ExisteGuiaGeneticaAsync(raza, anos.FirstOrDefault());
            
            return Ok(new { 
                esValida = existe && anos.Any(),
                anosDisponibles = anos.ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error interno: {ex.Message}");
        }
    }

    /// <summary>
    /// Obtiene los años disponibles para una raza específica
    /// </summary>
    /// <param name="raza">Raza de la guía genética</param>
    /// <returns>Lista de años disponibles</returns>
    [HttpGet("anos")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<int>>> ObtenerAnosDisponibles([FromQuery] string raza)
    {
        if (string.IsNullOrEmpty(raza))
        {
            return BadRequest("Raza es requerida.");
        }

        var anos = await _guiaGeneticaService.ObtenerAnosDisponiblesAsync(raza);
        return Ok(anos);
    }
}
