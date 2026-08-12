// src/ZooSanMarino.Domain/Entities/LoteSilo.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// De qué silos/bodegas CONSUME un lote. Relación <b>N:M</b>: un lote puede estar asignado a varios
/// silos (el caso del negocio: «si ese silo dejó de tener alimento, le asigno dos o más»).
///
/// <para>
/// El seguimiento diario (levante y producción) usa esta tabla para ofrecer solo los silos del lote,
/// y rechaza un consumo contra un silo que no esté acá.
/// </para>
///
/// <para>
/// Se cuelga de <c>lotes.lote_id</c> (el maestro) y NO de <c>lote_postura_levante</c> /
/// <c>lote_postura_produccion</c>: la asignación tiene que sobrevivir al cierre del levante y seguir
/// valiendo en producción, y <c>lotes</c> es la única fila que existe en las dos etapas. Colgarlo de
/// los espejos obligaría a copiarlo al cerrar, con una ventana en la que el lote queda sin silos.
/// </para>
///
/// <para>
/// Reasignar no recalcula lo ya consumido: los movimientos viejos conservan el silo con el que se
/// registraron.
/// </para>
/// </summary>
public class LoteSilo
{
    public int Id { get; set; }

    /// <summary>Empresa dueña del vínculo (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Lote maestro (FK a <c>lotes.lote_id</c>).</summary>
    public int LoteId { get; set; }

    /// <summary>Silo o bodega del que consume (FK a <c>farm_silos.id</c>). De la granja del lote.</summary>
    public int FarmSiloId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    // Navegación
    public FarmSilo FarmSilo { get; set; } = null!;
}
