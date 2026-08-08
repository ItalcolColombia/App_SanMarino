// src/ZooSanMarino.Application/Calculos/PaginacionCalculos.cs
// Normalización del tamaño de página de los listados paginados. Sin EF, sin estado, sin I/O.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas de paginación compartidas por los listados paginados.
/// <para>
/// <b>Por qué existe.</b> Los services traían este clamp copiado a mano:
/// <c>if (pageSize &lt;= 0 || pageSize &gt; 200) pageSize = 20;</c> — es decir, <b>pedir de MÁS
/// devolvía el MÍNIMO</b>. No es un tope: es una pérdida silenciosa de datos, sin error ni warning,
/// y produjo dos incidentes reales:
/// </para>
/// <list type="bullet">
/// <item><c>ReporteContableService</c> pedía 10.000 movimientos de inventario y recibía 20 ⇒ el
/// Reporte Contable de postura solo veía los 20 más recientes de la granja (commit
/// <c>92cd918</c>).</item>
/// <item>Siete pantallas del front piden 1.000-2.000 ítems de catálogo para llenar sus selectores
/// (<c>ajuste-form</c>, <c>conteo-fisico</c>, <c>kardex-list</c>, <c>traslado-form</c> y los modales de
/// seguimiento de levante y producción) y recibían 20, sobre los que además filtraban por activo.</item>
/// </list>
/// <para>
/// <b>La regla correcta:</b> quien no especifica tamaño recibe el default; quien pide más que el tope
/// recibe <b>el tope</b>, nunca el default. Así un pedido excesivo devuelve menos de lo pedido pero
/// coherente con el <c>Total</c> que viaja en el mismo <c>PagedResult</c>, y el llamador puede paginar.
/// </para>
/// </summary>
public static class PaginacionCalculos
{
    /// <summary>Tamaño por defecto histórico de los listados.</summary>
    public const int PageSizePorDefecto = 20;

    /// <summary>
    /// Tope de los listados que crecen sin techo (movimientos de inventario, auditorías): pedir más
    /// obliga a paginar de verdad.
    /// </summary>
    public const int MaximoListadoTransaccional = 200;

    /// <summary>
    /// Tope de las tablas MAESTRAS acotadas (catálogo de ítems), que el front consume entero como
    /// selector. El catálogo más grande hoy es el de Santa Reyes con 310 ítems: 2.000 deja margen 6×
    /// sin volver la consulta pesada (proyección liviana sobre columnas indexadas).
    /// </summary>
    public const int MaximoCatalogoMaestro = 2000;

    /// <summary>
    /// Normaliza el tamaño de página pedido.
    /// <list type="bullet">
    /// <item><paramref name="pedido"/> ≤ 0 ⇒ <paramref name="porDefecto"/> (no especificó nada).</item>
    /// <item><paramref name="pedido"/> &gt; <paramref name="maximo"/> ⇒ <paramref name="maximo"/>
    /// (pidió de más: se le da el tope, JAMÁS el default — esta es la línea que blinda el bug).</item>
    /// <item>en rango ⇒ el valor pedido.</item>
    /// </list>
    /// </summary>
    public static int NormalizarPageSize(
        int pedido,
        int maximo = MaximoListadoTransaccional,
        int porDefecto = PageSizePorDefecto)
    {
        if (pedido <= 0) return porDefecto;
        return pedido > maximo ? maximo : pedido;
    }

    /// <summary>Normaliza el número de página: nunca menor que 1.</summary>
    public static int NormalizarPage(int pedido) => pedido <= 0 ? 1 : pedido;
}
