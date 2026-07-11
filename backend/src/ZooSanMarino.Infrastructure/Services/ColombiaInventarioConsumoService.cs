using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Fase 3 (paso 2) — descuento/devolución de inventario Colombia en el MODELO B unificado
/// (inventario_gestion_stock / item_inventario_ecuador) a NIVEL GRANJA. Reemplaza a
/// FarmInventoryConsumoService (modelo A) para los lotes Colombia.
///
/// Id-mapping (dos caminos, ver <see cref="ResolverItemsBPorCatalogItemAsync"/>): (1) registros/ítems
/// históricos traen <c>catalogItemId</c> (modelo A) y se resuelven a <c>item_inventario_ecuador.id</c>
/// por CÓDIGO (mapeo del backfill A→B); (2) ítems creados directamente en el inventario nuevo (sin fila
/// espejo en catalogo_items) llegan como <c>itemInventarioEcuadorId</c> y se usan tal cual (pass-through
/// validado contra company/pais de Colombia). Batch: una query por camino resuelve todos los ids a la vez.
///
/// Descuenta contra el stock B nivel granja (nucleo/galpon NULL) mediante los métodos aditivos de
/// InventarioGestionService — sin exigir galpón (Ecuador/Panamá siguen con núcleo/galpón, intactos).
/// NO abre transacción propia: participa de la IDbContextTransaction externa del servicio de
/// seguimiento (bloqueo atómico), igual que FarmInventoryConsumoService.
/// </summary>
public class ColombiaInventarioConsumoService : IColombiaInventarioConsumoService
{
    /// <summary>Scope de Colombia en el modelo B unificado (backfill A→B).</summary>
    private const int CompanyColombia = 1;
    private const int PaisColombia = 1;

    private readonly ZooSanMarinoContext _db;
    private readonly IInventarioGestionService _gestion;

    public ColombiaInventarioConsumoService(ZooSanMarinoContext db, IInventarioGestionService gestion)
    {
        _db = db;
        _gestion = gestion;
    }

