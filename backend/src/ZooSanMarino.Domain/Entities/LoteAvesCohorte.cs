// src/ZooSanMarino.Domain/Entities/LoteAvesCohorte.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Cohorte de aves RECIBIDAS por un lote en un traslado: grupo de aves que entró al lote
/// <see cref="LoteId"/> conservando la edad que traía de su lote de origen.
/// <para>
/// Un lote puede tener aves propias (edad implícita = su <c>lotes.fecha_encaset</c>) y aves
/// recibidas de otros lotes con otra edad; cada cohorte cuenta SIEMPRE su edad desde
/// <see cref="FechaEncasetCohorte"/> (el encasetamiento del lote ORIGEN), no desde la fecha de
/// ingreso ni desde el encaset del lote receptor.
/// </para>
/// Se crea en cada traslado de aves desde el seguimiento diario (misma etapa o cross-etapa
/// Levante→Producción) dentro de la misma transacción del traslado, y se soft-deletea cuando se
/// elimina el <see cref="MovimientoAves"/> de auditoría que la originó.
/// </summary>
public class LoteAvesCohorte : AuditableEntity
{
    public int Id { get; set; }

    /// <summary>Lote RECEPTOR de las aves (FK a <c>lotes.lote_id</c>).</summary>
    public int LoteId { get; set; }

    /// <summary>Lote base del que provienen las aves (informativo; puede ser null en cargas manuales).</summary>
    public int? LoteOrigenId { get; set; }

    /// <summary>Movimiento de aves que originó la cohorte (auditoría / reversión).</summary>
    public int? MovimientoAvesId { get; set; }

    /// <summary>Fecha en que las aves ingresaron al lote receptor (fecha del traslado).</summary>
    public DateOnly FechaIngreso { get; set; }

    /// <summary>
    /// Fecha de encasetamiento del lote ORIGEN. La edad de la cohorte (días/semanas) se calcula
    /// SIEMPRE desde esta fecha — es lo que permite que un lote tenga grupos de distinta edad.
    /// </summary>
    public DateOnly FechaEncasetCohorte { get; set; }

    public int CantidadHembras { get; set; }
    public int CantidadMachos { get; set; }

    public string? Observaciones { get; set; }
}
