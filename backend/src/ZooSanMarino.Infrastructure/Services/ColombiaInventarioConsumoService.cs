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
/// Id-mapping (dos caminos, EXPLÍCITOS en <see cref="ItemConsumoKey"/> — ya no se adivina la
/// tabla de origen por existencia en catalogo_items, heurístico que fallaba por colisión de
/// rangos de ids): (1) claves con EsItemInventario=false traen <c>catalogItemId</c> (modelo A)
/// y se resuelven a <c>item_inventario_ecuador.id</c> por CÓDIGO (mapeo del backfill A→B);
/// (2) claves con EsItemInventario=true traen <c>itemInventarioEcuadorId</c> directo (ítems
/// creados en el inventario nuevo, sin fila espejo) y se usan tal cual, validados contra la
/// empresa efectiva. Batch: una query por camino resuelve todos los ids a la vez.
///
/// EMPRESA EFECTIVA = <c>farms.company_id</c> de la granja del lote (multi-empresa: Sanmarino,
/// Demo, etc. — antes estaba hardcodeada company 1 y rechazaba los ítems de otras empresas).
/// País fijo Colombia: este servicio solo se invoca para lotes cuyo país resolvió Colombia
/// (InventarioConsumoGate), y el filtro evita descuentos cross-país dentro de una empresa.
///
/// Descuenta contra el stock B nivel granja (nucleo/galpon NULL) mediante los métodos aditivos de
/// InventarioGestionService — sin exigir galpón (Ecuador/Panamá siguen con núcleo/galpón, intactos).
/// NO abre transacción propia: participa de la IDbContextTransaction externa del servicio de
/// seguimiento (bloqueo atómico), igual que FarmInventoryConsumoService.
/// </summary>
public class ColombiaInventarioConsumoService : IColombiaInventarioConsumoService
{
    /// <summary>Id de país Colombia (tabla paises) — scope del modelo B unificado para este servicio.</summary>
    private const int PaisColombia = 1;

    private readonly ZooSanMarinoContext _db;
    private readonly IInventarioGestionService _gestion;

    public ColombiaInventarioConsumoService(ZooSanMarinoContext db, IInventarioGestionService gestion)
    {
        _db = db;
        _gestion = gestion;
    }

    /// <summary>
    /// Empresa efectiva del descuento = empresa dueña de la granja del lote. Es la misma empresa
    /// bajo la que se listan los ítems del dropdown y se registró el stock B de esa granja.
    /// </summary>
    private async Task<int> ResolverCompanyIdDeGranjaAsync(int farmId, CancellationToken ct)
    {
        var companyId = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException($"No se pudo resolver la empresa de la granja {farmId} para descontar inventario.");
        return companyId.Value;
    }

    /// <summary>
    /// Resuelve en BATCH las claves de ítem → item_inventario_ecuador.id (modelo B), scope
    /// empresa efectiva de la granja + país Colombia. Camino por clave (sin adivinar):
    ///   1) EsItemInventario=false → catalogItemId → codigo → item_inventario_ecuador por código.
    ///   2) EsItemInventario=true → id directo de item_inventario_ecuador; se valida que exista y
    ///      pertenezca a la empresa efectiva antes de aceptarlo (pass-through controlado).
    /// Las claves que no resuelven por su camino NO figuran en el diccionario.
    /// </summary>
    private async Task<Dictionary<ItemConsumoKey, int>> ResolverItemsBAsync(int companyId, IEnumerable<ItemConsumoKey> keys, CancellationToken ct)
    {
        var distintas = keys.Where(k => k.Id > 0).Distinct().ToArray();
        if (distintas.Length == 0) return new Dictionary<ItemConsumoKey, int>();

        var catalogIds = distintas.Where(k => !k.EsItemInventario).Select(k => k.Id).ToArray();
        var directIds = distintas.Where(k => k.EsItemInventario).Select(k => k.Id).ToArray();

        // Camino 1: catalogItemId → codigo (modelo A, catálogo Colombia).
        var codigosPorCatalogItem = catalogIds.Length == 0
            ? new List<(int Id, string Codigo)>()
            : (await _db.CatalogItems.AsNoTracking()
                .Where(c => catalogIds.Contains(c.Id))
                .Select(c => new { c.Id, c.Codigo })
                .ToListAsync(ct))
              .Select(c => (c.Id, c.Codigo)).ToList();

        var codigos = codigosPorCatalogItem.Select(c => c.Codigo).Distinct().ToArray();

        // codigo → item_inventario_ecuador.id (modelo B, empresa efectiva + Colombia).
        var itemsB = codigos.Length == 0
            ? new List<(int Id, string Codigo)>()
            : (await _db.ItemInventario.AsNoTracking()
                .Where(e => e.CompanyId == companyId && e.PaisId == PaisColombia && codigos.Contains(e.Codigo))
                .Select(e => new { e.Id, e.Codigo })
                .ToListAsync(ct))
              .Select(e => (e.Id, e.Codigo)).ToList();

        // Camino 2: ids directos de item_inventario_ecuador (empresa efectiva + Colombia).
        var itemsBDirectos = directIds.Length == 0
            ? new List<int>()
            : await _db.ItemInventario.AsNoTracking()
                .Where(e => e.CompanyId == companyId && e.PaisId == PaisColombia && directIds.Contains(e.Id))
                .Select(e => e.Id)
                .ToListAsync(ct);

        return ColombiaInventarioIdResolutionCalculos.Resolver(codigosPorCatalogItem, itemsB, itemsBDirectos);
    }

