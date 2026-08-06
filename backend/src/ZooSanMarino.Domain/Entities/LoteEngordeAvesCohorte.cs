// src/ZooSanMarino.Domain/Entities/LoteEngordeAvesCohorte.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Cohorte de aves RECIBIDAS por un lote de POLLO ENGORDE en un traslado: grupo que entró al lote
/// <see cref="LoteAveEngordeId"/> conservando la edad y la ubicación que traía de su lote de origen.
/// <para>
/// Espejo de <see cref="LoteAvesCohorte"/> (postura) para la línea de engorde. Vive en su propia tabla
/// porque el lote receptor es <c>lote_ave_engorde</c>, no <c>lotes</c>; y lleva <see cref="CantidadMixtas"/>
/// porque en engorde las aves pueden vivir en un solo bucket mixto.
/// </para>
/// <para>
/// <b>Por qué existe:</b> hasta ahora la edad de un lote de engorde salía de una sola fecha
/// (<c>lote_ave_engorde.fecha_encaset</c>), así que un lote que recibía aves de otro las absorbía con la
/// edad del receptor y sin rastro de su procedencia. Además el techo de venta de la auditoría parte del
/// registro <c>Inicio</c> del historial, que solo se escribe al CREAR el lote: sin estas filas, vender las
/// aves recibidas se reportaba como sobreventa aunque existieran en el maestro.
/// </para>
/// Se crea al COMPLETAR el movimiento (que es cuando <c>CompleteAsync</c> acredita el maestro del destino)
/// y se da de baja lógica al cancelarlo o eliminarlo — nunca se borra.
/// </summary>
public class LoteEngordeAvesCohorte : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Lote de engorde RECEPTOR de las aves.</summary>
    public int LoteAveEngordeId { get; set; }

    /// <summary>Lote de engorde del que provienen las aves (informativo; null si el origen no es un lote AE).</summary>
    public int? LoteAveEngordeOrigenId { get; set; }

    /// <summary>Movimiento que originó la cohorte (auditoría / reversión).</summary>
    public int? MovimientoPolloEngordeId { get; set; }

    /// <summary>
    /// Ubicación del lote ORIGEN <b>congelada</b> al momento del traslado: sobrevive a que el lote origen se
    /// reubique o se elimine, que es lo que hace auditable un receptor con aves de varias procedencias.
    /// </summary>
    public int? GranjaOrigenId { get; set; }
    public string? NucleoOrigenId { get; set; }
    public string? GalponOrigenId { get; set; }

    /// <summary>Fecha en que las aves ingresaron al lote receptor (fecha del traslado, no la de registro).</summary>
    public DateOnly FechaIngreso { get; set; }

    /// <summary>
    /// Fecha de encasetamiento del lote ORIGEN. La edad de la cohorte se calcula SIEMPRE desde esta fecha —
    /// es lo que permite que un lote tenga grupos de distinta edad.
    /// </summary>
    public DateOnly FechaEncasetCohorte { get; set; }

    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }
    public int CantidadMixtas { get; set; }

    public string? Observaciones { get; set; }

    public int TotalAves => CantidadHembras + CantidadMachos + CantidadMixtas;
}
