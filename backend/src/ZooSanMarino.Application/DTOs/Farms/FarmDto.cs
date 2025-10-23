namespace ZooSanMarino.Application.DTOs;

public record FarmDto(
    int Id,
    int CompanyId,
    string Name,
    int? RegionalId,   // ← nullable, **igual** que la entidad
    string Status,     // ← 'A' o 'I'
    int DepartamentoId,
    int CiudadId       // ← OJO: el service mapea entity.MunicipioId → DTO.CiudadId
);
