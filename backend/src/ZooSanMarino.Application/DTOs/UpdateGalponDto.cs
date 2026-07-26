// file: src/ZooSanMarino.Application/DTOs/UpdateGalponDto.cs
namespace ZooSanMarino.Application.DTOs;

public sealed record UpdateGalponDto(
    string  GalponId,
    string  GalponNombre,
    string  NucleoId,     // ← antes GalponNucleoId
    int     GranjaId,
    string? Ancho,
    string? Largo,
    string? TipoGalpon,
    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    string? CodigoErpUbicacion      = null,
    string? DescripcionErpUbicacion = null
);
