// src/ZooSanMarino.Application/Interfaces/ISiloService.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Lista MAESTRA de silos de la empresa activa (1..100). De acá salen los silos que después se
/// asignan a cada granja (<see cref="IFarmSiloService"/>).
/// </summary>
public interface ISiloCatalogoService
{
    Task<IEnumerable<SiloCatalogoDto>> GetAllAsync(bool soloActivos = false, CancellationToken ct = default);
    Task<SiloCatalogoDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<SiloCatalogoDto> CreateAsync(CreateSiloCatalogoDto dto, CancellationToken ct = default);
    Task<SiloCatalogoDto?> UpdateAsync(int id, UpdateSiloCatalogoDto dto, CancellationToken ct = default);

    /// <summary>Baja lógica. Falla si el silo ya está asignado a alguna granja.</summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>Crea el rango completo de una (idempotente: omite los números que ya existen).</summary>
    Task<GenerarRangoSilosResultDto> GenerarRangoAsync(GenerarRangoSilosDto dto, CancellationToken ct = default);
}

/// <summary>
/// Silos y bodegas de una GRANJA: la ubicación real del inventario cuando
/// <c>Company.ManejaInventarioPorSilo</c> está activo.
/// </summary>
public interface IFarmSiloService
{
    /// <summary>Silos/bodegas de una granja (o de todas las granjas de la empresa si <c>granjaId</c> es null).</summary>
    Task<IEnumerable<FarmSiloDto>> GetAsync(int? granjaId = null, bool soloActivos = false, CancellationToken ct = default);

    Task<FarmSiloDto?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<FarmSiloDto> CreateAsync(CreateFarmSiloDto dto, CancellationToken ct = default);
    Task<FarmSiloDto?> UpdateAsync(int id, UpdateFarmSiloDto dto, CancellationToken ct = default);

    /// <summary>
    /// Baja lógica. Falla si el silo tiene stock, movimientos o asignaciones vivas: borrarlo
    /// dejaría saldos sin ubicación y el histórico mintiendo.
    /// </summary>
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);

    /// <summary>
    /// Fija de una vez qué silos del catálogo tiene la granja. Los que ya estaban y no vienen se dan
    /// de baja lógica (si no tienen movimientos); los nuevos se crean.
    /// </summary>
    Task<IEnumerable<FarmSiloDto>> AsignarDesdeCatalogoAsync(AsignarSilosGranjaDto dto, CancellationToken ct = default);
}

/// <summary>Qué silos alimentan a un galpón (N:M). Es navegación, no contención: el stock vive en el silo.</summary>
public interface IGalponSiloService
{
    Task<IEnumerable<GalponSiloDto>> GetAsync(int granjaId, string? nucleoId = null, string? galponId = null, CancellationToken ct = default);

    /// <summary>Silos de la granja que se pueden asignar a este galpón (todos los activos de la granja).</summary>
    Task<IEnumerable<FarmSiloDto>> GetDisponiblesAsync(int granjaId, CancellationToken ct = default);

    /// <summary>Reemplaza el conjunto de silos del galpón. Lista vacía = ninguno.</summary>
    Task<IEnumerable<GalponSiloDto>> AsignarAsync(int granjaId, string nucleoId, string galponId, AsignarSilosDto dto, CancellationToken ct = default);
}

/// <summary>De qué silos consume un LOTE (N:M). El seguimiento diario solo ofrece estos.</summary>
public interface ILoteSiloService
{
    Task<IEnumerable<LoteSiloDto>> GetByLoteAsync(int loteId, CancellationToken ct = default);

    /// <summary>
    /// Silos elegibles para el lote: los de su GALPÓN (<c>galpon_silos</c>). Si el galpón no tiene
    /// ninguno asignado, cae a todos los silos activos de la granja del lote — un lote sin opciones
    /// no podría registrar consumo y el usuario quedaría trabado sin saber por qué.
    /// </summary>
    Task<IEnumerable<FarmSiloDto>> GetDisponiblesAsync(int loteId, CancellationToken ct = default);

    /// <summary>Reemplaza el conjunto de silos del lote. Lista vacía = ninguno.</summary>
    Task<IEnumerable<LoteSiloDto>> AsignarAsync(int loteId, AsignarSilosDto dto, CancellationToken ct = default);
}
