// src/ZooSanMarino.Domain/Entities/LoteHuevoItem.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Qué tipos de huevo PRODUCE un lote — la lista blanca de ítems del catálogo
/// (<c>catalogo_items</c> con <c>item_type = 'huevo'</c>) que ese lote puede clasificar en el
/// seguimiento diario de producción. Relación <b>N:M</b>: un lote declara varios ítems.
///
/// <para>
/// <b>Fail-closed a propósito</b> (decisión del cliente, 21-ago-2026): un lote SIN ítems asignados
/// no ofrece ninguno y el guardado rechaza cualquier clasificación. No se cae al catálogo completo.
/// El operario tiene que editar el lote y declarar qué produce — que es justamente el control que
/// el cliente pidió: «así controlamos mejor todo».
/// </para>
///
/// <para>
/// Se cuelga de <c>lotes.lote_id</c> (el maestro) y NO de <c>lote_postura_produccion</c>, por la
/// misma razón que <see cref="LoteSilo"/>: <c>lotes</c> es la única fila que existe en las dos
/// etapas, así que la declaración sobrevive al cierre del levante sin tener que copiarla.
/// </para>
///
/// <para>
/// <b>Solo aplica a PRODUCCIÓN.</b> Levante no tiene modelo de ítems para huevos en ninguna capa
/// (usa las 11 columnas fijas) y el backend excluye de la captura en levante a las empresas que
/// clasifican por ítems — ver <c>SeguimientoLoteLevanteService.EmpresaCapturaHuevosEnLevanteAsync</c>.
/// </para>
///
/// <para>
/// Cambiar la lista NO reescribe lo ya registrado: cada seguimiento guarda su propio desglose en
/// <c>metadata.huevoItems</c>, y esa foto es la que leen indicadores, reportes y traslados.
/// </para>
/// </summary>
public class LoteHuevoItem
{
    public int Id { get; set; }

    /// <summary>Empresa dueña del vínculo (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Lote maestro (FK a <c>lotes.lote_id</c>).</summary>
    public int LoteId { get; set; }

    /// <summary>Ítem de huevo del catálogo (FK a <c>catalogo_items.id</c>, <c>item_type='huevo'</c>).</summary>
    public int CatalogItemId { get; set; }

    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedByUserId { get; set; }

    // Navegación
    public CatalogItem CatalogItem { get; set; } = null!;
}
