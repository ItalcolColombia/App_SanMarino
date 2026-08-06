namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculos puros (sin EF ni estado) de las COHORTES de aves de un lote y de la habilitación del
/// traslado CROSS-ETAPA (Levante → Producción).
/// <para>
/// Una cohorte es un grupo de aves que ingresó a un lote por traslado conservando la edad que traía
/// de su lote de origen: su edad se cuenta SIEMPRE desde la <c>fecha_encaset</c> del lote ORIGEN
/// (<c>fecha_encaset_cohorte</c>), no desde la fecha de ingreso ni desde el encaset del receptor.
/// </para>
/// La fórmula de semanas NO se duplica: delega en
/// <see cref="MovimientoAvesCalculos.SemanaDesdeEncaset(DateTime, DateTime)"/> (división entera por
/// 7 + 1 ⇒ el día del encasetamiento es la semana 1).
/// </summary>
public static class LoteCohortesCalculos
{
    /// <summary>Etapa "Levante" tal como viaja en los DTOs de traslado.</summary>
    public const string EtapaLevante = "Levante";

    /// <summary>Etapa "Produccion" tal como viaja en los DTOs de traslado.</summary>
    public const string EtapaProduccion = "Produccion";

    /// <summary>
    /// Edad en DÍAS de la cohorte a la <paramref name="fecha"/> indicada, contada desde
    /// <paramref name="fechaEncaset"/>. Día del encasetamiento = 0. Se aplica <b>clamp a 0</b>
    /// cuando la fecha consultada es anterior al encasetamiento (edad negativa carece de sentido).
    /// </summary>
    public static int EdadDias(DateOnly fechaEncaset, DateOnly fecha)
    {
        var dias = fecha.DayNumber - fechaEncaset.DayNumber;
        return dias > 0 ? dias : 0;
    }

    /// <summary>
    /// Edad en SEMANAS (1-based) de la cohorte a la <paramref name="fecha"/> indicada: el día del
    /// encasetamiento es la semana 1. Usa la misma aritmética que el resto del sistema
    /// (<see cref="MovimientoAvesCalculos.SemanaDesdeEncaset(DateTime, DateTime)"/>) sobre los días
    /// ya clampados por <see cref="EdadDias"/>.
    /// </summary>
    public static int EdadSemanas(DateOnly fechaEncaset, DateOnly fecha)
    {
        var encaset = fechaEncaset.ToDateTime(TimeOnly.MinValue);
        var fechaClampada = encaset.AddDays(EdadDias(fechaEncaset, fecha));
        return MovimientoAvesCalculos.SemanaDesdeEncaset(fechaClampada, encaset);
    }

    /// <summary>
    /// Ubicación de origen de una cohorte en una sola línea legible: <c>"Granja · Núcleo · Galpón"</c>,
    /// omitiendo las partes que no se conocen. Devuelve <c>null</c> cuando no hay ningún dato, para que la
    /// UI muestre su propio guion en vez de una cadena vacía.
    /// <para>
    /// Vive acá (y no en cada service) porque las dos líneas —postura y engorde— pintan la misma columna y
    /// el formato tiene que ser idéntico en ambas.
    /// </para>
    /// </summary>
    public static string? DescribirUbicacionOrigen(string? granja, string? nucleo, string? galpon)
    {
        var partes = new[] { granja, nucleo, galpon }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim())
            .ToArray();

