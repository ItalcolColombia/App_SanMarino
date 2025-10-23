// src/ZooSanMarino.Application/DTOs/Farms/FarmDetailDto.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record FarmDetailDto(
    int        Id,
    int        CompanyId,
    string     Name,
    int?       RegionalId,       // nullable
    string     Status,           // 'A' | 'I'
    int        DepartamentoId,
    int        CiudadId,         // mapeado desde entidad.MunicipioId
    int?       CreatedByUserId,
    DateTime?  CreatedAt,
    int?       UpdatedByUserId,
    DateTime?  UpdatedAt,
    int        NucleosCount,
    int        GalponesCount,
    int        LotesCount
);
