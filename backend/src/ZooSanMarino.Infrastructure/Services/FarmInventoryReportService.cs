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
    private readonly ICurrentUser? _current;
    
    public FarmInventoryReportService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db;
        _current = current;
    }

    public async Task<IEnumerable<KardexItemDto>> GetKardexAsync(int farmId, int catalogItemId, DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        // Validar que la granja pertenezca a la empresa del usuario
        var farm = await _db.Set<Farm>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == farmId, ct);
        
        if (farm == null) return Enumerable.Empty<KardexItemDto>();
        
        // Filtrar por empresa del usuario actual
        if (_current != null && _current.CompanyId > 0)
        {
            if (farm.CompanyId != _current.CompanyId)
            {
                return Enumerable.Empty<KardexItemDto>();
            }
        }

        // Filtros de empresa/país del usuario actual (la fn los ignora cuando llegan null/<=0).
        int? companyFilter = (_current != null && _current.CompanyId > 0) ? _current.CompanyId : null;
        int? paisFilter = (_current != null && _current.CompanyId > 0
                           && _current.PaisId.HasValue && _current.PaisId.Value > 0)
                          ? _current.PaisId.Value : null;

        // Delegar el cálculo del saldo a la BD (fn_kardex_farm_inventory, window function).
        // Reemplaza el foreach en memoria; misma aritmética/orden (saldo por created_at, id,
        // con el mismo switch de signo por movement_type). Equivalencia golden verificada.
        var rows = await _db.Database
            .SqlQueryRaw<KardexBdRow>(
                "SELECT * FROM fn_kardex_farm_inventory({0}::int, {1}::int, {2}::int, {3}::int, {4}::timestamptz, {5}::timestamptz)",
                farmId,
                catalogItemId,
                (object?)companyFilter ?? DBNull.Value,
                (object?)paisFilter ?? DBNull.Value,
                (object?)from ?? DBNull.Value,
                (object?)to ?? DBNull.Value)
            .ToListAsync(ct);

        return rows.Select(r => new KardexItemDto
        {
            Fecha      = r.Fecha,
            Tipo       = r.Tipo,
            Referencia = r.Referencia,
            Cantidad   = r.Cantidad,
            Unidad     = r.Unidad,
            Saldo      = r.Saldo,
            Motivo     = r.Motivo
        }).ToList();
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
