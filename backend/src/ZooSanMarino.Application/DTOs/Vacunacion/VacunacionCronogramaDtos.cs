// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionCronogramaDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Ítem del cronograma de vacunación, con la franja calculada y el registro de aplicación (si existe).</summary>
public record VacunacionCronogramaItemDto(
    int Id,
    string LineaProductiva,
    int LoteId,               // PK específico de la línea (LotePosturaLevanteId/LotePosturaProduccionId/LoteAveEngordeId)
    string LoteNombre,
    int GranjaId,
    string? GranjaNombre,
    string? NucleoId,
    string? GalponId,
    int ItemInventarioId,
    string ItemInventarioNombre,
    string UnidadObjetivo,
    int? ValorObjetivo,
    DateTime? FechaObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    DateTime FechaInicioFranja,
    DateTime FechaFinFranja,
    int Orden,
    bool Activo,
    string? Notas,
    VacunacionRegistroAplicacionDto? Registro
);

public record VacunacionRegistroAplicacionDto(
    int Id,
    string Estado,
    DateTime? FechaAplicacion,
    int? DiasDesviacion,
    bool Incumplido,
    string? MotivoDescripcion,
    int UsuarioRegistraId,
    string? UsuarioRegistraNombre,
    int? AplicadoPorUserId,
    string? AplicadoPorUserNombre,
    string? AplicadoPorNombreLibre
);

public record VacunacionCronogramaItemCreateRequest(
    string LineaProductiva,
    int LoteId,
    int ItemInventarioId,
    string UnidadObjetivo,
    int? ValorObjetivo,
    DateTime? FechaObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    int Orden = 0,
    string? Notas = null
);

public record VacunacionCronogramaItemUpdateRequest(
    int ItemInventarioId,
    string UnidadObjetivo,
    int? ValorObjetivo,
    DateTime? FechaObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    int Orden,
    bool Activo,
    string? Notas
);

/// <summary>Cronograma "de toda la vida del lote": si se pide por un lote Producción, incluye también
/// los ítems programados cuando estaba en Levante (vía LotePosturaProduccion.LotePosturaLevanteId), y viceversa.</summary>
public record VacunacionCronogramaLoteRequest(string LineaProductiva, int LoteId);
