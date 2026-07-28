// src/ZooSanMarino.Application/Calculos/VentanaAlimentoPrevioCalculos.cs
// Desde qué fecha cuenta el alimento de un lote de engorde para el saldo del reporte diario.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Resuelve la fecha de corte del saldo de alimento: hasta cuántos días ANTES del encasetamiento se
/// cuenta el alimento que ya estaba en el galpón.
/// <para>
/// El corte existe desde el FIX #12 (may-2026): sin él, la apertura heredaba el inventario residual
/// del lote anterior que ocupó el galpón (lote 75/2602: 132,277 kg de más). Pero cortar exactamente
/// en la fecha de encaset descarta el alimento propio del lote: en engorde el PREINICIADOR llega
/// antes que los pollitos, y esos kilos quedaban fuera del saldo aunque el galpón los tuviera.
/// Caso testigo — galpón 6 de DAYLAND (jul-2026): 12.129,638 kg recibidos 4 días antes del encaset,
/// justo la diferencia entre el saldo del reporte y el inventario real.
/// </para>
/// <para>
/// La ventana es el punto medio: cuenta lo que llegó "para este lote" sin alcanzar el ciclo anterior.
/// El default son 10 días — menos que el vacío sanitario típico entre lotes (10 a 14), así que no
/// puede tocar el cierre del ciclo previo — y cada empresa puede ajustarlo.
/// </para>
/// </summary>
public static class VentanaAlimentoPrevioCalculos
{
    /// <summary>Días previos al encaset que se cuentan cuando la empresa no configuró un valor propio.</summary>
    public const int DiasPreviosPorDefecto = 10;

    /// <summary>Tope duro: más allá de esto la ventana alcanzaría al ciclo anterior del galpón.</summary>
    public const int DiasPreviosMaximo = 30;

    /// <summary>
    /// Normaliza el valor configurado por la empresa. Negativo o null ⇒ el default; por encima del
    /// máximo ⇒ el máximo (fail-safe: nunca se abre la ventana más de lo que el negocio admite).
    /// </summary>
    public static int NormalizarDias(int? diasConfigurados) => diasConfigurados switch
    {
        null => DiasPreviosPorDefecto,
        < 0 => DiasPreviosPorDefecto,
        > DiasPreviosMaximo => DiasPreviosMaximo,
        var d => d.Value
    };

    /// <summary>
    /// Fecha desde la que cuentan los movimientos de alimento del galpón. Sin fecha de encaset no hay
    /// corte (null): el lote todavía no tiene referencia temporal y filtrar sería arbitrario.
    /// </summary>
    public static DateTime? FechaCorte(DateTime? fechaEncaset, int? diasConfigurados)
    {
        if (!fechaEncaset.HasValue) return null;
        return fechaEncaset.Value.Date.AddDays(-NormalizarDias(diasConfigurados));
    }
}
