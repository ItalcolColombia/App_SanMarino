// src/ZooSanMarino.Domain/Entities/Vacunacion/VacunacionCronogramaItem.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Ítem del cronograma de vacunación de un lote: una vacuna programada para aplicarse en una
/// franja de tiempo (semana de vida en Postura, día de vida en Engorde, o fecha fija sin importar
/// la fase). El lote se referencia vía exactamente un FK de línea (<see cref="LotePosturaLevanteId"/>,
/// <see cref="LotePosturaProduccionId"/> o <see cref="LoteAveEngordeId"/>) según <see cref="LineaProductiva"/>,
/// porque no existe un "Lote" único confiable entre líneas (ver plan del módulo).
/// </summary>
public class VacunacionCronogramaItem : AuditableEntity
{
    public int Id { get; set; }
    public int? PaisId { get; set; }

    /// <summary>"Levante" | "Produccion" | "Engorde" (extensible a futuras líneas sin migración de schema).</summary>
    public string LineaProductiva { get; set; } = null!;

    public int? LotePosturaLevanteId { get; set; }
    public int? LotePosturaProduccionId { get; set; }
    public int? LoteAveEngordeId { get; set; }

    // Denormalizado al crear (mismo patrón que las tablas de lote) para filtros de reportes sin join extra.
    public int GranjaId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }

    /// <summary>FK a ItemInventario (filtrado por TipoItem = "vacuna" en el selector).</summary>
    public int ItemInventarioId { get; set; }

    /// <summary>"Semana" | "Dia" | "Fecha".</summary>
    public string UnidadObjetivo { get; set; } = null!;

    /// <summary>Semana N (Postura) o día N de edad (Engorde), según <see cref="UnidadObjetivo"/>.</summary>
    public int? ValorObjetivo { get; set; }

    /// <summary>Fecha fija objetivo, usada solo si <see cref="UnidadObjetivo"/> = "Fecha".</summary>
    public DateTime? FechaObjetivo { get; set; }

    /// <summary>Ancho de la franja válida antes/después del objetivo (ej. semana = 6/0 para lunes-domingo).</summary>
    public int RangoDiasAntes { get; set; }
    public int RangoDiasDespues { get; set; }

    public int Orden { get; set; }
    public bool Activo { get; set; } = true;
    public string? Notas { get; set; }

    /// <summary>
    /// Ítem de la plantilla de empresa del que salió esta fila, o <c>null</c> si se cargó a mano.
    ///
    /// <para>
    /// Es la clave de idempotencia del materializador: <c>(lote, origen_plantilla_item_id)</c> es
    /// único, así que aplicar el plan N veces deja el mismo cronograma. Se conserva aunque el ítem
    /// deje de estar gobernado por el plan (ver <see cref="GeneradoAutomatico"/>) —justamente para
    /// que no se pueda crear un duplicado— y la FK es <c>ON DELETE SET NULL</c>: el ítem del lote es
    /// historia sanitaria y sobrevive al borrado de la plantilla de la que nació.
    /// </para>
    /// </summary>
    public int? OrigenPlantillaItemId { get; set; }

    /// <summary>
    /// <c>true</c> mientras esta fila la gobierna el plan de la empresa; <c>false</c> si la escribió
    /// —o la corrigió— una persona.
    ///
    /// <para>
    /// Todo lo que existía antes del materializador nace en <c>false</c>, que es exactamente lo que
    /// es: cargado a mano. Y editar por el CRUD un ítem generado lo pasa a <c>false</c>: una
    /// corrección sobre <b>este</b> lote es una decisión explícita, y el plan de la empresa no la
    /// puede deshacer en silencio en la próxima materialización.
    /// </para>
    /// </summary>
    public bool GeneradoAutomatico { get; set; }

    public Farm Farm { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }
    public ItemInventario ItemInventario { get; set; } = null!;
    public LotePosturaLevante? LotePosturaLevante { get; set; }
    public LotePosturaProduccion? LotePosturaProduccion { get; set; }
    public LoteAveEngorde? LoteAveEngorde { get; set; }
    public VacunacionRegistroAplicacion? RegistroAplicacion { get; set; }
}
