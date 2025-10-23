// file: src/ZooSanMarino.Application/DTOs/Nucleos/NucleoDetailDto.cs
using ZooSanMarino.Application.DTOs.Farms;
using ZooSanMarino.Application.DTOs.Shared;

namespace ZooSanMarino.Application.DTOs.Nucleos;

public sealed record NucleoDetailDto(
    string  NucleoId,
    int     GranjaId,
    string  NucleoNombre,
    // Auditoría
    int      CompanyId,
    int      CreatedByUserId,
    DateTime CreatedAt,
    int?     UpdatedByUserId,
    DateTime? UpdatedAt,
    // Relación
    FarmLiteDto Farm,
    // Métricas
    int GalponesCount,
    int LotesCount
);
