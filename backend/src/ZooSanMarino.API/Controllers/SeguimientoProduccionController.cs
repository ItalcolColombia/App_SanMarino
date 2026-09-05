using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

/// <summary>
/// Solo <c>GetFilterData</c> sigue viva — es la única acción que el front usa
/// (<c>GET /filter-data</c>, cascada Granja→Núcleo→Galpón→Lote). El resto del CRUD
/// (Create/GetAll/GetByLoteId/Update/Delete/Filter, respaldado antes por
/// <c>ISeguimientoProduccionService</c>) se eliminó: sin caller real confirmado (ni front,
/// ni app móvil, ni otro service), y el alta real de producción va por
/// <c>POST/PUT /api/Produccion/seguimiento</c> (<c>ProduccionController</c>).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class SeguimientoProduccionController : ControllerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SeguimientoProduccionController(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>Datos para filtros en cascada (Granja → Núcleo → Galpón → Lote) con lotes desde lote_postura_produccion.</summary>
    [HttpGet("filter-data")]
    [ProducesResponseType(typeof(SeguimientoProduccionFilterDataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SeguimientoProduccionFilterDataDto>> GetFilterData(CancellationToken ct = default)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var sp = scope.ServiceProvider;

        // Granjas/núcleos/galpones accesibles (cascada) desde el servicio de filtros existente.
        var filterDataSvc = sp.GetRequiredService<ILoteProduccionFilterDataService>();
        var baseData = await filterDataSvc.GetFilterDataAsync(ct);
        var farmIds = baseData.Farms.Select(f => f.Id).ToHashSet();

        // REQ-012d: construir el DTO REAL (LotePosturaProduccionFilterItemDto) proyectando
        // lote_postura_produccion CON FechaEncaset (+ aves iniciales/actuales y estado de cierre).
        // Antes el endpoint devolvía el item genérico SIN fechaEncaset → el front calculaba la edad
        // con base null (EDAD DÍAS=0, EDAD SEMANAS clamp fijo). El front lee `fechaEncaset` (camelCase).
        var lppSvc = sp.GetRequiredService<ILotePosturaProduccionService>();
        var lotes = (await lppSvc.GetAllAsync(ct))
            .Where(l => farmIds.Contains(l.GranjaId))
            .Select(l => new LotePosturaProduccionFilterItemDto(
                LotePosturaProduccionId: l.LotePosturaProduccionId,
                LoteNombre: l.LoteNombre,
                GranjaId: l.GranjaId,
                NucleoId: l.NucleoId,
                GalponId: l.GalponId,
                AvesHInicial: l.AvesHInicial ?? l.HembrasInicialesProd,
                AvesMInicial: l.AvesMInicial ?? l.MachosInicialesProd,
                AvesHActual: l.AvesHActual,
                AvesMActual: l.AvesMActual,
                EstadoCierre: l.EstadoCierre,
                FechaEncaset: l.FechaEncaset))
            .ToList();

        var data = new SeguimientoProduccionFilterDataDto(
            Farms: baseData.Farms,
            Nucleos: baseData.Nucleos,
            Galpones: baseData.Galpones,
            Lotes: lotes);

        return Ok(data);
    }
}