    /// <summary>
    /// Resuelve en BATCH un id "mixto" → item_inventario_ecuador.id (modelo B), scope company 1/pais 1
    /// de Colombia. Dos caminos, en orden:
    ///   1) catalogItemId (modelo A histórico) → codigo → item_inventario_ecuador por código
    ///      (mapeo del backfill A→B).
    ///   2) Ids que NO existen en catalogo_items (nunca tuvieron fila ahí): se asume que ya son un
    ///      item_inventario_ecuador.id directo — ítems creados directamente en el inventario nuevo
    ///      (p.ej. desde Config > Ítems de inventario), sin espejo en catalogo_items. Se valida que
    ///      el id exista y pertenezca a Colombia antes de aceptarlo (pass-through controlado).
    /// Un id que SÍ existe en catalogo_items pero no tiene equivalente por código NO cae al camino 2
    /// (evita interpretar mal un id de catalogo_items como si fuera de item_inventario_ecuador).
    /// Los ids que no resuelven por ninguno de los dos caminos NO figuran en el diccionario.
    /// </summary>
    private async Task<Dictionary<int, int>> ResolverItemsBPorCatalogItemAsync(IEnumerable<int> catalogItemIds, CancellationToken ct)
    {
        var ids = catalogItemIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return new Dictionary<int, int>();

        // catalogItemId → codigo (modelo A, catálogo Colombia).
        var codigosPorCatalogItem = await _db.CatalogItems.AsNoTracking()
            .Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Codigo })
            .ToListAsync(ct);

        var codigos = codigosPorCatalogItem.Select(c => c.Codigo).Distinct().ToArray();

        // codigo → item_inventario_ecuador.id (modelo B, Colombia = company 1/pais 1).
        var itemsB = codigos.Length == 0
            ? new List<(int Id, string Codigo)>()
            : (await _db.ItemInventario.AsNoTracking()
                .Where(e => e.CompanyId == CompanyColombia && e.PaisId == PaisColombia && codigos.Contains(e.Codigo))
                .Select(e => new { e.Id, e.Codigo })
                .ToListAsync(ct))
              .Select(e => (e.Id, e.Codigo)).ToList();

        // Camino 2: ids que ni siquiera existen en catalogo_items → posible item_inventario_ecuador.id directo.
        var idsEnCatalogoItems = codigosPorCatalogItem.Select(c => c.Id).ToHashSet();
        var candidatosDirectos = ids.Where(id => !idsEnCatalogoItems.Contains(id)).ToArray();
        var itemsBDirectos = candidatosDirectos.Length == 0
            ? new List<int>()
            : await _db.ItemInventario.AsNoTracking()
                .Where(e => e.CompanyId == CompanyColombia && e.PaisId == PaisColombia && candidatosDirectos.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync(ct);

        return ColombiaInventarioIdResolutionCalculos.Resolver(
            codigosPorCatalogItem.Select(c => (c.Id, c.Codigo)).ToList(),
            itemsB,
            itemsBDirectos);
    }

    /// <summary>Resuelve el ítem B de un solo catalogItemId (Colombia). Lanza si no existe/no tiene mapeo.</summary>
    private async Task<int> ResolverItemBObligatorioAsync(int catalogItemId, CancellationToken ct)
    {
        var map = await ResolverItemsBPorCatalogItemAsync(new[] { catalogItemId }, ct);
        if (map.TryGetValue(catalogItemId, out var itemBId)) return itemBId;
        throw new InvalidOperationException(
            $"El producto (catalogItemId={catalogItemId}) no tiene equivalente en el inventario unificado de Colombia (item_inventario_ecuador). No se puede descontar.");
    }

    public async Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, CancellationToken ct = default)
    {
        var positivos = byCatalogItemId.Where(kv => kv.Value > 0).ToArray();
        if (positivos.Length == 0) return;

        var map = await ResolverItemsBPorCatalogItemAsync(positivos.Select(kv => kv.Key), ct);

        foreach (var kv in positivos)
        {
            if (!map.TryGetValue(kv.Key, out var itemBId))
                throw new InvalidOperationException(
                    $"El producto (catalogItemId={kv.Key}) no tiene equivalente en el inventario unificado de Colombia (item_inventario_ecuador). No se puede descontar.");

            var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(e => e.Id == itemBId, ct);
            var disponible = await _db.InventarioGestionStock.AsNoTracking()
                .Where(x => x.FarmId == farmId && x.ItemInventarioEcuadorId == itemBId && x.NucleoId == null && x.GalponId == null)
                .Select(x => (decimal?)x.Quantity)
                .FirstOrDefaultAsync(ct) ?? 0m;

            if (disponible < kv.Value)
                throw new InvalidOperationException(
                    $"Stock insuficiente para '{item?.Codigo} - {item?.Nombre}' (granja {farmId}): disponible {disponible:0.###} kg, requerido {kv.Value:0.###} kg.");
        }
    }

    public async Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, string reference, CancellationToken ct = default)
    {
        foreach (var kv in byCatalogItemId)
        {
            if (kv.Value <= 0) continue;
            var itemBId = await ResolverItemBObligatorioAsync(kv.Key, ct);
            await _gestion.RegistrarConsumoNivelGranjaAsync(
                new InventarioGestionConsumoRequest(farmId, null, null, itemBId, kv.Value, "kg", reference, null), ct);
        }
    }

    public async Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<int, decimal> byCatalogItemId, string reference, string? reason, CancellationToken ct = default)
    {
        foreach (var kv in byCatalogItemId)
        {
            if (kv.Value <= 0) continue;
            var itemBId = await ResolverItemBObligatorioAsync(kv.Key, ct);
            await _gestion.RegistrarIngresoNivelGranjaAsync(
                new InventarioGestionIngresoRequest(farmId, null, null, itemBId, kv.Value, "kg", reference, reason), ct);
        }
    }

    public async Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<int, decimal> oldByCatalogItemId, IReadOnlyDictionary<int, decimal> newByCatalogItemId, string reference, CancellationToken ct = default)
    {
        var allItemIds = new HashSet<int>(oldByCatalogItemId.Keys);
        foreach (var k in newByCatalogItemId.Keys) allItemIds.Add(k);

        foreach (var catalogItemId in allItemIds)
        {
            var newQty = newByCatalogItemId.TryGetValue(catalogItemId, out var n) ? n : 0m;
            var oldQty = oldByCatalogItemId.TryGetValue(catalogItemId, out var o) ? o : 0m;
            var diff = newQty - oldQty;
            if (diff == 0) continue;

            var itemBId = await ResolverItemBObligatorioAsync(catalogItemId, ct);
            if (diff > 0)
                await _gestion.RegistrarConsumoNivelGranjaAsync(
                    new InventarioGestionConsumoRequest(farmId, null, null, itemBId, diff, "kg", reference + " (ajuste)", null), ct);
            else
                await _gestion.RegistrarIngresoNivelGranjaAsync(
                    new InventarioGestionIngresoRequest(farmId, null, null, itemBId, -diff, "kg", reference + " (devolución)", "Devolución por ajuste de seguimiento"), ct);
        }
    }
}
