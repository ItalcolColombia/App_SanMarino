// Cálculo PURO de la captura de huevos en LEVANTE (semana 14+) y de su arrastre al primer
// registro de PRODUCCIÓN al liquidar el levante.
// Sin EF, sin estado, sin I/O: solo aritmética de la clasificadora, gate de semana, suma/delta
// y (des)serialización de la marca que viaja en seguimiento_diario_produccion.metadata.
using System.Text.Json;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Clasificación de huevos de un día con las 11 categorías de la clasificadora fija (la misma que
/// usa producción). <see cref="Incubables"/> y <see cref="Totales"/> son DERIVADOS — nunca se
/// almacenan por separado en este tipo, para que no puedan quedar descuadrados:
/// <code>
/// Incubables = Limpio + Tratado
/// Totales    = Incubables + Sucio + Deforme + Blanco + DobleYema + Piso + Pequeno + Roto + Desecho + Otro
/// </code>
/// Es exactamente la aritmética del modal de producción
/// (<c>modal-seguimiento-diario.component.ts → totalesClasificadoraFija()</c>).
/// </summary>
public readonly record struct HuevosClasificacion(
    int Limpio = 0,
    int Tratado = 0,
    int Sucio = 0,
    int Deforme = 0,
    int Blanco = 0,
    int DobleYema = 0,
    int Piso = 0,
    int Pequeno = 0,
    int Roto = 0,
    int Desecho = 0,
    int Otro = 0)
{
    /// <summary>Clasificación en cero (todas las categorías a 0).</summary>
    public static readonly HuevosClasificacion Cero = default;

    /// <summary>Huevos incubables = Limpio + Tratado (va a <c>huevo_inc</c>).</summary>
    public int Incubables => Limpio + Tratado;

    /// <summary>Huevos totales = Incubables + las 9 no incubables (va a <c>huevo_tot</c>).</summary>
    public int Totales =>
        Incubables + Sucio + Deforme + Blanco + DobleYema + Piso + Pequeno + Roto + Desecho + Otro;

    /// <summary>true si las 11 categorías están en 0 (nada que registrar ni que arrastrar).</summary>
    public bool EsCero => this == Cero;

    /// <summary>
    /// true si alguna categoría es positiva. Es el disparador del gate de semana 14: registrar
    /// ceros nunca se bloquea (un seguimiento normal de semana 3 manda ceros y debe pasar).
    /// </summary>
    public bool AlgunoPositivo =>
        Limpio > 0 || Tratado > 0 || Sucio > 0 || Deforme > 0 || Blanco > 0 || DobleYema > 0 ||
        Piso > 0 || Pequeno > 0 || Roto > 0 || Desecho > 0 || Otro > 0;
}

/// <summary>
/// Reglas puras de los huevos capturados en el Seguimiento Diario de LEVANTE y de su arrastre a
/// PRODUCCIÓN al liquidar.
/// <para>
/// Contexto: en postura las aves empiezan a poner antes de pasar a producción. Desde la
/// <b>semana 14</b> de vida el seguimiento de levante captura la clasificación de huevos igual que
/// producción; al liquidar el levante, el acumulado se arrastra al PRIMER registro de producción
/// (la fecha de inicio de producción) y, si el usuario registra producción ese mismo día, los
/// huevos se SUMAN sobre esa fila en vez de rechazar el alta.
/// </para>
/// <para>
/// El arrastre es una <b>proyección recalculable</b>: los huevos originales nunca se borran ni se
/// marcan en levante, que sigue siendo la fuente de verdad. La marca
/// <see cref="MetadataKeyArrastre"/> guarda lo ya aplicado para que re-ejecutar el arrastre sume
/// solo el <see cref="Delta"/> (idempotencia).
/// </para>
/// </summary>
public static class HuevosLevanteCalculos
{
    /// <summary>
    /// Semana de vida (1-based) a partir de la cual el levante puede registrar huevos.
    /// Semana 14 ⟺ <c>dias >= 91</c> (13 × 7), porque el día del encaset es la semana 1.
    /// </summary>
    public const int SemanaMinimaHuevosLevante = 14;

    /// <summary>Clave de la marca de arrastre dentro del jsonb <c>metadata</c> del seguimiento de producción.</summary>
    public const string MetadataKeyArrastre = "arrastreHuevosLevante";

    /// <summary>Versión del shape de la marca (por si hay que migrarla más adelante).</summary>
    public const int VersionMarcaArrastre = 1;

    /// <summary>
    /// Propiedad de la marca que indica que el usuario YA registró el seguimiento de ese día
    /// (⇒ la ventana de merge está cerrada y vuelve a aplicar el 400 por duplicado).
    /// </summary>
    private const string PropSeguimientoRegistrado = "seguimientoRegistrado";

    /// <summary>
    /// Semana de vida (1-based) del lote a la fecha indicada. Idéntica a la fórmula canónica
    /// <c>MovimientoAvesCalculos.SemanaDesdeEncaset</c> y al SQL de las fns de indicadores
    /// (<c>floor((fecha - encaset)/7) + 1</c>): trunca ambas fechas a día y divide entero por 7.
    /// <b>El día del encaset es la semana 1.</b>
    /// </summary>
    public static int SemanaVida(DateTime fecha, DateTime fechaEncaset)
    {
        var diasDesdeEncaset = (fecha.Date - fechaEncaset.Date).Days;
        return (diasDesdeEncaset / 7) + 1;
    }

    /// <summary>
    /// Gate de captura: true solo si el lote tiene fecha de encaset conocida y el registro cae en la
    /// semana <see cref="SemanaMinimaHuevosLevante"/> o posterior.
    /// <b>Fail-closed</b>: sin fecha de encaset (o con fecha de registro anterior al encaset) no se
    /// permiten huevos — es preferible bloquear que aceptar un dato que ningún reporte podrá ubicar.
    /// </summary>
    public static bool PermiteHuevos(DateTime? fechaEncaset, DateTime fechaRegistro)
    {
        if (!fechaEncaset.HasValue) return false;
        if ((fechaRegistro.Date - fechaEncaset.Value.Date).Days < 0) return false;
        return SemanaVida(fechaRegistro, fechaEncaset.Value) >= SemanaMinimaHuevosLevante;
    }

    /// <summary>Suma dos clasificaciones categoría por categoría.</summary>
    public static HuevosClasificacion Sumar(HuevosClasificacion a, HuevosClasificacion b) => new(
        Limpio: a.Limpio + b.Limpio,
        Tratado: a.Tratado + b.Tratado,
        Sucio: a.Sucio + b.Sucio,
        Deforme: a.Deforme + b.Deforme,
        Blanco: a.Blanco + b.Blanco,
        DobleYema: a.DobleYema + b.DobleYema,
        Piso: a.Piso + b.Piso,
        Pequeno: a.Pequeno + b.Pequeno,
        Roto: a.Roto + b.Roto,
        Desecho: a.Desecho + b.Desecho,
        Otro: a.Otro + b.Otro);

    /// <summary>
    /// Diferencia <paramref name="nuevo"/> − <paramref name="aplicado"/> categoría por categoría.
    /// Es lo que falta arrastrar: cero ⇒ no hay nada que hacer (idempotente).
    /// <b>No se clampea a 0</b> a propósito: si en levante se borraron huevos, el delta negativo es
    /// el que deja el espejo cuadrado.
    /// </summary>
    public static HuevosClasificacion Delta(HuevosClasificacion nuevo, HuevosClasificacion aplicado) => new(
        Limpio: nuevo.Limpio - aplicado.Limpio,
        Tratado: nuevo.Tratado - aplicado.Tratado,
        Sucio: nuevo.Sucio - aplicado.Sucio,
        Deforme: nuevo.Deforme - aplicado.Deforme,
        Blanco: nuevo.Blanco - aplicado.Blanco,
        DobleYema: nuevo.DobleYema - aplicado.DobleYema,
        Piso: nuevo.Piso - aplicado.Piso,
        Pequeno: nuevo.Pequeno - aplicado.Pequeno,
        Roto: nuevo.Roto - aplicado.Roto,
        Desecho: nuevo.Desecho - aplicado.Desecho,
        Otro: nuevo.Otro - aplicado.Otro);

    /// <summary>
    /// Peso promedio del huevo ponderado por la cantidad de huevos de cada día. Devuelve
    /// <c>null</c> si no hay días con huevos o si ninguno tiene peso informado (sin división por
    /// cero): el promedio simple distorsionaría el dato de un día con 10 huevos frente a otro con
    /// 10.000.
    /// </summary>
    public static double? PesoHuevoPonderado(IEnumerable<(double? Peso, int Totales)> dias)
    {
        double sumaPesoPorHuevo = 0;
        long sumaHuevos = 0;

        foreach (var (peso, totales) in dias)
        {
            if (!peso.HasValue || totales <= 0) continue;
            sumaPesoPorHuevo += peso.Value * totales;
            sumaHuevos += totales;
        }

        if (sumaHuevos <= 0) return null;
        return sumaPesoPorHuevo / sumaHuevos;
    }

    /// <summary>
    /// true si el metadata trae la marca de arrastre. Es el discriminador que habilita la SUMA en
    /// <c>ProduccionService.CrearSeguimientoAsync</c>: sin marca, el alta duplicada sigue
    /// rechazándose con el mensaje de siempre.
    /// </summary>
    public static bool TieneMarcaArrastre(JsonDocument? metadata) =>
        metadata is not null &&
        metadata.RootElement.ValueKind == JsonValueKind.Object &&
        metadata.RootElement.TryGetProperty(MetadataKeyArrastre, out var marca) &&
        marca.ValueKind == JsonValueKind.Object;

    /// <summary>
    /// Lee del metadata la clasificación ya arrastrada (<c>arrastreHuevosLevante.aplicado</c>).
    /// Devuelve <see cref="HuevosClasificacion.Cero"/> si no hay marca o está mal formada, de modo
    /// que el delta contra el acumulado real arrastre todo (comportamiento seguro).
    /// </summary>
    public static HuevosClasificacion LeerArrastreAplicado(JsonDocument? metadata)
    {
        if (!TieneMarcaArrastre(metadata)) return HuevosClasificacion.Cero;

        var marca = metadata!.RootElement.GetProperty(MetadataKeyArrastre);
        if (!marca.TryGetProperty("aplicado", out var ap) || ap.ValueKind != JsonValueKind.Object)
            return HuevosClasificacion.Cero;

        return new HuevosClasificacion(
            Limpio: LeerInt(ap, "huevoLimpio"),
            Tratado: LeerInt(ap, "huevoTratado"),
            Sucio: LeerInt(ap, "huevoSucio"),
            Deforme: LeerInt(ap, "huevoDeforme"),
            Blanco: LeerInt(ap, "huevoBlanco"),
            DobleYema: LeerInt(ap, "huevoDobleYema"),
            Piso: LeerInt(ap, "huevoPiso"),
            Pequeno: LeerInt(ap, "huevoPequeno"),
            Roto: LeerInt(ap, "huevoRoto"),
            Desecho: LeerInt(ap, "huevoDesecho"),
            Otro: LeerInt(ap, "huevoOtro"));
    }

    /// <summary>
    /// Escribe/reemplaza la marca de arrastre dentro del jsonb <c>metadata</c>, CONSERVANDO verbatim
    /// todas las demás claves ya presentes (<c>itemsHembras</c>, <c>consumoOriginal*</c>,
    /// <c>huevoItems</c>…). <paramref name="aplicado"/> es el acumulado TOTAL ya volcado (no el
    /// delta), para que la próxima ejecución calcule el delta correcto.
    /// </summary>
    public static JsonDocument EscribirMarcaArrastre(
        JsonDocument? metadata,
        HuevosClasificacion aplicado,
        int lotePosturaLevanteId,
        DateTime fechaArrastre)
    {
        var dict = new Dictionary<string, object?>();

        if (metadata != null && metadata.RootElement.ValueKind == JsonValueKind.Object)
            foreach (var prop in metadata.RootElement.EnumerateObject())
                if (!string.Equals(prop.Name, MetadataKeyArrastre, StringComparison.Ordinal))
                    dict[prop.Name] = prop.Value.Clone();

        var marca = new Dictionary<string, object?>
        {
            ["lotePosturaLevanteId"] = lotePosturaLevanteId,
            ["fecha"] = fechaArrastre.ToString("yyyy-MM-dd"),
            ["version"] = VersionMarcaArrastre,
            ["aplicado"] = AplicadoAJson(aplicado)
        };
        // Si YA había una marca con la ventana cerrada, se mantiene cerrada: re-arrastrar no debe
        // habilitar un segundo registro del mismo día. Sin marca previa (primer arrastre) la ventana
        // nace ABIERTA, que es justo el caso que pidió el negocio.
        if (TieneMarcaArrastre(metadata) && !PermiteMergeSeguimiento(metadata))
            marca[PropSeguimientoRegistrado] = true;
        dict[MetadataKeyArrastre] = marca;

        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    /// <summary>
    /// ¿La fila admite que el seguimiento del día se SUME sobre ella?
    /// <para>
    /// Sólo si tiene la marca de arrastre y <b>todavía no</b> se registró el seguimiento de ese día
    /// (<c>seguimientoRegistrado != true</c>). Así la regla «un registro por día» se conserva: la
    /// ventana de merge se abre una única vez, justo para el día de la liquidación, y se cierra en
    /// cuanto el usuario registra. Un segundo alta del mismo día vuelve a dar el 400 de siempre.
    /// </para>
    /// </summary>
    public static bool PermiteMergeSeguimiento(JsonDocument? metadata)
    {
        if (!TieneMarcaArrastre(metadata)) return false;
        var marca = metadata!.RootElement.GetProperty(MetadataKeyArrastre);
        return !(marca.TryGetProperty(PropSeguimientoRegistrado, out var v) &&
                 v.ValueKind == JsonValueKind.True);
    }

    /// <summary>
    /// Cierra la ventana de merge: deja <c>seguimientoRegistrado = true</c> dentro de la marca,
    /// conservando el resto del metadata (y el <c>aplicado</c>, que sigue mandando para el delta).
    /// </summary>
    public static JsonDocument? MarcarSeguimientoRegistrado(JsonDocument? metadata)
    {
        if (!TieneMarcaArrastre(metadata)) return metadata;

        var dict = new Dictionary<string, object?>();
        foreach (var prop in metadata!.RootElement.EnumerateObject())
            if (!string.Equals(prop.Name, MetadataKeyArrastre, StringComparison.Ordinal))
                dict[prop.Name] = prop.Value.Clone();

        var marcaDict = new Dictionary<string, object?>();
        foreach (var prop in metadata.RootElement.GetProperty(MetadataKeyArrastre).EnumerateObject())
            if (!string.Equals(prop.Name, PropSeguimientoRegistrado, StringComparison.Ordinal))
                marcaDict[prop.Name] = prop.Value.Clone();
        marcaDict[PropSeguimientoRegistrado] = true;

        dict[MetadataKeyArrastre] = marcaDict;
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    /// <summary>
    /// Copia la marca de arrastre de <paramref name="origen"/> a <paramref name="destino"/> verbatim,
    /// conservando el resto de <paramref name="destino"/>.
    /// <para>
    /// Necesario en el MERGE del seguimiento de producción: el metadata se reconstruye desde el
    /// request (ítems, consumo original…) y sin esto la marca se perdería, con lo cual el siguiente
    /// arrastre volvería a sumar todo desde cero (dejaría de ser idempotente).
    /// </para>
    /// Si <paramref name="origen"/> no tiene marca, devuelve <paramref name="destino"/> sin cambios.
    /// </summary>
    public static JsonDocument? CopiarMarcaArrastre(JsonDocument? destino, JsonDocument? origen)
    {
        if (!TieneMarcaArrastre(origen)) return destino;

        var dict = new Dictionary<string, object?>();
        if (destino != null && destino.RootElement.ValueKind == JsonValueKind.Object)
            foreach (var prop in destino.RootElement.EnumerateObject())
                if (!string.Equals(prop.Name, MetadataKeyArrastre, StringComparison.Ordinal))
                    dict[prop.Name] = prop.Value.Clone();

        dict[MetadataKeyArrastre] = origen!.RootElement.GetProperty(MetadataKeyArrastre).Clone();
        return JsonDocument.Parse(JsonSerializer.Serialize(dict));
    }

    /// <summary>
    /// Shape camelCase de <c>aplicado</c>: las 11 categorías más los derivados
    /// <c>huevoTot</c>/<c>huevoInc</c> (redundantes a propósito, para poder auditar el arrastre
    /// leyendo el jsonb sin recalcular nada).
    /// </summary>
    private static Dictionary<string, object?> AplicadoAJson(HuevosClasificacion c) => new()
    {
        ["huevoLimpio"] = c.Limpio,
        ["huevoTratado"] = c.Tratado,
        ["huevoSucio"] = c.Sucio,
        ["huevoDeforme"] = c.Deforme,
        ["huevoBlanco"] = c.Blanco,
        ["huevoDobleYema"] = c.DobleYema,
        ["huevoPiso"] = c.Piso,
        ["huevoPequeno"] = c.Pequeno,
        ["huevoRoto"] = c.Roto,
        ["huevoDesecho"] = c.Desecho,
        ["huevoOtro"] = c.Otro,
        ["huevoInc"] = c.Incubables,
        ["huevoTot"] = c.Totales
    };

    private static int LeerInt(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) ? n : 0;
}
