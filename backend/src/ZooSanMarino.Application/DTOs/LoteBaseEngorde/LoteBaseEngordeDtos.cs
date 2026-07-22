// src/ZooSanMarino.Application/DTOs/LoteBaseEngorde/LoteBaseEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs.LoteBaseEngorde;

/// <summary>Ítem del catálogo de lotes base de engorde (agrupador global por empresa).</summary>
public sealed record LoteBaseEngordeDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? CodigoErp,
    string? LineaGenetica,
    /// <summary>Fecha de activación (vigencia por año; NULL = siempre vigente).</summary>
    DateTime? FechaActivacion,
    /// <summary>Desactivación manual: inactivo no aparece en el selector de crear-lote.</summary>
    bool Activo,
    /// <summary>Cantidad de lotes de engorde vivos amarrados a este lote base.</summary>
    int LotesAsignados,
    DateTime CreatedAt
);

public sealed record CreateLoteBaseEngordeDto(
    string Nombre,
    string? Descripcion = null,
    string? CodigoErp = null,
    string? LineaGenetica = null,
    DateTime? FechaActivacion = null
);

public sealed record UpdateLoteBaseEngordeDto(
    int Id,
    string Nombre,
    string? Descripcion = null,
    string? CodigoErp = null,
    string? LineaGenetica = null,
    DateTime? FechaActivacion = null
);

/// <summary>Body del toggle manual de activación.</summary>
public sealed record SetActivoLoteBaseEngordeDto(bool Activo);
