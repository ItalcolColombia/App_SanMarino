using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Domain.Enums;
using ZooSanMarino.Infrastructure.Persistence;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;


namespace ZooSanMarino.Infrastructure.Services;

public class FarmInventoryMovementService : IFarmInventoryMovementService
{
    private readonly ZooSanMarinoContext _db;
    private readonly ICurrentUser? _current;

    public FarmInventoryMovementService(ZooSanMarinoContext db, ICurrentUser? current = null)
    {
        _db = db; _current = current;
    }

    private async Task<int> ResolveItemIdAsync(int? catalogItemId, string? codigo, CancellationToken ct)
    {
        if (catalogItemId.HasValue)
        {
            var exists = await _db.CatalogItems.AnyAsync(c => c.Id == catalogItemId.Value, ct);
            if (!exists) throw new InvalidOperationException("El producto no existe.");
            return catalogItemId.Value;
        }
        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var item = await _db.CatalogItems.FirstOrDefaultAsync(c => c.Codigo == codigo.Trim(), ct);
            if (item == null) throw new InvalidOperationException("El producto (codigo) no existe.");
            return item.Id;
        }
        throw new InvalidOperationException("Debe especificar CatalogItemId o Codigo.");
    }

    private async Task<string?> GetItemTypeAsync(int itemId, CancellationToken ct)
    {
        var item = await _db.CatalogItems
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == itemId, ct);
        return item?.ItemType;
    }

    private static void EnsurePositive(decimal qty)
    {
        if (qty <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
    }

    private async Task<(int CompanyId, int PaisId)> GetFarmCompanyAndPaisAsync(int farmId, CancellationToken ct)
    {
        var farm = await _db.Set<Farm>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == farmId, ct);
        
        if (farm == null) throw new InvalidOperationException($"La granja {farmId} no existe.");
        
        // Obtener el país desde el departamento
        var departamento = await _db.Set<Departamento>()
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.DepartamentoId == farm.DepartamentoId, ct);
        
        if (departamento == null) throw new InvalidOperationException($"No se encontró el departamento {farm.DepartamentoId} de la granja {farmId}.");
        
        return (farm.CompanyId, departamento.PaisId);
    }

    private async Task<FarmProductInventory> GetOrCreateInventoryAsync(int farmId, int itemId, string unit, CancellationToken ct)
    {
        // Consulta simple usando solo los campos básicos que siempre existen
        // Esto evita JOINs automáticos y problemas con columnas que pueden no existir
        var invId = await _db.FarmProductInventory
            .AsNoTracking()
            .Where(x => x.FarmId == farmId && x.CatalogItemId == itemId)
            .Select(x => x.Id)
            .FirstOrDefaultAsync(ct);
        
        if (invId > 0)
        {
            // Si existe, obtenerlo rastreado para poder actualizarlo
            var tracked = await _db.FarmProductInventory.FindAsync(new object[] { invId }, ct);
            if (tracked != null) return tracked;
        }

        // Obtener company_id y pais_id de la granja
        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(farmId, ct);

        var inv = new FarmProductInventory
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            CompanyId = companyId,
            PaisId = paisId,
            Quantity = 0,
            Unit = string.IsNullOrWhiteSpace(unit) ? "kg" : unit.Trim(),
            Metadata = JsonDocument.Parse("{}"),
            Active = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        _db.FarmProductInventory.Add(inv);
        await _db.SaveChangesAsync(ct);
        return inv;
    }

    private async Task<InventoryMovementDto> MapMovementAsync(FarmInventoryMovement m, CancellationToken ct)
    {
        var item = await _db.CatalogItems.AsNoTracking().FirstAsync(x => x.Id == m.CatalogItemId, ct);
        return new InventoryMovementDto
        {
            Id = m.Id,
            FarmId = m.FarmId,
            CatalogItemId = m.CatalogItemId,
            ItemType = m.ItemType ?? item.ItemType,  // Usar el del movimiento o del catálogo
            Codigo = item.Codigo,
            Nombre = item.Nombre,
            Quantity = m.Quantity,
            MovementType = m.MovementType.ToString(),
            Unit = m.Unit,
            Reference = m.Reference,
            Reason = m.Reason,
            Origin = m.Origin,
            Destination = m.Destination,
            TransferGroupId = m.TransferGroupId,
            // Campos específicos para movimiento de alimento
            DocumentoOrigen = m.DocumentoOrigen,
            TipoEntrada = m.TipoEntrada,
            GalponDestinoId = m.GalponDestinoId,
            FechaMovimiento = m.FechaMovimiento,
            Metadata = m.Metadata,
            ResponsibleUserId = m.ResponsibleUserId,
            CreatedAt = m.CreatedAt
        };
    }

    public async Task<InventoryMovementDto> PostEntryAsync(int farmId, InventoryEntryRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Obtener company_id y pais_id de la granja y validar que pertenezca a la empresa del usuario
        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(farmId, ct);
        
        // Validar que la granja pertenezca a la empresa del usuario
        if (_current != null && _current.CompanyId > 0)
        {
            if (companyId != _current.CompanyId)
            {
                throw new UnauthorizedAccessException("La granja no pertenece a su empresa.");
            }
        }

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        inv.Quantity += req.Quantity;
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        // Obtener el tipo de item del catálogo (si no se envió en el request)
        var itemType = req.ItemType;
        if (string.IsNullOrWhiteSpace(itemType))
        {
            itemType = await GetItemTypeAsync(itemId, ct);
        }

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            ItemType = itemType,  // Tipo de item del catálogo
            CompanyId = companyId,
            PaisId = paisId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.Entry,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Origin = req.Origin,  // Origen para entrada
            Destination = null,  // No aplica para entrada
            // Campos específicos para movimiento de alimento (opcionales - solo se asignan si vienen en el request)
            DocumentoOrigen = req.DocumentoOrigen,  // null para movimientos normales
            TipoEntrada = req.TipoEntrada,          // null para movimientos normales
            GalponDestinoId = req.GalponDestinoId,  // null para movimientos normales
            FechaMovimiento = req.FechaMovimiento,   // null para movimientos normales (se usa CreatedAt)
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapMovementAsync(mov, ct);
    }

    public async Task<InventoryMovementDto> PostExitAsync(int farmId, InventoryExitRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Obtener company_id y pais_id de la granja y validar que pertenezca a la empresa del usuario
        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(farmId, ct);
        
        // Validar que la granja pertenezca a la empresa del usuario
        if (_current != null && _current.CompanyId > 0)
        {
            if (companyId != _current.CompanyId)
            {
                throw new UnauthorizedAccessException("La granja no pertenece a su empresa.");
            }
        }

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        if (inv.Quantity < req.Quantity) throw new InvalidOperationException("Stock insuficiente para la salida.");
        inv.Quantity -= req.Quantity;
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        // Obtener el tipo de item del catálogo (si no se envió en el request)
        var itemType = req.ItemType;
        if (string.IsNullOrWhiteSpace(itemType))
        {
            itemType = await GetItemTypeAsync(itemId, ct);
        }

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            ItemType = itemType,  // Tipo de item del catálogo
            CompanyId = companyId,
            PaisId = paisId,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.Exit,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Origin = null,  // No aplica para salida
            Destination = req.Destination,  // Destino para salida
            // Campos específicos para movimiento de alimento (opcionales - null para movimientos normales)
            DocumentoOrigen = req.DocumentoOrigen,  // null para movimientos normales
            TipoEntrada = req.TipoEntrada,          // null para movimientos normales
            GalponDestinoId = req.GalponDestinoId,  // null para movimientos normales
            FechaMovimiento = req.FechaMovimiento,   // null para movimientos normales (se usa CreatedAt)
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return await MapMovementAsync(mov, ct);
    }

    public async Task<(InventoryMovementDto Out, InventoryMovementDto In)> PostTransferAsync(int fromFarmId, InventoryTransferRequest req, CancellationToken ct = default)
    {
        EnsurePositive(req.Quantity);
        if (req.ToFarmId == fromFarmId) throw new InvalidOperationException("La granja destino debe ser diferente.");
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;
        var group = Guid.NewGuid();

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Obtener company_id y pais_id de ambas granjas y validar que pertenezcan a la empresa del usuario
        var (companyIdFrom, paisIdFrom) = await GetFarmCompanyAndPaisAsync(fromFarmId, ct);
        var (companyIdTo, paisIdTo) = await GetFarmCompanyAndPaisAsync(req.ToFarmId, ct);
        
        // Validar que ambas granjas pertenezcan a la empresa del usuario
        if (_current != null && _current.CompanyId > 0)
        {
            if (companyIdFrom != _current.CompanyId || companyIdTo != _current.CompanyId)
            {
                throw new UnauthorizedAccessException("Las granjas deben pertenecer a su empresa.");
            }
        }

        // Obtener el tipo de item del catálogo (si no se envió en el request)
        var itemType = req.ItemType;
        if (string.IsNullOrWhiteSpace(itemType))
        {
            itemType = await GetItemTypeAsync(itemId, ct);
        }

        // OUT (origen)
        var invFrom = await GetOrCreateInventoryAsync(fromFarmId, itemId, unit, ct);
        if (invFrom.Quantity < req.Quantity) throw new InvalidOperationException("Stock insuficiente en la granja origen.");
        invFrom.Quantity -= req.Quantity;
        invFrom.Unit = unit;
        invFrom.UpdatedAt = DateTimeOffset.UtcNow;

        var movOut = new FarmInventoryMovement
        {
            FarmId = fromFarmId,
            CatalogItemId = itemId,
            ItemType = itemType,  // Tipo de item del catálogo
            CompanyId = companyIdFrom,
            PaisId = paisIdFrom,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.TransferOut,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Origin = null,  // No aplica para traslado
            Destination = null,  // No aplica para traslado
            TransferGroupId = group,
            // Campos específicos para movimiento de alimento
            DocumentoOrigen = req.DocumentoOrigen,
            TipoEntrada = req.TipoEntrada,
            GalponDestinoId = req.GalponDestinoId,
            FechaMovimiento = req.FechaMovimiento ?? DateTimeOffset.UtcNow,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(movOut);

        // IN (destino)
        var invTo = await GetOrCreateInventoryAsync(req.ToFarmId, itemId, unit, ct);
        invTo.Quantity += req.Quantity;
        invTo.Unit = unit;
        invTo.UpdatedAt = DateTimeOffset.UtcNow;

        var movIn = new FarmInventoryMovement
        {
            FarmId = req.ToFarmId,
            CatalogItemId = itemId,
            ItemType = itemType,  // Tipo de item del catálogo
            CompanyId = companyIdTo,
            PaisId = paisIdTo,
            Quantity = req.Quantity,
            MovementType = InventoryMovementType.TransferIn,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Origin = null,  // No aplica para traslado
            Destination = null,  // No aplica para traslado
            TransferGroupId = group,
            // Campos específicos para movimiento de alimento
            DocumentoOrigen = req.DocumentoOrigen,
            TipoEntrada = req.TipoEntrada,
            GalponDestinoId = req.GalponDestinoId,
            FechaMovimiento = req.FechaMovimiento ?? DateTimeOffset.UtcNow,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(movIn);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return (await MapMovementAsync(movOut, ct), await MapMovementAsync(movIn, ct));
    }


    public async Task<InventoryMovementDto> PostAdjustAsync(int farmId, InventoryAdjustRequest req, CancellationToken ct = default)
    {
        var itemId = await ResolveItemIdAsync(req.CatalogItemId, req.Codigo, ct);
        var unit = string.IsNullOrWhiteSpace(req.Unit) ? "kg" : req.Unit.Trim();
        string? userId = _current != null ? _current.UserId.ToString() : null;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Obtener company_id y pais_id de la granja
        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(farmId, ct);

        var inv = await GetOrCreateInventoryAsync(farmId, itemId, unit, ct);
        inv.Quantity += req.Quantity; // puede ser + o −
        if (inv.Quantity < 0) throw new InvalidOperationException("El saldo no puede ser negativo.");
        inv.Unit = unit;
        inv.UpdatedAt = DateTimeOffset.UtcNow;

        // Obtener el tipo de item del catálogo (si no se envió en el request)
        var itemType = req.ItemType;
        if (string.IsNullOrWhiteSpace(itemType))
        {
            itemType = await GetItemTypeAsync(itemId, ct);
        }

        var mov = new FarmInventoryMovement
        {
            FarmId = farmId,
            CatalogItemId = itemId,
            ItemType = itemType,  // Tipo de item del catálogo
            CompanyId = companyId,
            PaisId = paisId,
            Quantity = Math.Abs(req.Quantity),
            MovementType = req.Quantity >= 0 ? InventoryMovementType.Adjust : InventoryMovementType.Adjust,
            Unit = unit,
            Reference = req.Reference,
            Reason = req.Reason,
            Origin = null,  // No aplica para ajuste
            Destination = null,  // No aplica para ajuste
            // Campos específicos para movimiento de alimento (opcionales en ajuste)
            DocumentoOrigen = req.DocumentoOrigen,
            TipoEntrada = req.TipoEntrada,
            GalponDestinoId = req.GalponDestinoId,
            FechaMovimiento = req.FechaMovimiento ?? DateTimeOffset.UtcNow,
            Metadata = req.Metadata ?? JsonDocument.Parse("{}"),
            ResponsibleUserId = userId
        };
        _db.FarmInventoryMovements.Add(mov);

        await _db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await MapMovementAsync(mov, ct);
    }

  public async Task<CommonDtos.PagedResult<InventoryMovementDto>> GetPagedAsync(
        int farmId, MovementQuery q, CancellationToken ct = default)
    {
        var query = _db.FarmInventoryMovements.AsNoTracking().Where(m => m.FarmId == farmId);
        
        // Filtrar por empresa y país del usuario actual (seguridad adicional)
        // También evita leer filas con company_id NULL (error "Column 'company_id' is null")
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(m => m.CompanyId == _current.CompanyId);
        }
        if (_current != null && _current.PaisId > 0)
        {
            query = query.Where(m => m.PaisId == _current.PaisId);
        }

        if (q.From.HasValue)               query = query.Where(m => m.CreatedAt >= q.From.Value);
        if (q.To.HasValue)                 query = query.Where(m => m.CreatedAt <= q.To.Value);
        if (q.CatalogItemId.HasValue)      query = query.Where(m => m.CatalogItemId == q.CatalogItemId.Value);
        if (!string.IsNullOrWhiteSpace(q.Codigo))
                                          query = query.Where(m => m.CatalogItem.Codigo == q.Codigo);
        if (!string.IsNullOrWhiteSpace(q.Type) &&
            Enum.TryParse<Domain.Enums.InventoryMovementType>(q.Type, out var mt))
                                          query = query.Where(m => m.MovementType == mt);

        var total = await query.LongCountAsync(ct);
        var page  = q.Page <= 0 ? 1 : q.Page;
        var size  = (q.PageSize <= 0 || q.PageSize > 200) ? 20 : q.PageSize;

        var list = await query.OrderByDescending(m => m.CreatedAt)
                              .Skip((page - 1) * size)
                              .Take(size)
                              .Select(m => new InventoryMovementDto
                              {
                                  Id = m.Id,
                                  FarmId = m.FarmId,
                                  CatalogItemId = m.CatalogItemId,
                                  ItemType = m.ItemType ?? m.CatalogItem.ItemType,  // Usar el del movimiento o del catálogo
                                  Codigo = m.CatalogItem.Codigo,
                                  Nombre = m.CatalogItem.Nombre,
                                  Quantity = m.Quantity,
                                  MovementType = m.MovementType.ToString(),
                                  Unit = m.Unit,
                                  Reference = m.Reference,
                                  Reason = m.Reason,
                                  Origin = m.Origin,
                                  Destination = m.Destination,
                                  TransferGroupId = m.TransferGroupId,
                                  // Campos específicos para movimiento de alimento
                                  DocumentoOrigen = m.DocumentoOrigen,
                                  TipoEntrada = m.TipoEntrada,
                                  GalponDestinoId = m.GalponDestinoId,
                                  FechaMovimiento = m.FechaMovimiento,
                                  Metadata = m.Metadata,
                                  ResponsibleUserId = m.ResponsibleUserId,
                                  CreatedAt = m.CreatedAt
                              })
                              .ToListAsync(ct);

        return new CommonDtos.PagedResult<InventoryMovementDto>
        {
            Items = list,
            Total = total,
            Page = page,
            PageSize = size
        };
    }
    public async Task<InventoryMovementDto?> GetByIdAsync(int farmId, int movementId, CancellationToken ct = default)
    {
        var query = _db.FarmInventoryMovements
            .AsNoTracking()
            .Include(x => x.CatalogItem)
            .Where(x => x.Id == movementId && x.FarmId == farmId);
        
        // Filtrar por empresa y país del usuario actual (seguridad adicional)
        if (_current != null && _current.CompanyId > 0)
        {
            query = query.Where(x => x.CompanyId == _current.CompanyId);
        }
        if (_current != null && _current.PaisId > 0)
        {
            query = query.Where(x => x.PaisId == _current.PaisId);
        }
        
        var m = await query.FirstOrDefaultAsync(ct);
        if (m is null) return null;

        return new InventoryMovementDto {
            Id = m.Id, FarmId = m.FarmId, CatalogItemId = m.CatalogItemId,
            ItemType = m.ItemType ?? m.CatalogItem.ItemType,  // Usar el del movimiento o del catálogo
            Codigo = m.CatalogItem.Codigo, Nombre = m.CatalogItem.Nombre,
            Quantity = m.Quantity, MovementType = m.MovementType.ToString(),
            Unit = m.Unit, Reference = m.Reference, Reason = m.Reason,
            Origin = m.Origin, Destination = m.Destination,
            TransferGroupId = m.TransferGroupId,
            // Campos específicos para movimiento de alimento
            DocumentoOrigen = m.DocumentoOrigen,
            TipoEntrada = m.TipoEntrada,
            GalponDestinoId = m.GalponDestinoId,
            FechaMovimiento = m.FechaMovimiento,
            Metadata = m.Metadata,
            ResponsibleUserId = m.ResponsibleUserId, CreatedAt = m.CreatedAt
        };
    }

    }
