// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionFilterDataDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Datos para armar los combos en cascada del formulario: granjas asignadas al usuario,
/// lotes de las 3 líneas dentro de esas granjas, y vacunas del catálogo (ItemInventario tipo "vacuna").</summary>
public record VacunacionFilterDataDto(
    List<FarmDto> Granjas,
    List<VacunacionLoteOpcionDto> Lotes,
    List<VacunacionVacunaOpcionDto> Vacunas
);

public record VacunacionLoteOpcionDto(
    int LoteId,
    string LineaProductiva,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId,
    DateTime? FechaEncaset,
    string? EstadoCierre
);

public record VacunacionVacunaOpcionDto(
    int Id,
    string Codigo,
    string Nombre,
    string Unidad
);
