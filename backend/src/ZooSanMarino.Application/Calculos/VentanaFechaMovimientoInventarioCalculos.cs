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
}
