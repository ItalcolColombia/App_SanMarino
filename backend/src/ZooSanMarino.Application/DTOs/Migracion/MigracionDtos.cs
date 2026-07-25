// src/ZooSanMarino.Application/DTOs/Migracion/MigracionDtos.cs
namespace ZooSanMarino.Application.DTOs.Migracion;

/// <summary>Un error o advertencia puntual detectado en una fila/celda de la plantilla.</summary>
/// <param name="Severidad">"Error" (bloquea all-or-nothing) o "Advertencia" (informativa, no bloquea).</param>
public record MigracionErrorDto(int Fila, string Columna, string? Valor, string Mensaje, string Severidad = "Error");

/// <summary>
/// Contexto de selección jerárquica para la migración. La EMPRESA NO viaja aquí:
/// se resuelve del header de empresa activa (X-Active-Company[-Id]) validado por el middleware.
/// </summary>
/// <param name="ReproductoraId">Solo Seguimiento Reproductora Engorde: id del lote reproductora
/// elegido en pantalla (opcional). Con valor, las filas sin columna "Reproductora" cargan a esa;
/// sin valor, cada fila debe identificar su reproductora en el Excel.</param>
public record MigracionContextoDto(
    int? GranjaId,
    string? NucleoId,
    string? GalponId,
    int? LoteId,
    int? ReproductoraId = null
);

/// <summary>Resultado de una corrida de validación (dry-run) o importación.</summary>
/// <param name="FilasOmitidas">Filas no procesadas por idempotencia (ya existían en BD); no son error.</param>
/// <param name="DuracionMs">Duración total de la corrida en milisegundos.</param>
/// <param name="TotalErrores">Total real de errores/advertencias detectados, antes de capar <c>Errores</c> a <c>MaxErroresReportados</c>.</param>
public record MigracionResultDto(
    string Tipo,
    bool Exito,
    int FilasTotales,
    int FilasProcesadas,
    int FilasError,
    string Estado,          // Validado | Procesado | ProcesadoParcial | ConErrores | Fallido
    bool FueDryRun,
    IReadOnlyList<MigracionErrorDto> Errores,
    int FilasOmitidas = 0,
    long DuracionMs = 0,
    int TotalErrores = 0
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

/// <summary>
/// Lote reproductora de un lote engorde, para el selector opcional del módulo (endpoint /reproductoras).
/// <paramref name="Cargados"/>/<paramref name="Confirmados"/> = días de seguimiento ya registrados/confirmados.
/// </summary>
public record ReproductoraElegibleDto(
    int Id,
    string ReproductoraId,
    string? Codigo,
    string Nombre,
    DateTime? FechaEncasetamiento,
    int Cargados,
    int Confirmados
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
    int UsuarioId,
    int FilasOmitidas,
    long? DuracionMs,
    bool FueDryRun,
    bool TieneErrores
);

/// <summary>Página del historial de auditoría de migraciones (endpoint /historial paginado).</summary>
public record MigracionHistorialPagedDto(
    IReadOnlyList<MigracionHistorialDto> Items,
    int Total,
    int Page,
    int PageSize
);
