namespace ZooSanMarino.Application.DTOs.Traslados;

/// <summary>
/// Cohorte de aves recibidas por un lote (grupo que conserva la edad de su lote de origen).
/// Las edades vienen calculadas a la fecha de consulta (hoy) desde <see cref="FechaEncasetCohorte"/>.
/// </summary>
public record LoteCohorteDto(
    int Id,
    int? LoteOrigenId,
    string? LoteOrigenNombre,
    /// <summary>
    /// Ubicación del lote origen CONGELADA al momento del traslado ("Granja · Núcleo · Galpón").
    /// Null en las cohortes anteriores a que se guardara este dato.
    /// </summary>
    string? UbicacionOrigen,
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
    /// <summary>
    /// Aves propias estimadas = saldo actual − recibidas por traslado. Permite cuadrar
    /// <i>propias + recibidas = saldo</i>. ⚠️ Aproximación: las bajas se registran por LOTE, no por cohorte,
    /// así que la mortalidad posterior al ingreso se descuenta implícitamente de las propias.
    /// </summary>
    int? HembrasPropias,
    int? MachosPropias,
    IReadOnlyList<LoteCohorteDto> Cohortes
);
