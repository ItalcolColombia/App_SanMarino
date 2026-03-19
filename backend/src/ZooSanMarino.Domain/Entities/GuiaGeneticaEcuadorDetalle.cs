namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Detalle de Guía Genética Ecuador: una fila por día + sexo.
/// </summary>
public class GuiaGeneticaEcuadorDetalle : AuditableEntity
{
    public int Id { get; set; }

    public int GuiaGeneticaEcuadorHeaderId { get; set; }
    public virtual GuiaGeneticaEcuadorHeader GuiaGeneticaEcuadorHeader { get; set; } = null!;

    /// <summary>Sexo: mixto / hembra / macho (guardado en lowercase).</summary>
    public string Sexo { get; set; } = null!;

    public int Dia { get; set; }

    public decimal PesoCorporalG { get; set; }
    public decimal GananciaDiariaG { get; set; }
    public decimal PromedioGananciaDiariaG { get; set; }
    public decimal CantidadAlimentoDiarioG { get; set; }
    public decimal AlimentoAcumuladoG { get; set; }

    public decimal CA { get; set; }
    public decimal MortalidadSeleccionDiaria { get; set; }
}

