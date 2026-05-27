// src/ZooSanMarino.Application/DTOs/Farms/FarmLiteDto.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public sealed record FarmLiteDto(
    int    Id,
    string Name,
    int?   RegionalId,      // nullable
    int    DepartamentoId,
    int    CiudadId,        // desde entidad.MunicipioId
    // ────────────────────────────────────────────────────────────────
    // NUEVOS CAMPOS (Panamá): cliente, zona, certificación GAB y geo
    // ────────────────────────────────────────────────────────────────
    int?       ClienteId       = null,
    string?    Zona            = null,   // 'Zona 1' | 'Zona 2'
    bool       CertificadoGab  = false,
    decimal?   Latitud         = null,
    decimal?   Longitud        = null
);
