// src/ZooSanMarino.Application/DTOs/LoteBaseEngorde/LoteBaseEngordeDtos.cs
namespace ZooSanMarino.Application.DTOs.LoteBaseEngorde;

/// <summary>Ítem del catálogo de lotes base de engorde (agrupador global por empresa).</summary>
public sealed record LoteBaseEngordeDto(
    int Id,
    string Nombre,
    string? Descripcion,
    string? CodigoErp,
    string? LineaGenetica,
    /// <summary>Cantidad de lotes de engorde vivos amarrados a este lote base.</summary>
    int LotesAsignados,
    DateTime CreatedAt
);

public sealed record CreateLoteBaseEngordeDto(
    string Nombre,
    string? Descripcion = null,
    string? CodigoErp = null,
    string? LineaGenetica = null
);

public sealed record UpdateLoteBaseEngordeDto(
    int Id,
    string Nombre,
    string? Descripcion = null,
    string? CodigoErp = null,
    string? LineaGenetica = null
);
