namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// Cohorte de aves recibidas por un lote (grupo que conserva la edad de su lote de origen).
/// Las edades vienen calculadas a la fecha de consulta (hoy) desde <see cref="FechaEncasetCohorte"/>.
/// </summary>
public record LoteCohorteDto(
    int Id,
    int? LoteOrigenId,
    string? LoteOrigenNombre,
    DateOnly FechaIngreso,
    DateOnly FechaEncasetCohorte,
    int EdadDias,
    int EdadSemanas,
    int CantidadHembras,
    int CantidadMachos,
    string? Observaciones
);

/// <summary>
/// Edades presentes en un lote: la cohorte PROPIA (implícita, por <c>lotes.fecha_encaset</c>) más
/// las cohortes recibidas por traslado, cada una con su edad actual.
/// </summary>
public record LoteCohortesDto(
    int LoteId,
    string LoteNombre,
    DateOnly? FechaEncasetPropia,
    int? EdadPropiaDias,
    int? EdadPropiaSemanas,
    IReadOnlyList<LoteCohorteDto> Cohortes
);
