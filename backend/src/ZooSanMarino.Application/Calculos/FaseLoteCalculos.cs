// src/ZooSanMarino.Application/Calculos/FaseLoteCalculos.cs
using System.Linq.Expressions;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Fase de un lote de postura: <c>Levante</c> o <c>Produccion</c>.
///
/// <para>
/// Al crear un lote la fase se DERIVA de las semanas transcurridas desde el encasetamiento
/// (≥ 26 ⇒ Producción). Eso es correcto para un lote que se da de alta al día, pero rompe la
/// carga de histórico: un lote encasetado hace un año nace en «Producción» y los dos reportes de
/// levante lo filtran (<c>lpl.Etapa == "Levante"</c> en el reporte técnico y
/// <c>l.Fase != "Produccion"</c> en el semanal), así que el dato entra por carga masiva y el
/// reporte no lo ve nunca.
/// </para>
///
/// <para>
/// Por eso la fase pasa a ser un dato OPCIONAL de entrada: si quien crea el lote la indica, manda
/// la indicada; si no, se conserva exactamente la derivación anterior. Es aditivo — un cliente que
/// no envía nada obtiene el mismo resultado de siempre.
/// </para>
/// </summary>
public static class FaseLoteCalculos
{
    public const string Levante = "Levante";
    public const string Produccion = "Produccion";

    /// <summary>Semanas a partir de las cuales un lote se considera en producción.</summary>
    public const int SemanasParaProduccion = 26;

    /// <summary>Las dos fases válidas, para validar la entrada.</summary>
    public static IReadOnlyList<string> Validas { get; } = new[] { Levante, Produccion };

    /// <summary>
    /// Normaliza la fase indicada por el usuario. Devuelve <c>null</c> si viene vacía (⇒ derivar)
    /// y lanza si trae un valor que no es ninguna de las dos fases.
    /// </summary>
    /// <exception cref="ArgumentException">La fase no es <c>Levante</c> ni <c>Produccion</c>.</exception>
    public static string? NormalizarFaseIndicada(string? fase)
    {
        if (string.IsNullOrWhiteSpace(fase)) return null;
        var limpia = fase.Trim();
        foreach (var v in Validas)
            if (string.Equals(limpia, v, StringComparison.OrdinalIgnoreCase))
                return v;
        throw new ArgumentException(
            $"Fase '{fase}' inválida. Usá '{Levante}' o '{Produccion}' (o dejala vacía para que se derive del encasetamiento).");
    }

    /// <summary>Derivación histórica: ≥ 26 semanas desde el encaset ⇒ Producción.</summary>
    public static string DerivarPorEdad(int semanasDesdeEncaset) =>
        semanasDesdeEncaset >= SemanasParaProduccion ? Produccion : Levante;

    /// <summary>
    /// Fase con la que nace el lote: la indicada si vino, y si no la derivada por edad.
    /// Comportamiento previo byte a byte cuando <paramref name="faseIndicada"/> es null/vacía.
    /// </summary>
    public static string Resolver(string? faseIndicada, int semanasDesdeEncaset) =>
        NormalizarFaseIndicada(faseIndicada) ?? DerivarPorEdad(semanasDesdeEncaset);

    /// <summary>
    /// ¿Este lote es un registro de LEVANTE, a los efectos de los reportes de levante?
    ///
    /// <para>
    /// La fase NO sirve para responderlo por sí sola: sólo toma el valor <c>Produccion</c> en la
    /// derivación por edad al crear el lote (<see cref="DerivarPorEdad"/>), y el paso real
    /// levante → producción no la actualiza — crea una fila en <c>lote_postura_produccion</c> y un
    /// lote hijo con <c>LotePadreId</c>. Testigo: los lotes K345A/B siguen en fase <c>Levante</c>
    /// con su producción P-K345A/B ya andando.
    /// </para>
    ///
    /// <para>
    /// Filtrar por <c>fase == "Levante"</c> no separa levante de producción: esconde exactamente los
    /// lotes cargados con historia (encaset de hace más de 26 semanas), que nacen en
    /// <c>Produccion</c> aunque toda su data sea de levante. El único registro que legítimamente
    /// NO es levante es el lote hijo de producción (fase <c>Produccion</c> <b>y</b> con padre),
    /// el mismo criterio que ya usa el listado de lotes.
    /// </para>
    /// </summary>
    public static bool EsRegistroLevante(string? fase, int? lotePadreId) =>
        !(fase == Produccion && lotePadreId != null);

