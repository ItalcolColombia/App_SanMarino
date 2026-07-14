// src/ZooSanMarino.Application/Interfaces/IMigracionRepository.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Interfaces;

public interface IMigracionRepository
{
    /// <summary>Persiste el registro de auditoría de una corrida de migración.</summary>
    Task<MigracionMasiva> RegistrarAsync(MigracionMasiva registro, CancellationToken ct = default);

    /// <summary>
    /// Historial paginado de corridas de la empresa (opcionalmente filtrado por tipo), más recientes
    /// primero. <paramref name="incluirValidaciones"/> = false excluye las corridas dry-run.
    /// <paramref name="pageSize"/> se acota a 1..100 (default 20); <paramref name="page"/> a ≥ 1.
    /// </summary>
    Task<(IReadOnlyList<MigracionMasiva> Items, int Total)> GetHistorialAsync(
        int companyId, string? tipo, int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default);

    /// <summary>Una corrida puntual por id, solo si pertenece a la empresa indicada. Null si no existe/no es de la empresa.</summary>
    Task<MigracionMasiva?> GetPorIdAsync(int id, int companyId, CancellationToken ct = default);
}