        return partes.Length == 0 ? null : string.Join(" · ", partes);
    }

    /// <summary>
    /// Techo de aves vendibles de un lote de ENGORDE que además RECIBIÓ aves por traslado.
    /// <para>
    /// <b>Por qué existe:</b> el techo de la auditoría de ventas parte del registro <c>Inicio</c> de
    /// <c>historial_lote_pollo_engorde</c>, que solo se escribe al CREAR el lote. Un lote que recibe aves
    /// por traslado sí ve subir su maestro (<c>hembras_l/machos_l</c>) pero no su <c>Inicio</c>, así que
    /// vender esas aves se reportaba como sobreventa aunque existieran físicamente. Sumar las cohortes
    /// recibidas alinea el techo con el maestro.
    /// </para>
    /// <para>
    /// <b>Retrocompatible por construcción:</b> un lote sin cohortes recibe 0 en los tres sumandos y
    /// devuelve el <c>Inicio</c> tal cual — el número de todos los lotes actuales no se mueve.
    /// </para>
    /// Las cohortes anuladas (soft-delete) no deben llegar acá: el llamador las filtra, igual que hace
    /// el resto del sistema con el histórico anulado.
    /// </summary>
    /// <param name="inicio">Aves del registro <c>Inicio</c> del lote, por sexo.</param>
    /// <param name="recibidas">Suma de las cohortes VIGENTES recibidas por el lote, por sexo.</param>
    public static (int Hembras, int Machos, int Mixtas) BaselineConCohortes(
        (int Hembras, int Machos, int Mixtas) inicio,
        (int Hembras, int Machos, int Mixtas) recibidas)
    {
        // Clamp a 0 en cada sumando: un dato negativo en la BD no debe inflar ni desinflar el techo.
        static int NoNegativo(int v) => v > 0 ? v : 0;

        return (
            NoNegativo(inicio.Hembras) + NoNegativo(recibidas.Hembras),
            NoNegativo(inicio.Machos) + NoNegativo(recibidas.Machos),
            NoNegativo(inicio.Mixtas) + NoNegativo(recibidas.Mixtas));
    }

    /// <summary>
    /// Aves PROPIAS de un lote = saldo actual − aves recibidas por traslado (cohortes vigentes).
    /// <para>
    /// Sirve para que la tabla «Edades en el lote» pueda cuadrar <i>propias + recibidas = saldo</i>.
    /// </para>
    /// <para>
    /// ⚠️ <b>Es una aproximación y se muestra como tal:</b> la mortalidad y la selección se registran por
    /// LOTE, no por cohorte, así que las bajas posteriores al ingreso se le descuentan implícitamente a las
    /// propias. Con clamp a 0 para que un lote donde ya murieron más aves de las propias no muestre
    /// negativos. Repartir las bajas entre cohortes exigiría una política de imputación
    /// (proporcional / FIFO / manual) que es una decisión de negocio, no un cálculo.
    /// </para>
    /// </summary>
    public static int PropiasDelLote(int saldoActual, int recibidasVigentes)
    {
        var propias = saldoActual - recibidasVigentes;
        return propias > 0 ? propias : 0;
    }

    /// <summary>
    /// <c>true</c> si origen y destino son la MISMA etapa (comparación case-insensitive, idéntica a
    /// la que hacía el servicio de traslados inline).
    /// </summary>
    public static bool EsMismaEtapa(string? tipoOrigen, string? tipoDestino) =>
        string.Equals(tipoOrigen, tipoDestino, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// <c>true</c> cuando la etapa indicada es Levante. Cualquier otro valor (incluye null) se
    /// considera Producción — misma discriminación que usa el servicio de traslados.
    /// </summary>
    public static bool EsLevante(string? tipo) =>
        string.Equals(tipo, EtapaLevante, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// ¿Se puede ejecutar el traslado con estas etapas?
    /// <list type="bullet">
    /// <item>Misma etapa → SIEMPRE permitido (comportamiento histórico intacto).</item>
    /// <item>Etapas distintas → solo si la empresa lo permite (<paramref name="companyPermite"/>)
    /// y EXCLUSIVAMENTE en el sentido Levante → Producción.</item>
    /// <item>Producción → Levante → NUNCA (ni con el flag activo).</item>
    /// </list>
    /// </summary>
    public static bool PuedeTrasladarCrossEtapa(bool companyPermite, string? tipoOrigen, string? tipoDestino)
    {
        if (EsMismaEtapa(tipoOrigen, tipoDestino)) return true;
        if (!companyPermite) return false;
        return EsLevante(tipoOrigen) && !EsLevante(tipoDestino);
    }

    /// <summary>
    /// Mensaje EXACTO del bloqueo cross-etapa (se conserva textual: el front y los smokes lo
    /// muestran tal cual desde que existe el traslado desde seguimiento diario).
    /// </summary>
    public static string MensajeCrossEtapaBloqueado(string? tipoOrigen, string? tipoDestino) =>
        $"No se permite cross-phase: origen={tipoOrigen} no coincide con destino={tipoDestino}. " +
        "Sólo se puede trasladar dentro de la misma etapa (Levante→Levante o Producción→Producción).";
}
