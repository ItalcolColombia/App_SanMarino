// src/ZooSanMarino.Application/Calculos/FechasPuras.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Normalización de "fechas puras" (la hora no tiene significado de negocio: fecha de encaset,
/// fecha de seguimiento diario, etc.).
///
/// Problema que resuelve: las columnas son <c>timestamp with time zone</c> y el front histórico
/// enviaba la fecha como medianoche UTC (<c>2026-07-21T00:00:00Z</c>). Al leerla, Npgsql (modo
/// legacy) la devuelve convertida a la zona del servidor y el navegador la muestra en UTC-5,
/// restando un día. Anclar el instante a MEDIODÍA UTC deja la fecha a ≥12 h de cualquier
/// medianoche: ni la serialización con offset ni la visualización local pueden cambiar el día.
/// </summary>
public static class FechasPuras
{
    /// <summary>
    /// Devuelve la fecha intencional del valor recibido anclada a las 12:00 UTC.
    /// - <see cref="DateTimeKind.Unspecified"/>: se toma la fecha literal digitada.
    /// - <see cref="DateTimeKind.Utc"/>: se toma la fecha del instante en UTC (el front ancla
    ///   dentro del día UTC intencional: mediodía UTC o, legado, medianoche UTC / mediodía local).
    /// - <see cref="DateTimeKind.Local"/>: se convierte a UTC y se toma esa fecha.
    /// Idempotente: aplicar dos veces devuelve el mismo valor.
    /// </summary>
    public static DateTime AnclarMediodiaUtc(DateTime valor)
    {
        var fecha = valor.Kind switch
        {
            DateTimeKind.Utc         => valor.Date,
            DateTimeKind.Local       => valor.ToUniversalTime().Date,
            _                        => valor.Date
        };
        return DateTime.SpecifyKind(fecha, DateTimeKind.Utc).AddHours(12);
    }

    /// <summary>
    /// Rango semiabierto <c>[desde, hasta)</c> que cubre el DÍA UTC del valor recibido.
    /// <para>
    /// Sirve para comparar "el mismo día" contra columnas <c>timestamp with time zone</c> sin
    /// depender de la zona horaria de la sesión de la BD. Es el reemplazo correcto de
    /// <c>x.Fecha.Date == fecha.Date</c> en LINQ: EF traduce esa expresión a
    /// <c>date_trunc('day', columna) = @parametro</c>, y <c>date_trunc</c> sobre <c>timestamptz</c>
    /// trunca en la zona de la SESIÓN — con una sesión en America/Bogota devuelve medianoche local
    /// (05:00 UTC) y nunca coincide con el parámetro en medianoche UTC, así que la comparación da
    /// falso aunque la fecha sea la misma.
    /// </para>
    /// <para>Además el rango es SARGABLE: puede usar un índice sobre la columna de fecha.</para>
    /// </summary>
    public static (DateTime Desde, DateTime Hasta) RangoDiaUtc(DateTime valor)
    {
        var fecha = valor.Kind switch
        {
            DateTimeKind.Local => valor.ToUniversalTime().Date,
            _                  => valor.Date
        };
        var desde = DateTime.SpecifyKind(fecha, DateTimeKind.Utc);
        return (desde, desde.AddDays(1));
    }

    /// <summary>Variante nullable de <see cref="AnclarMediodiaUtc(DateTime)"/>.</summary>
    public static DateTime? AnclarMediodiaUtc(DateTime? valor) =>
        valor.HasValue ? AnclarMediodiaUtc(valor.Value) : null;
}
