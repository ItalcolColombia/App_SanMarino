// src/ZooSanMarino.Infrastructure/Services/FarmInventoryReportService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Domain.Enums;
using ZooSanMarino.Infrastructure.Persistence;

public class FarmInventoryReportService : IFarmInventoryReportService
{
    private readonly ZooSanMarinoContext _db;
    public FarmInventoryReportService(ZooSanMarinoContext db) => _db = db;

    public async Task<IEnumerable<KardexItemDto>> GetKardexAsync(int farmId, int catalogItemId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var q = _db.FarmInventoryMovements
            .AsNoTracking()
            .Where(m => m.FarmId == farmId && m.CatalogItemId == catalogItemId);
        if (from.HasValue) q = q.Where(m => m.CreatedAt >= from.Value);
        if (to.HasValue)   q = q.Where(m => m.CreatedAt <= to.Value);

        var raw = await q.OrderBy(m => m.CreatedAt).ToListAsync(ct);

        var saldo = 0m;
        var list = new List<KardexItemDto>();
        foreach (var m in raw)
        {
            var sign = m.MovementType switch
            {
                InventoryMovementType.Entry       => +1m,
                InventoryMovementType.TransferIn  => +1m,
                InventoryMovementType.Exit        => -1m,
                InventoryMovementType.TransferOut => -1m,
                InventoryMovementType.Adjust      => (decimal)(m.Quantity >= 0 ? +1 : -1), // según tu modelo
                _ => 0m
            };
            var delta = sign * m.Quantity;
            saldo += delta;

            list.Add(new KardexItemDto {
                Fecha = m.CreatedAt.UtcDateTime,
                Tipo  = m.MovementType.ToString(),
                Referencia = m.Reference,
                Cantidad   = delta,
                Unidad     = m.Unit,
                Saldo      = saldo,
                Motivo     = m.Reason
            });
        }
        return list;
    }

    public async Task ApplyStockCountAsync(int farmId, StockCountRequest req, CancellationToken ct = default)
    {
        // Ajuste por diferencia: (conteo - saldo actual) → movimiento Adjust
        foreach (var it in req.Items)
        {
            var inv = await _db.FarmProductInventory.FirstOrDefaultAsync(x => x.FarmId == farmId && x.CatalogItemId == it.CatalogItemId, ct);
            var current = inv?.Quantity ?? 0m;
            var diff = it.Conteo - current;
            if (diff == 0) continue;

            var adjReq = new InventoryAdjustRequest { CatalogItemId = it.CatalogItemId, Quantity = diff, Unit = inv?.Unit ?? "kg", Reason = "Conteo físico" };
            // Reusar el servicio de movimientos si lo inyectas, o hacer el ajuste aquí.
            if (inv is null)
            {
                inv = new FarmProductInventory { FarmId = farmId, CatalogItemId = it.CatalogItemId, Quantity = 0, Unit = adjReq.Unit ?? "kg", Active = true, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
                _db.FarmProductInventory.Add(inv);
            }
            inv.Quantity += diff;
            inv.UpdatedAt = DateTimeOffset.UtcNow;

            _db.FarmInventoryMovements.Add(new FarmInventoryMovement {
                FarmId = farmId, CatalogItemId = it.CatalogItemId, Quantity = Math.Abs(diff),
                MovementType = InventoryMovementType.Adjust, Unit = inv.Unit, Reason = "Conteo físico",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);
    }
}
