namespace ZooSanMarino.Application.DTOs.FlujosValidacion;

/// <summary>Una firma de la línea de tiempo de una instancia.</summary>
public record AccionInstanciaDto(
    string Accion,
    int PasoOrden,
    string PasoNombre,
    Guid UserId,
    string UserNombre,
    string? Comentario,
    DateTime CreatedAt
);

public record InstanciaPasoDto(
    int Orden,
    string Nombre,
    string Estado,
    IReadOnlyList<string> CandidatosLegibles
);

/// <summary>
/// Estado completo de la instancia de un recurso para la sesión actual: qué etapa está pendiente,
/// quién puede actuar y qué acciones habilita el backend (la UI no calcula autorización sola).
/// </summary>
public record EstadoInstanciaFlujoDto(
    Guid InstanciaId,
    string ProcesoKey,
    string RecursoId,
    string Estado,
    int PasoActualOrden,
    int TotalPasos,
    string PasoActualNombre,
    DateTime? Vencimiento,
    bool PuedeAprobar,
    bool PuedeDevolver,
    bool PuedeCorregirYReenviar,
    bool PuedeEliminar,
    IReadOnlyList<InstanciaPasoDto> Pasos,
    IReadOnlyList<AccionInstanciaDto> Historial
);

public record AprobarInstanciaCommand(Guid InstanciaId);

public record DevolverInstanciaCommand(Guid InstanciaId, string Motivo);

public record CorregirYReenviarInstanciaCommand(Guid InstanciaId);

public record EliminarInstanciaCommand(Guid InstanciaId);

/// <param name="Kg">Kilos de alimento aplicados, solo si la acción finalizó la instancia.</param>
/// <param name="Aves">Aves descontadas, solo si la acción finalizó la instancia.</param>
public record ResultadoAccionInstanciaDto(
    Guid InstanciaId,
    string EstadoResultante,
    bool Finalizada,
    decimal? Kg = null,
    int? Aves = null
);

/// <summary>Fila de la bandeja "Mis pendientes".</summary>
public record PendienteFlujoDto(
    Guid InstanciaId,
    string ProcesoKey,
    string RecursoId,
    int PasoActualOrden,
    string PasoActualNombre,
    string FarmNombre,
    string? NucleoId,
    string? GalponId,
    string? LoteRef,
    DateOnly FechaSeguimiento,
    DateTime? Vencimiento
);

/// <summary>Tarjeta roja persistente del Home.</summary>
public record NovedadValidacionDto(
    long NovedadId,
    Guid InstanciaId,
    string ProcesoKey,
    string CompanyNombre,
    string FarmNombre,
    string? NucleoId,
    string? GalponId,
    string? LoteRef,
    DateOnly FechaSeguimiento,
    int EtapaQueDevolvioOrden,
    string EtapaQueDevolvioNombre,
    string DevueltoPorNombre,
    string Motivo,
    DateTime CreatedAt,
    DateTime? ReadAt
);
