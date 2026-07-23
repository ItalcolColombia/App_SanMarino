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

    /// <summary>
    /// Historial paginado de auditoría de la empresa activa (opcionalmente por tipo).
    /// <paramref name="incluirValidaciones"/> = false excluye las corridas dry-run (solo importaciones reales).
    /// </summary>
    Task<MigracionHistorialPagedDto> GetHistorialAsync(string? tipo, int page, int pageSize, bool incluirValidaciones, CancellationToken ct = default);

    /// <summary>Errores/advertencias de una corrida puntual del historial. Null si no existe o no es de la empresa activa.</summary>
    Task<IReadOnlyList<MigracionErrorDto>?> GetErroresAsync(int id, CancellationToken ct = default);

    /// <summary>Lotes elegibles para migración de históricos según las reglas de fase.</summary>
    Task<IReadOnlyList<LoteElegibleDto>> GetElegiblesAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>Lotes reproductora del lote engorde indicado (selector opcional de Seguimiento Reproductora Engorde).</summary>
    Task<IReadOnlyList<ReproductoraElegibleDto>> GetReproductorasElegiblesAsync(int loteId, CancellationToken ct = default);

    /// <summary>Genera la plantilla .xlsx del tipo indicado, con datos relacionados y validaciones.</summary>
    Task<(byte[] Contenido, string NombreArchivo)> GenerarPlantillaAsync(TipoMigracion tipo, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>Valida el archivo (dry-run): no inserta nada, devuelve el reporte de errores completo.</summary>
    Task<MigracionResultDto> ValidarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, CancellationToken ct = default);

    /// <summary>
    /// Importa el archivo: valida y, si no hay errores, inserta de forma masiva (comportamiento
    /// all-or-nothing por defecto). Si <paramref name="permitirParcial"/> es true y hay al menos una
    /// fila válida junto a filas con error, inserta SOLO las válidas (Estado "ProcesadoParcial").
    /// </summary>
    Task<MigracionResultDto> ImportarAsync(TipoMigracion tipo, IFormFile file, MigracionContextoDto contexto, bool permitirParcial, CancellationToken ct = default);
}
