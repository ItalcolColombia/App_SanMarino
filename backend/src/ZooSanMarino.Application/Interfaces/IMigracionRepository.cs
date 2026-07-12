// src/ZooSanMarino.Application/Interfaces/IMigracionRepository.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Interfaces;

public interface IMigracionRepository
{
    /// <summary>Persiste el registro de auditoría de una corrida de migración.</summary>
    Task<MigracionMasiva> RegistrarAsync(MigracionMasiva registro, CancellationToken ct = default);

    /// <summary>Historial de corridas de la empresa (opcionalmente filtrado por tipo), más recientes primero.</summary>
    Task<IReadOnlyList<MigracionMasiva>> GetHistorialAsync(int companyId, string? tipo, CancellationToken ct = default);
}
