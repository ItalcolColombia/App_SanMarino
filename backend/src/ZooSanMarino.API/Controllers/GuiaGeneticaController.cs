using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/guia-genetica")]
[Produces("application/json")]
public class GuiaGeneticaController : ControllerBase
{
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public GuiaGeneticaController(IGuiaGeneticaService guiaGeneticaService)
    {
        _guiaGeneticaService = guiaGeneticaService;
    }

    /// <summary>
    /// Obtiene guía genética para (raza, año, edad)
    /// </summary>
    [HttpGet("obtener")]
    [ProducesResponseType(typeof(GuiaGeneticaResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(GuiaGeneticaResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GuiaGeneticaResponse>> ObtenerGuiaGenetica(
        [FromQuery] string raza,
        [FromQuery] int anoTabla,
        [FromQuery] int edad)
    {
        if (string.IsNullOrWhiteSpace(raza) || anoTabla <= 0 || edad <= 0)
        {
            return BadRequest(new GuiaGeneticaResponse(false, null, "Parámetros inválidos. Raza, año y edad son requeridos."));
        }

        var request = new GuiaGeneticaRequest(raza.Trim(), anoTabla, edad);
        var response = await _guiaGeneticaService.ObtenerGuiaGeneticaAsync(request);

        if (!response.Existe) return NotFound(response);
        return Ok(response);
    }

    /// <summary>
    /// Obtiene guía genética para un rango de edades
    /// </summary>
    [HttpGet("rango")]
    [ProducesResponseType(typeof(IEnumerable<GuiaGeneticaDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<GuiaGeneticaDto>>> ObtenerGuiaGeneticaRango(
        [FromQuery] string raza,
        [FromQuery] int anoTabla,
        [FromQuery] int edadDesde,
        [FromQuery] int edadHasta)
    {
        if (string.IsNullOrWhiteSpace(raza) || anoTabla <= 0 || edadDesde <= 0 || edadHasta < edadDesde)
        {
            return BadRequest("Parámetros inválidos.");
        }

        var data = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(raza.Trim(), anoTabla, edadDesde, edadHasta);
        return Ok(data);
    }

    /// <summary>
    /// Verifica existencia de guía genética para (raza, año)
    /// </summary>
    [HttpGet("existe")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult<bool>> ExisteGuiaGenetica(
        [FromQuery] string raza,
        [FromQuery] int anoTabla)
    {
        if (string.IsNullOrWhiteSpace(raza) || anoTabla <= 0)
        {
            return Ok(false);
        }

        var existe = await _guiaGeneticaService.ExisteGuiaGeneticaAsync(raza.Trim(), anoTabla);
        return Ok(existe);
    }

    /// <summary>
    /// Lista de razas disponibles
    /// </summary>
    [HttpGet("razas")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<string>>> ObtenerRazasDisponibles()
    {
        var razas = await _guiaGeneticaService.ObtenerRazasDisponiblesAsync();
        return Ok(razas);
    }

    /// <summary>
    /// Años disponibles para una raza
    /// </summary>
    [HttpGet("anos")]
    [ProducesResponseType(typeof(IEnumerable<int>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<int>>> ObtenerAnosDisponibles([FromQuery] string raza)
    {
        if (string.IsNullOrWhiteSpace(raza)) return BadRequest("Raza es requerida.");
        var anos = await _guiaGeneticaService.ObtenerAnosDisponiblesAsync(raza.Trim());
        return Ok(anos);
    }

    /// <summary>
    /// Obtiene información completa de una raza (años disponibles sin repetir y validación)
    /// </summary>
    [HttpGet("info-raza")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<object>> ObtenerInformacionRaza([FromQuery] string raza)
    {
        if (string.IsNullOrWhiteSpace(raza))
        {
            return Ok(new
            {
                esValida = false,
                anosDisponibles = Array.Empty<int>()
            });
        }

        var anos = await _guiaGeneticaService.ObtenerAnosDisponiblesAsync(raza.Trim());
        
        // Eliminar duplicados y ordenar
        var anosUnicos = anos.Distinct().OrderBy(a => a).ToList();

        return Ok(new
        {
            esValida = anosUnicos.Any(),
            anosDisponibles = anosUnicos
        });
    }
}