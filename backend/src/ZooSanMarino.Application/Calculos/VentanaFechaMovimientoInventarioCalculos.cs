// src/ZooSanMarino.Application/Calculos/VentanaFechaMovimientoInventarioCalculos.cs
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Ventana de fechas admitida para los movimientos de inventario que se cargan A MANO por pantalla:
/// del día 1 del mes en curso hasta HOY.
///
/// <para>
/// Por qué existe: la fecha del movimiento era libre, así que se podían registrar entradas de meses
/// ya cerrados (y también fechas futuras, que nadie validaba). Pedido del usuario, 07-ago-2026.
/// </para>
///
/// <para>
/// ⚠️ Esta regla vale SOLO para la puerta manual (el controller de Gestión de Inventario). Los
/// mismos métodos del servicio los usan la carga masiva, las devoluciones al editar o borrar un
/// seguimiento diario y la anulación de gastos, que escriben con fecha histórica A PROPÓSITO.
/// Aplicar la ventana en el servicio rompería esos tres caminos.
/// </para>
/// </summary>
public static class VentanaFechaMovimientoInventarioCalculos
{
    /// <summary>
    /// Colombia, Ecuador y Panamá operan las tres en UTC−5 (sin horario de verano), así que el día
    /// operativo es el día UTC menos 5 horas. Sin esto, entre las 19:00 y la medianoche local el
    /// servidor ya estaría en el día —y el último día del mes, en el MES— siguiente, y rechazaría la
    /// fecha de hoy que el usuario ve en su pantalla.
    /// </summary>
    private const int HorasOffsetDiaOperativo = -5;

    /// <summary>Día operativo (UTC−5) correspondiente a un instante UTC.</summary>
    public static DateTime DiaOperativo(DateTimeOffset ahoraUtc) =>
        ahoraUtc.UtcDateTime.AddHours(HorasOffsetDiaOperativo).Date;

    /// <summary>Primer día admitido: el 1 del mes de <paramref name="hoy"/>.</summary>
    public static DateTime PrimerDiaAdmitido(DateTime hoy) => new(hoy.Year, hoy.Month, 1);

    /// <summary>
    /// ¿La fecha pedida cae dentro de la ventana? <c>null</c> siempre es válido: significa «sin fecha
    /// explícita», y el servicio le pone la hora actual (que por construcción está dentro).
    /// </summary>
    public static bool EsFechaPermitida(DateTime? fecha, DateTime hoy)
    {
        if (fecha is null) return true;
        var dia = fecha.Value.Date;
        return dia >= PrimerDiaAdmitido(hoy) && dia <= hoy.Date;
    }

    /// <summary>Mensaje único del rechazo, para que las cinco puertas manuales digan lo mismo.</summary>
    public static string MensajeFueraDeVentana(DateTime hoy)
    {
        var desde = PrimerDiaAdmitido(hoy).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var hasta = hoy.Date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return $"La fecha debe estar dentro del mes en curso: entre el {desde} y el {hasta}. " +
               "No se pueden registrar movimientos de meses anteriores ni con fecha futura.";
    }

    // ─── D4: excepción por alimento previo al encasetamiento ────────────────────
    //
    // POR QUÉ EXISTE
    // El alimento llega a la granja 2-7 días ANTES de que entren los pollitos y contabilidad necesita
    // la fecha REAL de llegada. Cuando el encaset cae a principio de mes, esa fecha real pertenece al
    // mes anterior y la ventana de arriba la rechaza — empujando de vuelta al workaround de fechar el
    // ingreso el primer día de consumo, que es exactamente lo que este trabajo viene a eliminar.
    //
    // La excepción NO abre el mes anterior: abre solo los días que la empresa ya declaró como ventana
    // de alimento previo (`companies.dias_alimento_previo_encaset`) alrededor de un encasetamiento
    // REAL de ESE galpón, con un tope duro de 30 días hacia atrás. Sin encaset que la justifique, la
    // regla del mes en curso sigue mandando.

