namespace ZooSanMarino.Domain.Entities;

/// <summary>Una etapa (1..20) de una definición de flujo. El orden es único dentro del flujo.</summary>
public class ValidacionFlujoPaso
{
    public int Id { get; set; }
    public int FlujoId { get; set; }

    public int Orden { get; set; }
    public string Nombre { get; set; } = null!;

    /// <summary>Fase 1 siempre 1 (ANY); deja preparado un quorum futuro (&gt;1).</summary>
    public int AprobacionesRequeridas { get; set; } = 1;

    public string? Descripcion { get; set; }

    public ValidacionFlujo Flujo { get; set; } = null!;
    public ICollection<ValidacionFlujoAsignado> Asignados { get; set; } = new List<ValidacionFlujoAsignado>();
}
