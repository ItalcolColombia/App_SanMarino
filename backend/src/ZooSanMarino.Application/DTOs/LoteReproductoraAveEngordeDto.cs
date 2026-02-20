// src/ZooSanMarino.Application/DTOs/LoteReproductoraAveEngordeDto.cs
namespace ZooSanMarino.Application.DTOs;

public record LoteReproductoraAveEngordeDto(
    int Id,
    int LoteAveEngordeId,
    string ReproductoraId,
    string NombreLote,
    DateTime? FechaEncasetamiento,
    int? M,
    int? H,
    int? Mixtas,
    int? MortCajaH,
    int? MortCajaM,
    int? UnifH,
    int? UnifM,
    decimal? PesoInicialM,
    decimal? PesoInicialH,
    decimal? PesoMixto,
    /// <summary>Cerrado cuando todas las aves iniciales fueron vendidas; Vigente si aún tiene aves.</summary>
    string Estado,
    /// <summary>Aves actuales = encasetadas - mortalidad - selección - ventas (saldo actual).</summary>
    int AvesActuales,
    /// <summary>Total de aves con que se abrió el lote reproductor (H + M + Mixtas al inicio).</summary>
    int SaldoApertura,
    /// <summary>Hembras al abrir el lote reproductor.</summary>
    int AvesInicioHembras,
    /// <summary>Machos al abrir el lote reproductor.</summary>
    int AvesInicioMachos
);

public record CreateLoteReproductoraAveEngordeDto(
    int LoteAveEngordeId,
    string ReproductoraId,
    string NombreLote,
    DateTime? FechaEncasetamiento,
    int? M,
    int? H,
    int? Mixtas,
    int? MortCajaH,
    int? MortCajaM,
    int? UnifH,
    int? UnifM,
    decimal? PesoInicialM,
    decimal? PesoInicialH,
    decimal? PesoMixto
);

public record UpdateLoteReproductoraAveEngordeDto(
    int LoteAveEngordeId,
    string ReproductoraId,
    string NombreLote,
    DateTime? FechaEncasetamiento,
    int? M,
    int? H,
    int? Mixtas,
    int? MortCajaH,
    int? MortCajaM,
    int? UnifH,
    int? UnifM,
    decimal? PesoInicialM,
    decimal? PesoInicialH,
    decimal? PesoMixto
);
