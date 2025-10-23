// src/ZooSanMarino.Application/Interfaces/IFarmInventoryMovementService.cs
using ZooSanMarino.Application.DTOs;
using CommonDtos = ZooSanMarino.Application.DTOs.Common;

namespace ZooSanMarino.Application.Interfaces;

public interface IFarmInventoryMovementService
{
    Task<InventoryMovementDto> PostEntryAsync   (int farmId, InventoryEntryRequest  req, CancellationToken ct = default);
    Task<InventoryMovementDto> PostExitAsync    (int farmId, InventoryExitRequest   req, CancellationToken ct = default);
    Task<(InventoryMovementDto Out, InventoryMovementDto In)> PostTransferAsync(int fromFarmId, InventoryTransferRequest req, CancellationToken ct = default);
    Task<InventoryMovementDto> PostAdjustAsync  (int farmId, InventoryAdjustRequest req, CancellationToken ct = default);

    Task<CommonDtos.PagedResult<InventoryMovementDto>> GetPagedAsync(int farmId, MovementQuery q, CancellationToken ct = default);
    Task<InventoryMovementDto?> GetByIdAsync(int farmId, int movementId, CancellationToken ct = default);
}
