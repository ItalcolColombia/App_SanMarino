// src/ZooSanMarino.Application/Calculos/VentanaFechaRegistroCalculos.cs
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Ventana de fechas admitida para los registros que se cargan A MANO por pantalla (movimientos de
/// inventario, movimientos de aves y de pollo engorde, traslados de aves y de huevos, gastos de
/// inventario), y el permiso que la destraba.
///
/// <para><b>La regla base</b> (aplica a todos, sin permiso): del <b>día 1 del mes en curso</b> o de
/// <b>hoy − <see cref="DiasRetroactividadBase"/> días</b>, el que llegue más atrás, hasta HOY.</para>
///
/// <para>
/// Por qué los 15 días y no sólo el mes: con la regla anterior —sólo el mes en curso— el día 1 de
/// cada mes nadie podía registrar lo que había llegado el día anterior, porque pertenecía al mes ya
/// cerrado. La operación no deja de existir el último día del mes; lo que pasaba era que el alimento
/// entrado el 31 se cargaba con fecha del 1, o directamente no se cargaba. Pedido del usuario,
/// 20-ago-2026.
/// </para>
///
/// <para>
/// <b>Es una ampliación estricta de la ventana anterior:</b> del día 16 en adelante manda el 1 del mes
/// (más ancho que 15 días) y del 1 al 15 manda <c>hoy − 15</c>. Ninguna fecha que se aceptaba antes se
/// rechaza ahora.
/// </para>
///
/// <para>
/// <b>El permiso <see cref="PermisoFechaRetroactiva"/></b> abre todo el pasado, sin tope. El
/// <b>futuro sigue cerrado para todos</b>, con permiso o sin él: una fecha posterior a hoy no es un
/// caso de negocio, es un error de tipeo (decisión del usuario, 20-ago-2026).
/// </para>
///
/// <para>
/// ⚠️ Esta regla vale SOLO para la puerta manual (los controllers). Los mismos métodos de los
/// services los usan la carga masiva, las devoluciones de alimento al editar o borrar un seguimiento
/// diario y la anulación de gastos, que escriben con fecha histórica A PROPÓSITO. Aplicar la ventana
/// en el service rompería esos tres caminos.
/// </para>
/// </summary>
public static class VentanaFechaRegistroCalculos
{
    /// <summary>
    /// Permiso que destraba el campo de fecha hacia atrás. Convención <c>modulo.accion</c> del
    /// catálogo (<c>permissions.key</c>); es transversal a los módulos, no de uno solo.
    /// </summary>
    public const string PermisoFechaRetroactiva = "registros.fecha_retroactiva";

    /// <summary>
    /// Días hacia atrás que la ventana base admite siempre, incluso cuando el mes recién empezó. Es
    /// global para todas las empresas —igual que la regla del mes en curso que amplía— y por eso vive
    /// acá y no en una columna de <c>companies</c>.
    /// </summary>
    public const int DiasRetroactividadBase = 15;

    /// <summary>
    /// Colombia, Ecuador y Panamá operan las tres en UTC−5 (sin horario de verano), así que el día
    /// operativo es el día UTC menos 5 horas. Sin esto, entre las 19:00 y la medianoche local el
    /// servidor ya estaría en el día siguiente y rechazaría la fecha de hoy que el usuario ve en su
    /// pantalla.
    /// </summary>
    private const int HorasOffsetDiaOperativo = -5;

    /// <summary>Día operativo (UTC−5) correspondiente a un instante UTC.</summary>
    public static DateTime DiaOperativo(DateTimeOffset ahoraUtc) =>
        ahoraUtc.UtcDateTime.AddHours(HorasOffsetDiaOperativo).Date;

    /// <summary>
    /// Primer día que la ventana base admite: el 1 del mes de <paramref name="hoy"/> o
    /// <c>hoy − <see cref="DiasRetroactividadBase"/></c>, el que llegue más atrás.
    /// </summary>
    public static DateTime PrimerDiaAdmitido(DateTime hoy)
    {
        var primeroDelMes = new DateTime(hoy.Year, hoy.Month, 1);
        var pisoRodante = hoy.Date.AddDays(-DiasRetroactividadBase);
        return pisoRodante < primeroDelMes ? pisoRodante : primeroDelMes;
    }

    /// <summary>
    /// ¿La fecha pedida cae dentro de lo admitido?
    /// <para>
    /// <c>null</c> siempre es válido: significa «sin fecha explícita», y el servicio le pone la hora
    /// actual (que por construcción está dentro).
    /// </para>
    /// </summary>
    /// <param name="puedeRetroactivar">
    /// El usuario tiene <see cref="PermisoFechaRetroactiva"/> ⇒ se admite cualquier fecha pasada. El
    /// futuro se rechaza igual.
    /// </param>
    public static bool EsFechaPermitida(DateTime? fecha, DateTime hoy, bool puedeRetroactivar = false)
    {
        if (fecha is null) return true;

        var dia = fecha.Value.Date;

        // El futuro no lo abre ningún permiso.
        if (dia > hoy.Date) return false;

        return puedeRetroactivar || dia >= PrimerDiaAdmitido(hoy);
    }

    /// <summary>
    /// Extremos que la pantalla puede ofrecer. <c>Min == null</c> significa <b>sin piso</b>: el
    /// usuario tiene el permiso y el datepicker no debe llevar atributo <c>min</c>.
    /// <c>Max</c> es SIEMPRE hoy.
    /// </summary>
    public static (DateTime? Min, DateTime Max) ExtremosVentana(DateTime hoy, bool puedeRetroactivar = false) =>
        (puedeRetroactivar ? null : PrimerDiaAdmitido(hoy), hoy.Date);

    /// <summary>
    /// Mensaje único del rechazo, para que todas las puertas manuales digan lo mismo.
    /// <para>
    /// Con el permiso, el único rechazo posible es por fecha futura, así que el texto no habla de una
    /// ventana que a ese usuario no lo limita.
    /// </para>
    /// </summary>
    public static string MensajeFueraDeVentana(DateTime hoy, bool puedeRetroactivar = false)
    {
        var hasta = Fecha(hoy.Date);

        if (puedeRetroactivar)
            return $"La fecha no puede ser posterior a hoy ({hasta}). El permiso de fecha " +
                   "retroactiva abre el pasado, no el futuro.";

        var desde = Fecha(PrimerDiaAdmitido(hoy));
        return $"La fecha debe estar entre el {desde} y el {hasta}: se admiten el mes en curso y los " +
               $"últimos {DiasRetroactividadBase} días. Para registrar una fecha anterior hace falta " +
               "el permiso de fecha retroactiva. Tampoco se admiten fechas futuras.";
    }

    /// <summary>Texto de ayuda del datepicker, con la misma regla que el rechazo.</summary>
    public static string TextoAyudaVentana(DateTime hoy, bool puedeRetroactivar = false)
    {
        if (puedeRetroactivar)
            return "Tenés permiso de fecha retroactiva: podés registrar cualquier fecha anterior. " +
                   $"El máximo sigue siendo hoy ({Fecha(hoy.Date)}).";

        return $"Se admite del {Fecha(PrimerDiaAdmitido(hoy))} al {Fecha(hoy.Date)} " +
               $"(el mes en curso y los últimos {DiasRetroactividadBase} días).";
    }

    /// <summary>
    /// ¿La lista de permisos de la sesión trae <see cref="PermisoFechaRetroactiva"/>? La comparación
    /// es <c>OrdinalIgnoreCase</c>, igual que el resto de los chequeos de permiso del repo (las keys
    /// se guardan en minúscula, pero el front las puede mandar con otra capitalización).
    /// </summary>
    public static bool TienePermisoRetroactivo(IEnumerable<string>? permisos) =>
        permisos is not null &&
        permisos.Any(p => string.Equals(p, PermisoFechaRetroactiva, StringComparison.OrdinalIgnoreCase));

    private static string Fecha(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
