namespace ZooSanMarino.Application.DTOs;

public record FarmDto(
    int Id,
    int CompanyId,
    string Name,
    int? RegionalId,   // ← nullable, **igual** que la entidad
    string Status,     // ← 'A' o 'I'
    int DepartamentoId,
    int CiudadId,      // ← OJO: el service mapea entity.MunicipioId → DTO.CiudadId
    string? DepartamentoNombre = null,  // ← Nombre del departamento
    string? CiudadNombre = null,        // ← Nombre de la ciudad/municipio
    string? RegionalNombre = null,      // ← Nombre de la regional
    string? CompanyNombre = null        // ← Nombre de la compañía (para no pedir lista aparte en el front)
);
