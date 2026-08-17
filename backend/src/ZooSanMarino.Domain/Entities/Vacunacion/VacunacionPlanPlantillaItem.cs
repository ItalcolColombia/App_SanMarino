// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionPlanPlantillaItem.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Una vacuna del plan estándar: qué se aplica y en qué momento de la vida del lote.
///
/// <para>
/// Espeja a propósito los campos de <see cref="VacunacionCronogramaItem"/>
/// (<c>UnidadObjetivo</c> / <c>ValorObjetivo</c> / <c>RangoDias*</c>) porque materializar es
/// copiarlos tal cual: si acá se guardara otra forma del mismo dato, el cronograma del lote y el
/// plan del que salió podrían decir cosas distintas y no habría manera de saber cuál manda.
/// </para>
///
/// <para>
/// No lleva <c>"Fecha"</c> como unidad: una fecha fija no se puede plantillar —sería la misma para
/// lotes encasetados en meses distintos—. Las fechas fijas siguen siendo ítems manuales del lote.
/// </para>
/// </summary>
public class VacunacionPlanPlantillaItem : AuditableEntity
{
    public int Id { get; set; }
    public int PlantillaId { get; set; }

    /// <summary>FK a ItemInventario (filtrado por TipoItem = "vacuna" en el selector).</summary>
    public int ItemInventarioId { get; set; }

    /// <summary>"Semana" (Postura) | "Dia" (Engorde).</summary>
    public string UnidadObjetivo { get; set; } = null!;

    /// <summary>Semana o día de vida en que toca.</summary>
    public int ValorObjetivo { get; set; }

    /// <summary>Ancho de la franja válida antes/después del objetivo.</summary>
    public int RangoDiasAntes { get; set; }
    public int RangoDiasDespues { get; set; }

    public int Orden { get; set; }
    public string? Notas { get; set; }

    public VacunacionPlanPlantilla Plantilla { get; set; } = null!;
    public ItemInventario ItemInventario { get; set; } = null!;
}