    /// <summary>
    /// <see cref="EsRegistroLevante"/> como expresión, para empujar el filtro a la BD en vez de
    /// traer los lotes y filtrarlos en memoria. Es la misma regla: <c>FaseLoteCalculosTests</c>
    /// verifica que la expresión compilada y el método coinciden en todas las combinaciones.
    /// </summary>
    public static Expression<Func<Lote, bool>> LoteEsRegistroLevante =>
        l => !(l.Fase == Produccion && l.LotePadreId != null);

    /// <summary>Estado de cierre que marca el fin de la etapa de levante.</summary>
    public const string CierreCerrado = "Cerrado";

    /// <summary>
    /// Fase que se le MUESTRA al usuario en el listado de lotes, resuelta por el ESTADO REAL del
    /// lote y no por su edad.
    ///
    /// <para>
    /// <b>Por qué no vale la edad.</b> La pantalla derivaba la fase de las semanas transcurridas
    /// desde el encasetamiento (<see cref="DerivarPorEdad"/>), que es correcto para un lote dado de
    /// alta al día y falso para todo lote cargado con historia: su encaset es viejo, así que aparece
    /// en «Producción» aunque nunca haya pasado a producción. Medido en la base: 8 de los 16 lotes
    /// de Sanmarino decían «Producción» sin tener el levante cerrado ni una sola fila de producción
    /// — son justamente los cargados con historia (A374, S369). Es el mismo defecto que
    /// <see cref="EsRegistroLevante"/> ya corrigió del lado de los reportes.
    /// </para>
    ///
    /// <para>
    /// <b>La regla.</b> Un lote pasa a Producción cuando ocurrieron LAS DOS COSAS: su levante se
    /// cerró y existe el lote de producción. Con una sola no alcanza — un levante cerrado sin
    /// producción sigue siendo un levante (terminado), y una producción creada con el levante
    /// abierto es un dato a medio cargar. Mientras falte cualquiera de las dos, la palabra
    /// «Producción» no aparece en pantalla.
    /// </para>
    /// </summary>
    /// <param name="levanteCerrado"><c>lote_postura_levante.estado_cierre == "Cerrado"</c>.</param>
    /// <param name="tieneProduccion">
    /// Existe una fila viva en <c>lote_postura_produccion</c> para el lote. Es la ÚNICA prueba de
    /// que la producción existe: los criterios por <c>fase</c> del lote hijo dan cero en la base
    /// real, porque el paso a producción no escribe esa columna (ver <see cref="EsRegistroLevante"/>).
    /// </param>
    public static string ResolverFaseVisible(bool levanteCerrado, bool tieneProduccion) =>
        levanteCerrado && tieneProduccion ? Produccion : Levante;

    /// <summary>
    /// <see cref="ResolverFaseVisible"/> a partir del texto crudo del estado de cierre, tolerante a
    /// mayúsculas, espacios y <c>null</c> (un lote sin fila de levante cuenta como abierto).
    /// </summary>
    public static string ResolverFaseVisible(string? estadoCierreLevante, bool tieneProduccion) =>
        ResolverFaseVisible(EsCierreCerrado(estadoCierreLevante), tieneProduccion);

    /// <summary>¿El texto del estado de cierre significa «cerrado»? Null/vacío ⇒ no.</summary>
    public static bool EsCierreCerrado(string? estadoCierre) =>
        string.Equals(estadoCierre?.Trim(), CierreCerrado, StringComparison.OrdinalIgnoreCase);
}
