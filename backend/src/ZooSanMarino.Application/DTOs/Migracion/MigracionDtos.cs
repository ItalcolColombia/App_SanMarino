// src/ZooSanMarino.Application/DTOs/Migracion/MigracionDtos.cs
namespace ZooSanMarino.Application.DTOs.Migracion;

/// <summary>Un error puntual detectado en una fila/celda de la plantilla.</summary>
public record MigracionErrorDto(int Fila, string Columna, string? Valor, string Mensaje);

/// <summary>
/// Contexto de selección jerárquica para la migración. La EMPRESA NO viaja aquí:
/// se resuelve del header de empresa activa (X-Active-Company[-Id]) validado por el middleware.
/// </summary>
public record MigracionContextoDto(
    int? GranjaId,
    string? NucleoId,
    string? GalponId,
    int? LoteId
);

/// <summary>Resultado de una corrida de validación (dry-run) o importación.</summary>
public record MigracionResultDto(
    string Tipo,
    bool Exito,
    int FilasTotales,
    int FilasProcesadas,
    int FilasError,
    string Estado,          // Validado | Procesado | ConErrores | Fallido
    bool FueDryRun,
    IReadOnlyList<MigracionErrorDto> Errores
);

/// <summary>Un lote elegible para migración de históricos (Levante/Producción/Ventas/Movimientos).</summary>
public record LoteElegibleDto(
    int LoteId,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId,
    string Fase,
    string? Estado
);

/// <summary>Item del historial de auditoría de migraciones (endpoint /historial).</summary>
public record MigracionHistorialDto(
    int Id,
    string Tipo,
    string NombreArchivo,
    int FilasTotales,
    int FilasProcesadas,
    int FilasError,
    string Estado,
    DateTime FechaProceso,
    int UsuarioId
);
