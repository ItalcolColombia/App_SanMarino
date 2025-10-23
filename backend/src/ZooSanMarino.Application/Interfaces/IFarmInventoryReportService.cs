  // src/ZooSanMarino.Application/Interfaces/IFarmInventoryReportService.cs
    using ZooSanMarino.Application.DTOs;

    namespace ZooSanMarino.Application.Interfaces;

    public interface IFarmInventoryReportService
    {
        Task<IEnumerable<KardexItemDto>> GetKardexAsync(int farmId, int catalogItemId, DateTime? from, DateTime? to, CancellationToken ct = default);
        Task ApplyStockCountAsync(int farmId, StockCountRequest req, CancellationToken ct = default);
    }
