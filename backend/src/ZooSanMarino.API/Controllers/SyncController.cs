using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Sincronización de capturas hechas sin conexión (PWA F3).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SyncController : ControllerBase
{
    private readonly ISyncPushService _svc;

    public SyncController(ISyncPushService svc) => _svc = svc;

    /// <summary>
    /// Aplica un lote de operaciones capturadas sin red.
    ///
    /// Devuelve **200 con un resultado por operación**, no un todo-o-nada: que una captura se rechace
    /// no puede impedir que las otras 24 entren. El cliente decide qué hacer con cada una mirando
    /// <c>errorCodigo</c>, que es estable; el <c>detalle</c> es para el humano y puede cambiar.
    ///
    /// El endpoint **ignora `X-Active-Company`**: la empresa sale de cada operación y se valida contra
    /// el token. Una captura del sábado en la empresa A no se aplica en la B porque el lunes el
    /// usuario abrió otra empresa.
    /// </summary>
    [HttpPost("push")]
    [ProducesResponseType(typeof(SyncPushResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SyncPushResponse>> Push([FromBody] SyncPushRequest request, CancellationToken ct)
    {
        var errorLote = SyncPushCalculos.ValidarLote(request?.Operaciones?.Count ?? 0);
        if (errorLote is not null)
        {
            // Tipado, no texto libre: el cliente parte el lote solo si sabe POR QUÉ se rechazó.
            // Y se rechaza entero — procesar "las primeras 25" haría que el dispositivo diera por
            // enviadas operaciones que el servidor nunca vio.
            return BadRequest(new
            {
                errorCodigo = errorLote,
                maxOperaciones = SyncPushCalculos.MaxOperacionesPorLote
            });
        }

        return Ok(await _svc.PushAsync(request!, ct));
    }
}
