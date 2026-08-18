// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionPlanPlantilla.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Plan de vacunación estándar de una empresa para una línea productiva y —opcionalmente— una raza:
/// el cronograma que después se materializa en cada lote.
///
/// <para>
/// Hasta ahora el cronograma se cargaba lote por lote, así que el plan sanitario vivía en la cabeza
/// del técnico y cada lote podía quedar distinto sin que nada lo notara. Esta tabla lo hace un dato:
/// se programa una vez y cada lote sabe solo cuál le toca.
/// </para>
///
/// <para>
/// <b><see cref="Raza"/> null es el comodín</b> —vale para toda la línea— y una raza explícita le
/// gana. A igual especificidad gana la de <see cref="VigenteDesde"/> más reciente. La regla completa
/// vive en <c>VacunacionPlantillaCalculos.ResolverEfectiva</c>, que es pura y está testeada: dos
/// plantillas que compiten nunca se resuelven "por el orden en que salieron de la base".
/// </para>
/// </summary>
public class VacunacionPlanPlantilla : AuditableEntity
{
    public int Id { get; set; }
    public int? PaisId { get; set; }

    public string Nombre { get; set; } = null!;

    /// <summary>"Levante" | "Produccion" | "Engorde" — mismos literales que el cronograma.</summary>
    public string LineaProductiva { get; set; } = null!;

    /// <summary>Raza a la que aplica. <b>Null = comodín</b>: vale para toda la línea.</summary>
    public string? Raza { get; set; }

    /// <summary>
    /// Aplica a lotes encasetados desde esta fecha. Permite cambiar el plan sanitario sin reescribir
    /// el de los lotes que ya venían con el anterior.
    /// </summary>
    public DateTime? VigenteDesde { get; set; }

    public bool Activa { get; set; } = true;
    public string? Notas { get; set; }

    public ICollection<VacunacionPlanPlantillaItem> Items { get; set; } = new List<VacunacionPlanPlantillaItem>();
}
