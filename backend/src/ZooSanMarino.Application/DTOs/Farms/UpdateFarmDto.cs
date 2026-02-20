// src/ZooSanMarino.Application/DTOs/UpdateFarmDto.cs
namespace ZooSanMarino.Application.DTOs.Farms;

public record UpdateFarmDto(
    int    Id,
    int    CompanyId,        // lo puedes ignorar en el service si usas _current.CompanyId
    string Name,
    string Status,           // 'A' | 'I'
    int?   RegionalId,       // ← nullable (front puede no enviarlo)
    int?   RegionalOptionId, // ← opcional; id de master_list_options; se resuelve a RegionalId por nombre
    int?   DepartamentoId,   // ← nullable (validado en el service)
    int?   CiudadId          // ← nullable (validado en el service; mapea a MunicipioId)
);
