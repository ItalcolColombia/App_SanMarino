using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.Dashboard;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Datos del dashboard.
///
/// <para><b>Reescrito el 1-sep-2026.</b> Hasta esa fecha los 8 endpoints de este controller
/// devolvían datos INVENTADOS: <c>new Random()</c> en producción por granja y registros diarios,
/// constantes en estadísticas generales (25 usuarios, 8 granjas, 12.500 aves), seis actividades fijas
/// con nombres de personas inventados, y <c>distribucion-lotes</c> con todos los conteos en 0 y un
/// <c>// TODO: Implementar cuando esté disponible</c>. Además <b>ninguna acción recibía la empresa
/// ni el usuario</b> —el front mandaba <c>companyId</c>, <c>userId</c> y <c>farmIds</c> y se
/// perdían—, así que no había recorte de ningún tipo.</para>
///
/// <para>Ahora hay <b>un endpoint por panel</b> (lo que hace posible la carga perezosa real: el panel
/// que no se dibuja no se pide) y cada uno resuelve su alcance del lado del servidor:
/// <c>ICurrentUser.CompanyId</c> validado por <c>ActiveCompanyMiddleware</c> +
/// <c>ILocationScopeResolver</c>, y corta por el módulo del menú antes de consultar nada. <b>Ninguno
/// recibe la empresa por parámetro</b>: aceptarla del cliente sería confiar en el header crudo.</para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboard;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IDashboardService dashboard, ILogger<DashboardController> logger)
    {
        _dashboard = dashboard;
        _logger = logger;
    }

    /// <summary>
    /// Conteos generales del alcance del usuario: granjas asignadas, y lotes de postura y de engorde
    /// (activos y totales), recortados por empresa activa y por alcance de ubicación.
    /// </summary>
    /// <remarks>Un usuario sin granjas visibles recibe ceros, no la empresa entera.</remarks>
    [HttpGet("resumen")]
    [ProducesResponseType(typeof(DashboardResumenDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetResumen(CancellationToken ct)
        => Responder(() => _dashboard.GetResumenAsync(ct), "el resumen");

    /// <summary>
    /// Panel de postura: mortalidad y huevo por día, y lotes activos por granja.
    /// </summary>
    /// <param name="desde">Inicio del período (inclusive). Por defecto, 30 días atrás.</param>
    /// <param name="hasta">Fin del período (inclusive). Por defecto, hoy.</param>
    [HttpGet("postura")]
    [ProducesResponseType(typeof(DashboardPosturaDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPostura(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Responder(() => _dashboard.GetPosturaAsync(desde, hasta, ct), "el panel de postura");

    /// <summary>
    /// Panel de pollo engorde: mortalidad, consumo y peso promedio por día, y lotes activos por granja.
    /// </summary>
    [HttpGet("engorde")]
    [ProducesResponseType(typeof(DashboardEngordeDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetEngorde(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
        => Responder(() => _dashboard.GetEngordeAsync(desde, hasta, ct), "el panel de engorde");

    /// <summary>
    /// Panel de alimento e inventario: existencias por granja y galpones con descuadre.
    /// </summary>
    [HttpGet("inventario")]
    [ProducesResponseType(typeof(DashboardInventarioDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetInventario(CancellationToken ct)
        => Responder(() => _dashboard.GetInventarioAsync(ct), "el panel de inventario");

    /// <summary>
    /// Panel de cumplimiento: vacunación vencida y próxima, y cuadres offline sin resolver.
    /// </summary>
    [HttpGet("cumplimiento")]
    [ProducesResponseType(typeof(DashboardCumplimientoDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetCumplimiento(CancellationToken ct)
        => Responder(() => _dashboard.GetCumplimientoAsync(ct), "el panel de cumplimiento");

    /// <summary>
    /// Envoltorio común: 200 con el dato, 499 si el cliente se fue, 500 con un mensaje que no filtra
    /// el detalle interno (que sí va al log).
    /// </summary>
    private async Task<IActionResult> Responder<T>(Func<Task<T>> obtener, string queEs)
    {
        try
        {
            return Ok(await obtener());
        }
        catch (OperationCanceledException)
        {
            // El cliente se fue (cambió de pantalla, cerró el panel). No es un error del servidor —
            // y con @defer en el front esto pasa seguido.
            return new StatusCodeResult(StatusCodes.Status499ClientClosedRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al calcular {QueEs} del dashboard", queEs);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = $"No se pudo calcular {queEs} del dashboard." });
        }
    }
}
