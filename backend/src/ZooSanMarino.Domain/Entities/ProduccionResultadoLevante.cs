// file: backend/src/ZooSanMarino.Domain/Entities/ProduccionResultadoLevante.cs
namespace ZooSanMarino.Domain.Entities;

public class ProduccionResultadoLevante
{
    public int LoteId { get; set; }
    public DateTime Fecha { get; set; }
    public int?    EdadSemana { get; set; }

    // H
    public int?    HembraViva { get; set; }
    public int     MortH { get; set; }
    public int     SelHOut { get; set; }
    public int     ErrH { get; set; }
    public double? ConsKgH { get; set; }
    public double? PesoH { get; set; }
    public double? UnifH { get; set; }
    public double? CvH { get; set; }
    public double? MortHPct { get; set; }
    public double? SelHPct { get; set; }
    public double? ErrHPct { get; set; }
    public int     MsEhH { get; set; }
    public int     AcMortH { get; set; }
    public int     AcSelH { get; set; }
    public int     AcErrH { get; set; }
    public double? AcConsKgH { get; set; }
    public double? ConsAcGrH { get; set; }
    public double? GrAveDiaH { get; set; }
    public double? DifConsHPct { get; set; }
    public double? DifPesoHPct { get; set; }
    public double? RetiroHPct { get; set; }
    public double? RetiroHAcPct { get; set; }

    // M
    public int?    MachoVivo { get; set; }
    public int     MortM { get; set; }
    public int     SelMOut { get; set; }
    public int     ErrM { get; set; }
    public double? ConsKgM { get; set; }
    public double? PesoM { get; set; }
    public double? UnifM { get; set; }
    public double? CvM { get; set; }
    public double? MortMPct { get; set; }
    public double? SelMPct { get; set; }
    public double? ErrMPct { get; set; }
    public int     MsEmM { get; set; }
    public int     AcMortM { get; set; }
    public int     AcSelM { get; set; }
    public int     AcErrM { get; set; }
    public double? AcConsKgM { get; set; }
    public double? ConsAcGrM { get; set; }
    public double? GrAveDiaM { get; set; }
    public double? DifConsMPct { get; set; }
    public double? DifPesoMPct { get; set; }
    public double? RetiroMPct { get; set; }
    public double? RetiroMAcPct { get; set; }

    // Rel/Guías
    public double? RelMHPct { get; set; }
    public double? PesoHGuia { get; set; }
    public double? UnifHGuia { get; set; }
    public double? ConsAcGrHGuia { get; set; }
    public double? GrAveDiaHGuia { get; set; }
    public double? MortHPctGuia { get; set; }
    public double? PesoMGuia { get; set; }
    public double? UnifMGuia { get; set; }
    public double? ConsAcGrMGuia { get; set; }
    public double? GrAveDiaMGuia { get; set; }
    public double? MortMPctGuia { get; set; }
    public string? AlimentoHGuia { get; set; }
    public string? AlimentoMGuia { get; set; }
}
