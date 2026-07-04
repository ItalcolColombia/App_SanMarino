using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Domain.Enums;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Fase 2 (S3) — descuento/devolución automáticos del inventario Colombia (modelo A).
/// Decrementa/incrementa <c>farm_product_inventory.Quantity</c> por (farm_id, catalog_item_id)
/// e inserta movimientos <c>ConsumoSeguimiento</c>/<c>DevolucionSeguimiento</c> en
/// <c>farm_inventory_movements</c>. NO abre transacción propia: usa el mismo DbContext scoped
/// del request y participa de la <c>IDbContextTransaction</c> externa que abre el servicio de
/// seguimiento (levante/producción). NO llama a SaveChanges de forma independiente para que el
/// commit/rollback lo controle el orquestador externo — salvo el alta de un stock inexistente,
/// que sí requiere persistir para obtener la entidad rastreada (idéntico al patrón del servicio
/// manual GetOrCreateInventoryAsync); ese SaveChanges queda dentro de la tx externa.
/// </summary>
public class FarmInventoryConsumoService : IFarmInventoryConsumoService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;

    public FarmInventoryConsumoService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db;
        _current = current;
    }

    public async Task ValidarStockConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, CancellationToken ct = default)
    {
        foreach (var kv in byItemId)
        {
            if (kv.Value <= 0) continue;
            var itemId = kv.Key;
            var qty = kv.Value;

            var item = await _db.CatalogItems.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == itemId, ct);
            if (item == null)
                throw new InvalidOperationException($"El producto (catalogItemId={itemId}) no existe en el catálogo. No se puede descontar el inventario.");

            var disponible = await _db.FarmProductInventory.AsNoTracking()
                .Where(x => x.FarmId == farmId && x.CatalogItemId == itemId)
                .Select(x => (decimal?)x.Quantity)
                .FirstOrDefaultAsync(ct) ?? 0m;

            if (disponible < qty)
                throw new InvalidOperationException(
                    $"Stock insuficiente para '{item.Codigo} - {item.Nombre}' (catalogItemId={itemId}): disponible {disponible:0.###} kg, requerido {qty:0.###} kg.");
        }
    }

    public async Task AplicarConsumoAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, string reference, CancellationToken ct = default)
    {
        foreach (var kv in byItemId)
        {
            if (kv.Value <= 0) continue;
            await MoverAsync(farmId, kv.Key, kv.Value, InventoryMovementType.ConsumoSeguimiento, reference, null, ct);
        }
    }

    public async Task AplicarDevolucionAsync(int farmId, IReadOnlyDictionary<int, decimal> byItemId, string reference, string? reason, CancellationToken ct = default)
    {
        foreach (var kv in byItemId)
        {
            if (kv.Value <= 0) continue;
            await MoverAsync(farmId, kv.Key, kv.Value, InventoryMovementType.DevolucionSeguimiento, reference, reason, ct);
        }
    }

    public async Task AplicarDiffAsync(int farmId, IReadOnlyDictionary<int, decimal> oldByItemId, IReadOnlyDictionary<int, decimal> newByItemId, string reference, CancellationToken ct = default)
    {
        var allItemIds = new HashSet<int>(oldByItemId.Keys);
        foreach (var k in newByItemId.Keys) allItemIds.Add(k);

        foreach (var itemId in allItemIds)
        {
            var newQty = newByItemId.TryGetValue(itemId, out var n) ? n : 0m;
            var oldQty = oldByItemId.TryGetValue(itemId, out var o) ? o : 0m;
            var diff = newQty - oldQty;
            if (diff > 0)
                await MoverAsync(farmId, itemId, diff, InventoryMovementType.ConsumoSeguimiento, reference + " (ajuste)", null, ct);
            else if (diff < 0)
                await MoverAsync(farmId, itemId, -diff, InventoryMovementType.DevolucionSeguimiento, reference + " (devolución)", "Devolución por ajuste de seguimiento", ct);
        }
    }

    /// <summary>
    /// Aplica UN movimiento (consumo o devolución) sobre el stock modelo A. Consumo valida stock
    /// y resta; devolución suma. Inserta el <c>FarmInventoryMovement</c> con el tipo dado. NO abre
    /// transacción; el SaveChanges del alta de stock inexistente participa de la tx externa.
    /// </summary>
    private async Task MoverAsync(int farmId, int itemId, decimal qty, InventoryMovementType tipo, string reference, string? reason, CancellationToken ct)
    {
        var item = await _db.CatalogItems.AsNoTracking().FirstOrDefaultAsync(c => c.Id == itemId, ct);
        if (item == null)
            throw new InvalidOperationException($"El producto (catalogItemId={itemId}) no existe en el catálogo.");

        var inv = await GetOrCreateInventoryTrackedAsync(farmId, itemId, item.CompanyId, item.PaisId, ct);

        var esConsumo = tipo == InventoryMovementType.ConsumoSeguimiento;
        if (esConsumo)
        {
            // Mismo criterio que FarmInventoryMovementService.PostExitAsync (inv.Quantity < qty → throw),
            // pero SIN tx propia. La validación previa (ValidarStockConsumoAsync) ya debió cubrir esto;
            // esta es una defensa dentro de la tx externa.
            if (inv.Quantity < qty)
                throw new InvalidOperationException(
                    $"Stock insuficiente para '{item.Codigo} - {item.Nombre}' (catalogItemId={itemId}): disponible {inv.Quantity:0.###}, requerido {qty:0.###}.");
            inv.Quantity -= qty;
        }
        else
        {
            inv.Quantity += qty; // devolución repone
        }
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            ItemType = item.ItemType,
            CompanyId = item.CompanyId,
            PaisId = item.PaisId,
            Quantity = qty,               // positiva (el signo lo da el tipo en el kardex)
            MovementType = tipo,
            Unit = "kg",
            Reference = reference,
            Reason = reason,
            Metadata = JsonDocument.Parse("{}"),
            ResponsibleUserId = _current != null ? _current.UserId.ToString() : null
        };
        _db.FarmInventoryMovements.Add(mov);
    }

    /// <summary>
    /// Obtiene el stock rastreado (farm_product_inventory) para (farmId, itemId). Si no existe,
    /// lo crea con Quantity=0 y lo persiste (SaveChanges dentro de la tx externa) para obtener la
    /// entidad rastreada. Idéntico patrón a FarmInventoryMovementService.GetOrCreateInventoryAsync.
    /// </summary>
    private async Task<FarmProductInventory> GetOrCreateInventoryTrackedAsync(int farmId, int itemId, int companyId, int paisId, CancellationToken ct)
    {
        var existing = await _db.FarmProductInventory
            .FirstOrDefaultAsync(x => x.FarmId == farmId && x.CatalogItemId == itemId, ct);
        if (existing != null) return existing;

        var inv = new FarmProductInventory
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            CompanyId = companyId,
            PaisId = paisId,
            Quantity = 0,
            Unit = "kg",
            Metadata = JsonDocument.Parse("{}"),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.FarmProductInventory.Add(inv);
        await _db.SaveChangesAsync(ct); // participa de la tx externa
        return inv;
    }
}
