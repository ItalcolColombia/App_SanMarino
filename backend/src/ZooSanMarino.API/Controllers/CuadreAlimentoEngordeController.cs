// src/ZooSanMarino.API/Controllers/CuadreAlimentoEngordeController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Cuadre de alimento de pollo engorde: verifica, galpón por galpón, que la tabla diaria y el
/// inventario sigan contando lo mismo.
/// <para>
/// Invariante: <c>saldo del ciclo activo == stock físico − movimientos posteriores al último
/// seguimiento</c>. El descuadre que originó el trabajo de jul-2026 lo detectó un humano de operación
/// semanas después; esto lo pone a la vista el mismo día.
/// </para>
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CuadreAlimentoEngordeController : ControllerBase
{
    private readonly ICuadreAlimentoEngordeService _service;
    private readonly ILogger<CuadreAlimentoEngordeController> _logger;
    private readonly ICurrentUser _current;

    public CuadreAlimentoEngordeController(
        ICuadreAlimentoEngordeService service,
        ILogger<CuadreAlimentoEngordeController> logger,
        ICurrentUser current)
    {
        _service = service;
        _logger = logger;
        _current = current;
    }

    /// <summary>
    /// Cuadre de todos los galpones de la empresa activa.
    /// </summary>
    /// <param name="soloConProblemas">
    /// Si es <c>true</c>, el detalle trae solo los galpones que requieren atención. El resumen se
    /// calcula siempre sobre el total.
    /// </param>
    [HttpGet]
    [ProducesResponseType(typeof(CuadreAlimentoEngordeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<CuadreAlimentoEngordeDto>> Get(
        [FromQuery] bool soloConProblemas = false,
        CancellationToken ct = default)
    {
        var r = await _service.ObtenerAsync(soloConProblemas, ct);

        if (r.Descuadrados > 0)
            _logger.LogWarning(
                "Cuadre de alimento de engorde: {Descuadrados} de {Total} galpones NO cuadran " +
                "({Kg:N1} kg de diferencia absoluta).",
                r.Descuadrados, r.TotalGalpones, r.KgErrorAbsoluto);

        return Ok(r);
    }

    /// <summary>
    /// Lotes ya liquidados que congelaron su liquidación con alimento en el galpón (anomalía R2).
    /// <para>
    /// Al liquidar, el galpón tiene que quedar en cero: el procedimiento operativo es trasladar el
    /// sobrante. Esto no bloquea ninguna liquidación — pone a la vista lo que quedó, para que la
    /// operación lo traslade o deje constancia de que lo toma el ciclo siguiente.
    /// </para>
    /// </summary>
    /// <param name="soloAnomalias">
    /// Si es <c>true</c>, el detalle deja fuera los lotes cuyo sobrante sí se trasladó. El resumen se
    /// calcula siempre sobre el total.
    /// </param>
    [HttpGet("liquidados-con-alimento")]
    [ProducesResponseType(typeof(AnomaliaAlimentoLiquidadoDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<AnomaliaAlimentoLiquidadoDto>> GetLiquidadosConAlimento(
        [FromQuery] bool soloAnomalias = false,
        CancellationToken ct = default)
    {
        var r = await _service.ObtenerLiquidadosConAlimentoAsync(soloAnomalias, ct);

        if (r.SinRespaldoFisico > 0)
            _logger.LogWarning(
                "Alimento de engorde liquidado: {SinRespaldo} lote(s) reclaman {Kg:N1} kg que el galpón " +
                "ya no tiene (se los consumió otro ciclo).",
                r.SinRespaldoFisico, r.KgSinRespaldo);

        return Ok(r);
    }

    /// <summary>
    /// Cierra el descuadre de un galpón: el operador declara los kilos que realmente hay y el sistema
    /// escribe lo que falta de cada lado.
    ///
    /// <para>
    /// El pedido original era «editar el saldo desde esta pestaña». <b>El saldo no es un campo</b>:
    /// lo deriva <c>fn_seguimiento_diario_engorde</c>. Lo que se corrige es el insumo equivocado, y
    /// puede ser cualquiera de los dos: si sobra stock se escribe un <c>AjusteStock</c> (que la tabla
    /// diaria no ve, y está bien porque la tabla ya tenía razón); si sobra tabla se escribe un
    /// <c>AjusteCuadreTabla*</c> (que el stock no ve, por lo mismo del otro lado). Lo normal es que
    /// se mueva uno solo. Después del ajuste el descuadre es <b>cero por construcción</b>.
    /// </para>
    ///
    /// <para>
    /// Requiere el permiso <c>cuadrar_ingresos_traslados_seguimiento</c>: es una escritura que
    /// reescribe kilos, no una consulta.
    /// </para>
    /// </summary>
    [HttpPost("cuadrar-galpon")]
    [ProducesResponseType(typeof(CuadrarGalponAlimentoResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CuadrarGalponAlimentoResultDto>> CuadrarGalpon(
        [FromBody] CuadrarGalponAlimentoRequest req,
        CancellationToken ct = default)
    {
        if (!_current.Permissions.Contains(PermisoCuadrar, StringComparer.Ordinal))
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = MensajeSinPermisoCuadrar,
                error = MensajeSinPermisoCuadrar
            });

        try
        {
            var r = await _service.CuadrarGalponAsync(req, ct);

            _logger.LogWarning(
                "Cuadre de alimento aplicado a mano: {Granja}/{Galpon} lote {Lote} — {Resumen} " +
                "(descuadre {Antes:N1} → {Despues:N1} kg).",
                r.Granja, r.GalponId, r.LoteNombre, r.Resumen, r.DescuadreAntesKg, r.DescuadreDespuesKg);

            return Ok(r);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message, error = ex.Message });
        }
    }

    /// <summary>
    /// Permiso de escritura de la pestaña. Se reusa la key que ya existe para el mismo concepto
    /// —«este botón realiza el cuadre de saldo agregando y acomodando fechas, de ingresos traslados,
    /// salida y entrada»— en vez de inventar una segunda llave para la misma puerta.
    /// </summary>
    private const string PermisoCuadrar = "cuadrar_ingresos_traslados_seguimiento";

    private const string MensajeSinPermisoCuadrar =
        "No tiene permiso para cuadrar el alimento de un galpón. Puede consultar el cuadre, " +
        "pero no aplicar correcciones.";
}
