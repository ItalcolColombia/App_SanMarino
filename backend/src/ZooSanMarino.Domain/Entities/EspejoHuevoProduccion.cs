// src/ZooSanMarino.Domain/Entities/EspejoHuevoProduccion.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Espejo de tipos de huevos por lote_postura_produccion.
/// huevo_*_historico: solo suma desde seguimiento_diario (producción).
/// huevo_*_dinamico: suma producción, resta movimientos (traslados/ventas).
/// </summary>
public class EspejoHuevoProduccion
{
    public int LotePosturaProduccionId { get; set; }
    public int CompanyId { get; set; }

    public int HuevoTotHistorico { get; set; }
    public int HuevoTotDinamico { get; set; }
    public int HuevoIncHistorico { get; set; }
    public int HuevoIncDinamico { get; set; }
    public int HuevoLimpioHistorico { get; set; }
    public int HuevoLimpioDinamico { get; set; }
    public int HuevoTratadoHistorico { get; set; }
    public int HuevoTratadoDinamico { get; set; }
    public int HuevoSucioHistorico { get; set; }
    public int HuevoSucioDinamico { get; set; }
    public int HuevoDeformeHistorico { get; set; }
    public int HuevoDeformeDinamico { get; set; }
    public int HuevoBlancoHistorico { get; set; }
    public int HuevoBlancoDinamico { get; set; }
    public int HuevoDobleYemaHistorico { get; set; }
    public int HuevoDobleYemaDinamico { get; set; }
    public int HuevoPisoHistorico { get; set; }
    public int HuevoPisoDinamico { get; set; }
    public int HuevoPequenoHistorico { get; set; }
    public int HuevoPequenoDinamico { get; set; }
    public int HuevoRotoHistorico { get; set; }
    public int HuevoRotoDinamico { get; set; }
    public int HuevoDesechoHistorico { get; set; }
    public int HuevoDesechoDinamico { get; set; }
    public int HuevoOtroHistorico { get; set; }
    public int HuevoOtroDinamico { get; set; }

    public string? HistoricoSemanal { get; set; } // JSONB
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public LotePosturaProduccion LotePosturaProduccion { get; set; } = null!;
    public Company Company { get; set; } = null!;
}
