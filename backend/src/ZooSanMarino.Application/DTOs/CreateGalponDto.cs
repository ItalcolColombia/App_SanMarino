// file: src/ZooSanMarino.Application/DTOs/CreateGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

public sealed record CreateGalponDto(
    string  GalponId,
    string  GalponNombre,
    string  NucleoId,     // ← unificado
    int     GranjaId,
    string? Ancho,
    string? Largo,
    string? TipoGalpon,
    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    string? CodigoErpUbicacion      = null,
    string? DescripcionErpUbicacion = null
);
