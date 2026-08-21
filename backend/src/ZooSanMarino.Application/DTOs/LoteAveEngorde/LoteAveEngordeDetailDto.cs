namespace ZooSanMarino.Application.DTOs.LoteAveEngorde;

using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

public sealed record LoteAveEngordeDetailDto(
    int LoteAveEngordeId,
    string LoteNombre,
    int GranjaId,
    string? NucleoId,
    string? GalponId,
    string? Regional,
    DateTime? FechaEncaset,
    DateTime? FechaAlistamiento,
    int? HembrasL,
    int? MachosL,
    double? PesoInicialH,
    double? PesoInicialM,
    double? UnifH,
    double? UnifM,
    int? MortCajaH,
    int? MortCajaM,
    string? Raza,
    int? AnoTablaGenetica,
    string? Linea,
    string? TipoLinea,
    string? CodigoGuiaGenetica,
    int? LineaGeneticaId,
    string? Tecnico,
    int? Mixtas,
    double? PesoMixto,
    int? AvesEncasetadas,
    int? EdadInicial,
    string? LoteErp,
    string? EstadoTraslado,
    string? EstadoOperativoLote,
    DateTime? LiquidadoAt,
    string? LiquidadoPorUserId,
    DateTime? ReabiertoAt,
    string? ReabiertoPorUserId,
    string? MotivoReapertura,
    // Mermas (R1) y sobrante de aves (R2)
    int? MermaUnidades,
    decimal? MermaKilos,
    DateTime? MermaRegistradaAt,
    string? MermaRegistradaPorUserId,
    int AvesSobrante,
    int? PaisId,
    string? PaisNombre,
    string? EmpresaNombre,
    int CompanyId,
    int CreatedByUserId,
    DateTime CreatedAt,
    int? UpdatedByUserId,
    DateTime? UpdatedAt,
    FarmLiteDto Farm,
    NucleoLiteDto? Nucleo,
    GalponLiteDto? Galpon,
    // Lote base global (opcional): agrupador para reportes por granja
    int? LoteBaseEngordeId = null,
    string? LoteBaseNombre = null,
    // Referencia de corrida (solo Panamá): número dentro del mismo lote base + galpón
    int? NumeroCorrida = null,
    /// <summary>Hora de llegada de las aves: desde las 13:00 el primer registro de seguimiento pasa al
    /// día siguiente del encasetamiento (ver EncasetamientoCalculos). No mueve la fecha ni la edad.</summary>
    TimeOnly? HoraEncasetamiento = null,
    // ─── Aves con que ARRANCÓ el lote (historial_lote_pollo_engorde, registro "Inicio") ───
    // No confundir con HembrasL/MachosL/Mixtas, que son el SALDO VIVO que el seguimiento diario y
    // las ventas van descontando. Estos tres son la base del encasetamiento y sólo se mueven con un
    // ajuste explícito (AjusteEncasetamientoCalculos). Null en un lote sin registro Inicio.
    /// <summary>Hembras con que se encasetó el lote (base histórica, no el saldo).</summary>
    int? InicialHembras = null,
    /// <summary>Machos con que se encasetó el lote (base histórica, no el saldo).</summary>
    int? InicialMachos = null,
    /// <summary>Mixtas con que se encasetó el lote (base histórica, no el saldo).</summary>
    int? InicialMixtas = null
);