    /// <summary>Mensaje de error de resolución, específico del camino de la clave.</summary>
    private static InvalidOperationException ErrorItemSinEquivalente(ItemConsumoKey key, int companyId) =>
        key.EsItemInventario
            ? new InvalidOperationException(
                $"El ítem de inventario (id={key.Id}) no existe o no pertenece a la empresa de la granja (empresa {companyId}, país Colombia). No se puede descontar.")
            : new InvalidOperationException(
                $"El producto (catalogItemId={key.Id}) no tiene equivalente en el inventario unificado de Colombia (item_inventario_ecuador). No se puede descontar.");

    public async Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, CancellationToken ct = default)
    {
        var positivos = byItem.Where(kv => kv.Value > 0).ToArray();
        if (positivos.Length == 0) return;

        var companyId = await ResolverCompanyIdDeGranjaAsync(farmId, ct);
        var map = await ResolverItemsBAsync(companyId, positivos.Select(kv => kv.Key), ct);

        foreach (var kv in positivos)
        {
            if (!map.TryGetValue(kv.Key, out var itemBId))
                throw ErrorItemSinEquivalente(kv.Key, companyId);

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

    public async Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, string reference, CancellationToken ct = default)
    {
        var positivos = byItem.Where(kv => kv.Value > 0).ToArray();
        if (positivos.Length == 0) return;

        var companyId = await ResolverCompanyIdDeGranjaAsync(farmId, ct);
        var map = await ResolverItemsBAsync(companyId, positivos.Select(kv => kv.Key), ct);

        foreach (var kv in positivos)
        {
            if (!map.TryGetValue(kv.Key, out var itemBId))
                throw ErrorItemSinEquivalente(kv.Key, companyId);
            await _gestion.RegistrarConsumoNivelGranjaAsync(
                new InventarioGestionConsumoRequest(farmId, null, null, itemBId, kv.Value, "kg", reference, null), ct);
        }
    }

    public async Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> byItem, string reference, string? reason, CancellationToken ct = default)
    {
        var positivos = byItem.Where(kv => kv.Value > 0).ToArray();
        if (positivos.Length == 0) return;

        var companyId = await ResolverCompanyIdDeGranjaAsync(farmId, ct);
        var map = await ResolverItemsBAsync(companyId, positivos.Select(kv => kv.Key), ct);

        foreach (var kv in positivos)
        {
            if (!map.TryGetValue(kv.Key, out var itemBId))
                throw ErrorItemSinEquivalente(kv.Key, companyId);
            await _gestion.RegistrarIngresoNivelGranjaAsync(
                new InventarioGestionIngresoRequest(farmId, null, null, itemBId, kv.Value, "kg", reference, reason), ct);
        }
    }

    public async Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<ItemConsumoKey, decimal> oldByItem, IReadOnlyDictionary<ItemConsumoKey, decimal> newByItem, string reference, CancellationToken ct = default)
    {
        var allKeys = new HashSet<ItemConsumoKey>(oldByItem.Keys);
        foreach (var k in newByItem.Keys) allKeys.Add(k);

        var conDiff = allKeys
            .Select(k => (Key: k, Diff: newByItem.GetValueOrDefault(k) - oldByItem.GetValueOrDefault(k)))
            .Where(x => x.Diff != 0)
            .ToArray();
        if (conDiff.Length == 0) return;

        var companyId = await ResolverCompanyIdDeGranjaAsync(farmId, ct);
        var map = await ResolverItemsBAsync(companyId, conDiff.Select(x => x.Key), ct);

        foreach (var (key, diff) in conDiff)
        {
            if (!map.TryGetValue(key, out var itemBId))
                throw ErrorItemSinEquivalente(key, companyId);

            if (diff > 0)
                await _gestion.RegistrarConsumoNivelGranjaAsync(
                    new InventarioGestionConsumoRequest(farmId, null, null, itemBId, diff, "kg", reference + " (ajuste)", null), ct);
            else
                await _gestion.RegistrarIngresoNivelGranjaAsync(
                    new InventarioGestionIngresoRequest(farmId, null, null, itemBId, -diff, "kg", reference + " (devolución)", "Devolución por ajuste de seguimiento"), ct);
        }
    }
}
