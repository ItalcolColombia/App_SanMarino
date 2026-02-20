// src/ZooSanMarino.Application/DTOs/Lotes/LoteFormDataDto.cs
// Datos necesarios para el modal de crear/editar lote en un solo response.

using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Galpones;

namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>
/// Payload único con todos los catálogos que necesita el front para el formulario de lote (crear/editar).
/// Reduce múltiples llamadas (Farms, Nucleos, Galpones, Users, Companies, Razas) a una sola.
/// </summary>
public sealed record LoteFormDataDto(
    IEnumerable<FarmDto> Farms,
    IEnumerable<NucleoDto> Nucleos,
    IEnumerable<GalponDetailDto> Galpones,
    IEnumerable<UserListDto> Tecnicos,
    IEnumerable<CompanyDto> Companies,
    IEnumerable<string> Razas
);
