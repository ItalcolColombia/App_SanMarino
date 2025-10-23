// file: src/ZooSanMarino.Application/DTOs/GalponDto.cs
namespace ZooSanMarino.Application.DTOs;

public sealed record GalponDto(
    string  GalponId,
    string  GalponNombre,
    string  NucleoId,     // ← unificado
    int     GranjaId,
    string? Ancho,
    string? Largo,
    string? TipoGalpon
);
