using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs.PuentePanama;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Puente de consulta ZooPanamaPollo → módulo de pollo engorde. Trae (solo lectura del origen) la guía
/// genética, granjas, lotes, seguimiento diario y reproductora, con filtros (año, cliente/granja, fecha
/// hasta) y los sincroniza de forma idempotente con la empresa ACTIVA (header X-Active-Company). Debe
/// ejecutarse logueado en la empresa destino (ItalcolPanama) con el permiso de integración.
/// Las credenciales del origen pueden venir en el body (front) o de la config del backend.
/// </summary>
[ApiController]
[Authorize]
[Route("api/sincronizacion-panama")]
[Tags("SincronizacionPanama")]
public class PuentePanamaController : ControllerBase
{
    private readonly IPuentePanamaService _svc;
    private readonly ILogger<PuentePanamaController> _logger;

    public PuentePanamaController(IPuentePanamaService svc, ILogger<PuentePanamaController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    /// <summary>Prueba la conexión/login contra el origen (con las credenciales del body o de la config).</summary>
    [HttpPost("probar-conexion")]
    [ProducesResponseType(typeof(ConexionResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConexionResultDto>> ProbarConexion([FromBody] PanamaConexion? origen, CancellationToken ct)
        => Ok(await _svc.ProbarConexionAsync(origen, ct));

    /// <summary>Clientes del origen (para el filtro del front).</summary>
    [HttpPost("clientes")]
    [ProducesResponseType(typeof(IEnumerable<PanamaCliente>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PanamaCliente>>> Clientes([FromBody] PanamaConexion? origen, CancellationToken ct)
        => Ok(await _svc.GetClientesOrigenAsync(origen, ct));

    /// <summary>Granjas del origen (todas o de un cliente) para el filtro del front.</summary>
    [HttpPost("granjas")]
    [ProducesResponseType(typeof(IEnumerable<PanamaGranja>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<PanamaGranja>>> Granjas(
        [FromBody] PanamaConexion? origen,
        [FromQuery] int? clienteIdOrigen,
        CancellationToken ct)
        => Ok(await _svc.GetGranjasOrigenAsync(origen, clienteIdOrigen, ct));

    /// <summary>
    /// Previsualiza (dry-run, NO inserta) qué traería la sincronización: cuenta guía/granjas/galpones/lotes/
    /// seguimientos, con detalle por lote y advertencias (p.ej. lotes sin guía genética).
    /// </summary>
    [HttpPost("previsualizar")]
    [ProducesResponseType(typeof(ResultadoSincronizacionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoSincronizacionDto>> Previsualizar([FromBody] SincronizarPanamaRequest request, CancellationToken ct)
    {
        request.DryRun = true;
        return Ok(await _svc.SincronizarAsync(request, ct));
    }

    /// <summary>Ejecuta la sincronización real (inserta). Idempotente: re-ejecutar no duplica.</summary>
    [HttpPost("sincronizar")]
    [ProducesResponseType(typeof(ResultadoSincronizacionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ResultadoSincronizacionDto>> Sincronizar([FromBody] SincronizarPanamaRequest request, CancellationToken ct)
    {
        var result = await _svc.SincronizarAsync(request, ct);
        if (result.Estado == "Fallido")
            _logger.LogWarning("Sincronización Panamá fallida: {Mensajes}", string.Join(" | ", result.Mensajes));
        return Ok(result);
    }

    /// <summary>
    /// Historial paginado de corridas del puente (empresa activa), más recientes primero.
    /// <paramref name="incluirValidaciones"/> = false excluye los dry-run (solo sincronizaciones reales).
    /// </summary>
    [HttpGet("historial")]
    [ProducesResponseType(typeof(SincronizacionHistorialPagedDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SincronizacionHistorialPagedDto>> Historial(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool incluirValidaciones = true,
        CancellationToken ct = default)
        => Ok(await _svc.GetHistorialAsync(page, pageSize, incluirValidaciones, ct));

    /// <summary>
    /// Detalle completo de una corrida del historial: los mismos contadores/lotes/mensajes de la
    /// previsualización, reconstruidos del detalle persistido. 404 si no existe o no es de la empresa activa.
    /// </summary>
    [HttpGet("historial/{id:int}")]
    [ProducesResponseType(typeof(SincronizacionHistorialDetalleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SincronizacionHistorialDetalleDto>> HistorialDetalle(int id, CancellationToken ct)
    {
        var det = await _svc.GetHistorialDetalleAsync(id, ct);
        return det is null ? NotFound() : Ok(det);
    }
}
