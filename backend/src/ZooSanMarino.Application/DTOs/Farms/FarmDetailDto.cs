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
    int        LotesCount,
    // ────────────────────────────────────────────────────────────────
    // NUEVOS CAMPOS (Panamá): cliente, zona, certificación GAB y geo
    // ────────────────────────────────────────────────────────────────
    int?       ClienteId       = null,
    string?    Zona            = null,   // 'Zona 1' | 'Zona 2'
    bool       CertificadoGab  = false,
    decimal?   Latitud         = null,
    decimal?   Longitud        = null
);
