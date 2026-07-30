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
}