    /// <summary>
    /// Tope duro hacia atrás de la excepción, contado desde HOY: sin él, un encaset viejo cargado
    /// tarde reabriría meses enteros por la puerta de atrás.
    /// </summary>
    public const int DiasMaximosRetroactividadEncaset = 30;

    /// <summary>
    /// ¿La fecha pedida es admisible considerando el alimento previo al encasetamiento del galpón?
    /// <para>
    /// Permitida si <see cref="EsFechaPermitida"/> ya la acepta (regla del mes en curso, intacta),
    /// <b>o</b> si se cumplen las tres condiciones de la excepción: no es futura y está dentro de los
    /// <see cref="DiasMaximosRetroactividadEncaset"/> días previos a hoy; el galpón tiene un
    /// encasetamiento a partir de esa fecha; y la fecha cae en
    /// <c>[encaset − diasVentanaEmpresa, encaset]</c>.
    /// </para>
    /// </summary>
    /// <param name="proximoEncasetEnGalpon">
    /// Encasetamiento más cercano del galpón con <c>fecha_encaset &gt;= fecha</c> (engorde o postura).
    /// <c>null</c> = el galpón no tiene ninguno ⇒ no hay excepción que aplicar.
    /// </param>
    /// <param name="diasVentanaEmpresa">
    /// <c>companies.dias_alimento_previo_encaset</c>. Los negativos se normalizan a 0, igual que en
    /// <see cref="AvisoFechaFueraDeCicloCalculos"/>.
    /// </param>
    public static bool EsFechaPermitidaConEncasetProximo(
        DateTime? fecha, DateTime hoy, DateTime? proximoEncasetEnGalpon, int diasVentanaEmpresa)
    {
        // La regla vigente manda: si ya la acepta (incluido el caso null), no se evalúa nada más.
        if (EsFechaPermitida(fecha, hoy)) return true;

        // Acá fecha nunca es null: EsFechaPermitida acepta null siempre.
        var dia = fecha!.Value.Date;

        // La excepción es para alimento que YA llegó. El futuro sigue prohibido por las dos vías.
        if (dia > hoy.Date) return false;
        if (dia < hoy.Date.AddDays(-DiasMaximosRetroactividadEncaset)) return false;

        if (proximoEncasetEnGalpon is not { } encaset) return false;

        var enc = encaset.Date;
        var dias = Math.Max(0, diasVentanaEmpresa);
        return dia >= enc.AddDays(-dias) && dia <= enc;
    }

    /// <summary>
    /// Rechazo cuando la fecha quedó fuera de TODA ventana: ni el mes en curso ni el alimento previo
    /// al encasetamiento del galpón. Distinto de <see cref="MensajeFueraDeVentana"/> para que la
    /// operación sepa que la excepción existe y por qué no aplicó en su caso.
    /// </summary>
    public static string MensajeFueraDeVentanaConEncaset(
        DateTime hoy, DateTime? proximoEncasetEnGalpon, int diasVentanaEmpresa)
    {
        var basico = MensajeFueraDeVentana(hoy);

        if (proximoEncasetEnGalpon is not { } encaset)
            return basico + " Se admite una fecha anterior solo cuando el alimento llegó antes de un " +
                   "encasetamiento de este galpón, y el galpón no tiene ninguno a partir de esa fecha.";

        var enc = encaset.Date;
        var dias = Math.Max(0, diasVentanaEmpresa);
        var desdeEnc = enc.AddDays(-dias).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var hastaEnc = enc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return basico + $" La única excepción es el alimento previo al encasetamiento del galpón " +
               $"({Fecha(enc)}): del {desdeEnc} al {hastaEnc}, y nunca más de " +
               $"{DiasMaximosRetroactividadEncaset} días hacia atrás.";
    }

    private static string Fecha(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
