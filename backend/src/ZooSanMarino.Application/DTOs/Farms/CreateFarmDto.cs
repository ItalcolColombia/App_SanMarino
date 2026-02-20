namespace ZooSanMarino.Application.DTOs.Farms;

public record CreateFarmDto(
    string Name,
    int CompanyId,      // si lo pones aquí o lo resuelves por _current.CompanyId (elige una)
    string Status,      // 'A' | 'I'
    int? RegionalId,    // ← nullable; id de la tabla Regional
    int? RegionalOptionId, // ← opcional; id de master_list_options; se resuelve a RegionalId por nombre
    int? DepartamentoId,
    int? CiudadId       // ← **ciudadId** del front; se mapea a entity.MunicipioId
);

