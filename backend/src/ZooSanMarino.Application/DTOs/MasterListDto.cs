// src/ZooSanMarino.Application/DTOs/MasterListDto.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>Opción de una lista maestra con id para poder guardarla en registros (ej. granja).</summary>
public record MasterListOptionItemDto(int Id, string Value);

public record MasterListDto(
    int      Id,
    string   Key,
    string   Name,
    IEnumerable<MasterListOptionItemDto> Options,
    IEnumerable<string> OptionValues, // Opciones solo como texto (Options.Select(o => o.Value)); compatibilidad con consumidores que esperan string[]
    int? CompanyId = null,
    string? CompanyName = null,
    int? CountryId = null,
    string? CountryName = null
);

public record CreateMasterListDto(
    string   Key,
    string   Name,
    IEnumerable<string> Options,
    int? CompanyId = null,
    int? CountryId = null
);

public record UpdateMasterListDto(
    int      Id,
    string   Key,
    string   Name,
    IEnumerable<string> Options,
    int? CompanyId = null,
    int? CountryId = null
);
