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
    int AvesInicioMachos,
    /// <summary>Cantidad de registros de seguimiento diario capturados (de 7).</summary>
    int NumRegistros = 0,
    /// <summary>Edad en días desde el encasetamiento hasta hoy.</summary>
    int EdadDias = 0,
    /// <summary>Hembras vivas actuales (inicio − mortalidad/selección/error de hembras).</summary>
    int AvesActualesHembras = 0,
    /// <summary>Machos vivos actuales (inicio − mortalidad/selección/error de machos).</summary>
    int AvesActualesMachos = 0,
    /// <summary>True si ya completó los 7 días de recogida de datos.</summary>
    bool SieteDiasCompletos = false,
    /// <summary>Código reproductora editable por el usuario (distinto del ReproductoraId autogenerado).</summary>
    string? CodigoReproductora = null,
    /// <summary>True si el lote (cerrado) fue reabierto con novedad para permitir eliminar registros.</summary>
    bool Reabierto = false,
    /// <summary>Novedad/motivo con que se reabrió el lote.</summary>
    string? NovedadApertura = null
);

/// <summary>Request para reabrir un lote reproductora cerrado. La novedad es obligatoria.</summary>
public record ReabrirLoteReproductoraDto(string Novedad);

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
    decimal? PesoMixto,
    string? CodigoReproductora = null
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
    decimal? PesoMixto,
    string? CodigoReproductora = null
);
