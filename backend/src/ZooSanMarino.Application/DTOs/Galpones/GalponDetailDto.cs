// file: src/ZooSanMarino.Application/DTOs/Galpones/GalponDetailDto.cs
using System;
namespace ZooSanMarino.Application.DTOs.Galpones;

// Alias para evitar conflictos de nombres
using FarmLite = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using Shared  = ZooSanMarino.Application.DTOs.Shared;

public sealed record GalponDetailDto(
    string   GalponId,
    string   GalponNombre,
    string   NucleoId,
    int      GranjaId,
    string?  Ancho,
    string?  Largo,
    string?  TipoGalpon,
    int      CompanyId,
    int?     CreatedByUserId,
    DateTime? CreatedAt,
    int?     UpdatedByUserId,
    DateTime? UpdatedAt,
    FarmLite                 Farm,    // ← Farms.FarmLiteDto (int? RegionalId)
    Shared.NucleoLiteDto     Nucleo,  // ← Shared
    Shared.CompanyLiteDto    Company  // ← Shared
);
