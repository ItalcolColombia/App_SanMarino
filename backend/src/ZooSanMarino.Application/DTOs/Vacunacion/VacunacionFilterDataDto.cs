// src/ZooSanMarino.Application/DTOs/Vacunacion/VacunacionFilterDataDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Datos para armar los combos en cascada del formulario, resueltos en UN solo round trip
/// por fn_vacunacion_filter_data (jsonb): granjas asignadas al usuario (proyección lite), lotes de
/// las 3 líneas dentro de esas granjas, vacunas del catálogo (ItemInventario tipo "vacuna") y
/// usuarios activos de la empresa (para el select "aplicado por usuario del sistema").</summary>
public record VacunacionFilterDataDto(
    List<VacunacionGranjaOpcionDto> Granjas,
    List<VacunacionLoteOpcionDto> Lotes,
    List<VacunacionVacunaOpcionDto> Vacunas,
    List<VacunacionUsuarioOpcionDto> Usuarios
);

/// <summary>Granja lite para combos: el front solo consume id/companyId/name (antes viajaba el FarmDto completo).</summary>
public record VacunacionGranjaOpcionDto(
    int Id,
    int CompanyId,
    string Name
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

/// <summary>Usuario del sistema para "aplicado por": Id = cédula parseada a int
/// (el UserId entero del sistema ES la cédula — mismo mapeo que Tickets).</summary>
public record VacunacionUsuarioOpcionDto(
    int Id,
    string? Nombre
);
