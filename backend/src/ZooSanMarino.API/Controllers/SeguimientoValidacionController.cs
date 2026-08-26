// src/ZooSanMarino.API/Controllers/SeguimientoValidacionController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Doble validación de los seguimientos diarios.
///
/// <para>
/// Mientras un registro no está validado se puede editar y eliminar, y lo que consumió está
/// <b>separado</b> (reservado), no descontado. Validar es lo que aplica el consumo al inventario y
/// baja las aves del maestro del lote.
/// </para>
///
/// <para>
/// La ruta no lleva <c>admin</c> en el path a propósito: el WAF devuelve 403 a cualquier path de API
/// que lo contenga.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SeguimientoValidacionController : ControllerBase
{
    private readonly IValidacionSeguimientoService _service;
    private readonly ILogger<SeguimientoValidacionController> _logger;

    public SeguimientoValidacionController(
        IValidacionSeguimientoService service,
        ILogger<SeguimientoValidacionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    /// <summary>
    /// Valida un registro: aplica el consumo de alimento y el descuento de aves, y lo deja de solo
    /// lectura.
    /// </summary>
    /// <param name="modulo">LEVANTE | PRODUCCION | ENGORDE | ENGORDE_EC | REPRODUCTORA.</param>
    /// <param name="id">Id del registro en la tabla de su módulo.</param>
    [HttpPost("{modulo}/{id:long}/validar")]
    [ProducesResponseType(typeof(ResultadoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Validar(string modulo, long id, CancellationToken ct)
    {
        var normalizado = Normalizar(modulo);
        if (normalizado is null) return BadRequest(new { message = MensajeModuloInvalido(modulo) });

        try
        {
            return Ok(await _service.ValidarAsync(normalizado, id, ct));
        }
        // El permiso faltante NO es un 500: el front lo usa para explicar por qué no ve el botón.
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Valida en bloque todos los pendientes de un lote, del día más viejo al más nuevo, cortando en
    /// la primera falla.
    ///
    /// <para>
    /// Devuelve <b>200 aunque haya cortado</b>: un corte no es un error de la request, es su
    /// resultado. Lo que se validó, se validó —cada registro va en su propia transacción— y el cuerpo
    /// trae el detalle registro por registro más el motivo del corte. El 403 sigue siendo 403 porque
    /// ahí no se aplicó nada.
    /// </para>
    /// </summary>
    /// <param name="modulo">LEVANTE | PRODUCCION | ENGORDE | ENGORDE_EC | REPRODUCTORA.</param>
    /// <param name="loteId">Clave numérica del lote en ese módulo.</param>
    [HttpPost("{modulo}/lote/{loteId:int}/validar-pendientes")]
    [ProducesResponseType(typeof(ResultadoValidacionEnBloqueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ValidarPendientes(string modulo, int loteId, CancellationToken ct)
    {
        var normalizado = Normalizar(modulo);
        if (normalizado is null) return BadRequest(new { message = MensajeModuloInvalido(modulo) });

        try
        {
            var resultado = await _service.ValidarPendientesDelLoteAsync(normalizado, loteId, ct);
            _logger.LogInformation(
                "Validación en bloque {Modulo} lote {LoteId}: {Validados}/{Solicitados} validados, {NoIntentados} sin intentar",
                normalizado, loteId, resultado.Validados, resultado.Solicitados, resultado.NoIntentados);
            return Ok(resultado);
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Quita la validación: devuelve alimento y aves, y vuelve a dejar el registro editable. Es la
    /// única forma de corregir un registro ya validado, y pide su propio permiso.
    /// </summary>
    [HttpPost("{modulo}/{id:long}/desvalidar")]
    [ProducesResponseType(typeof(ResultadoValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Desvalidar(string modulo, long id, CancellationToken ct)
    {
        var normalizado = Normalizar(modulo);
        if (normalizado is null) return BadRequest(new { message = MensajeModuloInvalido(modulo) });

        try
        {
            return Ok(await _service.DesvalidarAsync(normalizado, id, ct));
        }
        catch (UnauthorizedAccessException ex) { return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Situación de validación de un lote: pendientes, vencidos y si eso bloquea el alta de un día
    /// nuevo. Lo consume el modal de alerta que aparece al entrar al lote.
    /// </summary>
    [HttpGet("{modulo}/pendientes")]
    [ProducesResponseType(typeof(PendientesValidacionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pendientes(string modulo, [FromQuery] int loteId, CancellationToken ct)
    {
        var normalizado = Normalizar(modulo);
        if (normalizado is null) return BadRequest(new { message = MensajeModuloInvalido(modulo) });

        return Ok(await _service.ObtenerPendientesAsync(normalizado, loteId, ct));
    }

    /// <summary>
    /// ¿La empresa activa opera con doble validación? El front ya lo tiene en el flag de la empresa;
    /// esto sirve para diagnosticar sin abrir la BD cuando la UI no muestra lo que se espera.
    /// </summary>
    [HttpGet("configuracion")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Configuracion(CancellationToken ct)
        => Ok(new
        {
            requiereValidacion = await _service.RequiereValidacionAsync(ct),
            diasPlazo = ValidacionSeguimientoCalculos.DiasPlazoValidacion
        });

    /// <summary>Acepta el módulo en cualquier capitalización y devuelve el literal canónico.</summary>
    private static string? Normalizar(string modulo)
    {
        var m = (modulo ?? "").Trim().ToUpperInvariant();
        return ModuloSeguimiento.EsValido(m) ? m : null;
    }

    private static string MensajeModuloInvalido(string modulo) =>
        $"Módulo de seguimiento desconocido: '{modulo}'. Válidos: " +
        $"{ModuloSeguimiento.Levante}, {ModuloSeguimiento.Produccion}, {ModuloSeguimiento.Engorde}, " +
        $"{ModuloSeguimiento.EngordeEcuador}, {ModuloSeguimiento.Reproductora}.";
}
