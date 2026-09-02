// src/ZooSanMarino.Application/Calculos/SemanaGuiaProduccionCalculos.cs
//
// Qué semana usar para cruzar una fila de PRODUCCIÓN contra la guía genética.
//
// 🔴 LA GUÍA SE INDEXA POR SEMANA DE VIDA DEL AVE, NO POR SEMANA DE POSTURA. Medido el
// 1-sep-2026 sobre las guías reales de las empresas:
//
//   guia_genetica_sanmarino_colombia  edad 1..71-97, primera edad con producción = 25/26,
//                                     primera edad con peso_h = 1  ⇒ cubre levante + postura
//   guia_genetica_santa_reyes         edad 18..140                 ⇒ arranca en producción
//
// Que la producción aparezca recién en la edad 25/26 es la prueba: es la semana de vida en que
// el ave empieza a poner. Por eso `GuiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync` filtra
// `edad >= 26` — ese método ya trataba la columna como semana de vida.
//
// Los reportes de producción, en cambio, numeran sus semanas DESDE EL INICIO DE PRODUCCIÓN
// (semana 1 = primera semana de postura) y cruzaban la guía con ese número. Resultado medido en el
// lote P-K345B de Sanmarino (encaset 2025-01-31, inicio de producción 2025-07-19, 169 días ⇒
// semana 25 de vida): su primera semana de postura se comparaba contra la fila de edad 1 de la
// guía —una pollita de un día—, que trae producción y peso vacíos y `uniformidad = 70`, un dato de
// LEVANTE que el reporte pintaba como si fuera la meta de postura.
//
// El desfase no era menor ni excepcional: de los lotes de producción vivos, TODOS tienen encaset
// cargado y el eje se corre entre 128 y 363 días (18 a 52 semanas).
namespace ZooSanMarino.Application.Calculos;

public static class SemanaGuiaProduccionCalculos
{
    /// <summary>
    /// Semana (base 1) de <paramref name="fecha"/> contada desde <paramref name="fechaBase"/>.
    /// Aritmética IDÉNTICA a la que tenían inline los reportes (<c>(int)</c> sobre
    /// <c>TotalDays</c> y luego <c>Math.Ceiling((dias + 1.0) / 7)</c>): no se normaliza a
    /// <c>.Date</c> ni se cambia el redondeo, para no mover el número que se PINTA.
    /// </summary>
    public static int SemanaDesde(DateTime fecha, DateTime fechaBase)
    {
        var edadDias = (int)(fecha - fechaBase).TotalDays;
        return (int)Math.Ceiling((edadDias + 1.0) / 7);
    }

    /// <summary>
    /// Semana con la que buscar la fila de guía: la <b>semana de vida</b> del ave, contada desde el
    /// encasetamiento, que es el eje en el que están las dos tablas de guía.
    ///
    /// <para>Esto NO cambia la semana que el reporte muestra en pantalla —esa sigue siendo la
    /// relativa al inicio de producción, que es como el usuario cuenta la postura—: cambia
    /// únicamente contra qué fila de la guía se compara.</para>
    ///
    /// <para>Sin <paramref name="fechaEncaset"/> se cae a la semana relativa a producción en vez de
    /// lanzar: un lote sin encaset registrado sigue mostrando el reporte, apenas sin cruzar bien la
    /// guía. Hoy no hay ninguno así —los lotes de producción vivos tienen todos su encaset—, pero
    /// el reporte no es el lugar para descubrirlo.</para>
    /// </summary>
    public static int Resolver(
        DateTime fecha,
        DateTime fechaInicioProduccion,
        DateTime? fechaEncaset)
        => fechaEncaset.HasValue
            ? SemanaDesde(fecha, fechaEncaset.Value)
            : SemanaDesde(fecha, fechaInicioProduccion);
}
