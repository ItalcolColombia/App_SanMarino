// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionPlantillaDtos.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Plantilla de vacunación en la lista: la cabecera y cuántas vacunas tiene programadas.
/// Los ítems no viajan acá — se piden con el detalle — para que la lista de una empresa con muchos
/// planes no arrastre cientos de filas que nadie está mirando.
/// </summary>
public record VacunacionPlantillaDto(
    int Id,
    string Nombre,
    string LineaProductiva,
    string? Raza,
    DateTime? VigenteDesde,
    bool Activa,
    string? Notas,
    int CantidadItems
);

/// <summary>Plantilla con sus ítems, ordenados como se van a materializar.</summary>
public record VacunacionPlantillaDetalleDto(
    int Id,
    string Nombre,
    string LineaProductiva,
    string? Raza,
    DateTime? VigenteDesde,
    bool Activa,
    string? Notas,
    List<VacunacionPlantillaItemDto> Items
);

public record VacunacionPlantillaItemDto(
    int Id,
    int PlantillaId,
    int ItemInventarioId,
    string ItemInventarioNombre,
    string UnidadObjetivo,
    int ValorObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    int Orden,
    string? Notas
);

public record VacunacionPlantillaCreateRequest(
    string Nombre,
    string LineaProductiva,
    string? Raza,
    DateTime? VigenteDesde,
    string? Notas
);

public record VacunacionPlantillaUpdateRequest(
    string Nombre,
    string LineaProductiva,
    string? Raza,
    DateTime? VigenteDesde,
    bool Activa,
    string? Notas
);

public record VacunacionPlantillaItemCreateRequest(
    int ItemInventarioId,
    string UnidadObjetivo,
    int ValorObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    int Orden = 0,
    string? Notas = null
);

public record VacunacionPlantillaItemUpdateRequest(
    int ItemInventarioId,
    string UnidadObjetivo,
    int ValorObjetivo,
    int RangoDiasAntes,
    int RangoDiasDespues,
    int Orden,
    string? Notas
);

/// <summary>
/// Qué plantilla le tocaría a un lote, y <b>por qué</b>.
///
/// <para>
/// Es sólo lectura: no escribe una fila en el cronograma. Existe para que la resolución se pueda
/// auditar <b>antes</b> de que W2 materialice nada — un plan sanitario que aparece solo en el
/// cronograma de un lote y nadie sabe de dónde salió es exactamente lo que este módulo vino a evitar.
/// </para>
/// </summary>
/// <param name="Motivo">
/// Explicación en castellano de la elección, o de por qué no hay ninguna (sin plantillas para la
/// línea, lote sin raza, todas con vigencia posterior al encaset…).
/// </param>
public record VacunacionPlantillaEfectivaDto(
    string LineaProductiva,
    int LoteId,
    string? LoteNombre,
    string? Raza,
    DateTime? FechaEncaset,
    VacunacionPlantillaDetalleDto? Plantilla,
    string Motivo
);
