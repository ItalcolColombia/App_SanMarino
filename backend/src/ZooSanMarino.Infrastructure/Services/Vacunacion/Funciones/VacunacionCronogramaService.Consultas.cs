// Vacunacion/Funciones/VacunacionCronogramaService.Consultas.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionCronogramaService
{
    /// <inheritdoc />
    public async Task<List<VacunacionCronogramaItemDto>> GetCronogramaLoteAsync(VacunacionCronogramaLoteRequest req, CancellationToken ct = default)
    {
        if (!LineasValidas.Contains(req.LineaProductiva))
            throw new InvalidOperationException($"lineaProductiva inválida: '{req.LineaProductiva}'.");

        // Resuelve el par Levante↔Producción (LotePosturaProduccion.LotePosturaLevanteId) para que el
        // cronograma sea "uno solo por toda la vida del lote", aunque Levante y Producción sean tablas
        // distintas. Engorde no tiene fases previas: solo su propio id.
        int? levanteId = null, produccionId = null, engordeId = null;
        switch (req.LineaProductiva)
        {
            case "Levante":
                levanteId = req.LoteId;
                produccionId = await _ctx.LotePosturaProduccion.AsNoTracking()
                    .Where(x => x.LotePosturaLevanteId == req.LoteId && x.CompanyId == _currentUser.CompanyId)
                    .Select(x => x.LotePosturaProduccionId)
                    .FirstOrDefaultAsync(ct);
                break;
            case "Produccion":
                produccionId = req.LoteId;
                levanteId = await _ctx.LotePosturaProduccion.AsNoTracking()
                    .Where(x => x.LotePosturaProduccionId == req.LoteId && x.CompanyId == _currentUser.CompanyId)
                    .Select(x => x.LotePosturaLevanteId)
                    .FirstOrDefaultAsync(ct);
                break;
            case "Engorde":
                engordeId = req.LoteId;
                break;
        }

        var items = await _ctx.VacunacionCronogramaItem
            .Include(x => x.ItemInventario)
            .Include(x => x.RegistroAplicacion)
            .Where(x => x.CompanyId == _currentUser.CompanyId)
            .Where(x =>
                (levanteId.HasValue && x.LotePosturaLevanteId == levanteId) ||
                (produccionId.HasValue && x.LotePosturaProduccionId == produccionId) ||
                (engordeId.HasValue && x.LoteAveEngordeId == engordeId))
            .ToListAsync(ct);

        if (items.Count == 0) return new List<VacunacionCronogramaItemDto>();

        var granjaIds = items.Select(x => x.GranjaId).Distinct().ToList();
        var granjaNombres = await _ctx.Farms.AsNoTracking()
            .Where(f => granjaIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Name, ct);

        (DateTime? FechaEncaset, string LoteNombre)? levanteInfo = levanteId.HasValue
            ? await _ctx.LotePosturaLevante.AsNoTracking()
                .Where(x => x.LotePosturaLevanteId == levanteId)
                .Select(x => new { x.FechaEncaset, x.LoteNombre })
                .Select(x => new ValueTuple<DateTime?, string>(x.FechaEncaset, x.LoteNombre))
                .FirstOrDefaultAsync(ct)
            : null;
        (DateTime? FechaEncaset, string LoteNombre)? produccionInfo = produccionId.HasValue
            ? await _ctx.LotePosturaProduccion.AsNoTracking()
                .Where(x => x.LotePosturaProduccionId == produccionId)
                .Select(x => new ValueTuple<DateTime?, string>(x.FechaEncaset, x.LoteNombre))
                .FirstOrDefaultAsync(ct)
            : null;
        (DateTime? FechaEncaset, string LoteNombre)? engordeInfo = engordeId.HasValue
            ? await _ctx.LoteAveEngorde.AsNoTracking()
                .Where(x => x.LoteAveEngordeId == engordeId)
                .Select(x => new ValueTuple<DateTime?, string>(x.FechaEncaset, x.LoteNombre))
                .FirstOrDefaultAsync(ct)
            : null;

        var result = new List<VacunacionCronogramaItemDto>(items.Count);
        foreach (var item in items)
        {
            var info = item.LineaProductiva switch
            {
                "Levante" => levanteInfo,
                "Produccion" => produccionInfo,
                "Engorde" => engordeInfo,
                _ => null
            } ?? (null, "");

            granjaNombres.TryGetValue(item.GranjaId, out var granjaNombre);
            result.Add(MapItem(item, info.FechaEncaset, info.LoteNombre, granjaNombre, item.ItemInventario?.Nombre ?? ""));
        }

        return result.OrderBy(x => x.FechaInicioFranja).ThenBy(x => x.Orden).ToList();
    }
}
