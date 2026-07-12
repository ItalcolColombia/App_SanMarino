// src/ZooSanMarino.Application/Interfaces/IMigracionService.cs
using Microsoft.AspNetCore.Http;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Orquestador del módulo de Migraciones Masivas. Encapsula toda la lógica de migración
/// (plantilla → validar → importar) reutilizando las reglas de negocio de los módulos existentes.
/// </summary>
public interface IMigracionService
{
    /// <summary>Catálogo de tipos de migración soportados.</summary>
    IReadOnlyList<TipoMigracionInfoDto> GetTipos();

    /// <summary>Historial de auditoría de la empresa activa (opcionalmente por tipo).</summary>
    Task<IReadOnlyList<MigracionHistorialDto>> GetHistorialAsync(string? tipo, CancellationToken ct = default);

    /// <summary>Lotes elegibles para migración de históricos según las reglas de fase.</summary>
    Task<IReadOnlyList<LoteElegibleDto>> GetElegiblesAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>Genera la plantilla .xlsx del tipo indicado, con datos relacionados y validaciones.</summary>
    Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>Valida el archivo (dry-run): no inserta nada, devuelve el reporte de errores.</summary>
    Task<MigracionResultDto> ValidarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>Importa el archivo: valida y, solo si no hay errores, inserta de forma masiva.</summary>
    Task<MigracionResultDto> ImportarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default);
}
