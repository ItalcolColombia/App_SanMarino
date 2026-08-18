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

    public CuadreAlimentoEngordeController(
        ICuadreAlimentoEngordeService service,
        ILogger<CuadreAlimentoEngordeController> logger)
    {
        _service = service;
        _logger = logger;
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
}
