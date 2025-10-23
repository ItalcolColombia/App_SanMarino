// file: backend/src/ZooSanMarino.Application/DTOs/ResultadoLevanteDtos.cs
namespace ZooSanMarino.Application.DTOs;

public record ResultadoLevanteItemDto(
    DateTime Fecha,
    int?     EdadSemana,
    // H
    int?     HembraViva,
    int      MortH, int SelH, int ErrH,
    double?  ConsKgH, double? PesoH, double? UnifH, double? CvH,
    double?  MortHPct, double? SelHPct, double? ErrHPct,
    int      MsEhH, int AcMortH, int AcSelH, int AcErrH,
    double?  AcConsKgH, double? ConsAcGrH, double? GrAveDiaH,
    double?  DifConsHPct, double? DifPesoHPct, double? RetiroHPct, double? RetiroHAcPct,
    // M
    int?     MachoVivo,
    int      MortM, int SelM, int ErrM,
    double?  ConsKgM, double? PesoM, double? UnifM, double? CvM,
    double?  MortMPct, double? SelMPct, double? ErrMPct,
    int      MsEmM, int AcMortM, int AcSelM, int AcErrM,
    double?  AcConsKgM, double? ConsAcGrM, double? GrAveDiaM,
    double?  DifConsMPct, double? DifPesoMPct, double? RetiroMPct, double? RetiroMAcPct,
    // Relación y Guías
    double?  RelMHPct,
    double?  PesoHGuia, double? UnifHGuia, double? ConsAcGrHGuia, double? GrAveDiaHGuia, double? MortHPctGuia,
    double?  PesoMGuia, double? UnifMGuia, double? ConsAcGrMGuia, double? GrAveDiaMGuia, double? MortMPctGuia,
    string?  AlimentoHGuia, string? AlimentoMGuia
);

public record ResultadoLevanteResponse(
    int     LoteId,
    DateTime? Desde,
    DateTime? Hasta,
    int     Total,
    IReadOnlyList<ResultadoLevanteItemDto> Items
);
