using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Grilla de producción con <c>permite_multiples_seguimientos_diarios</c>: cuelga de cada fila del
/// día (la que devuelve <c>fn_seguimiento_diario_produccion</c> ya agrupada) los registros reales
/// que la componen, para que cada uno tenga su propia fila con Ver / Validar / Editar / Eliminar.
///
/// <para>
/// <b>Por qué no se reemplaza la fila del día por los registros.</b> El mismo arreglo alimenta en el
/// front los indicadores, la gráfica y el Excel, que cuentan DÍAS. La fila agrupada se queda como
/// está y los registros viajan aparte, en <see cref="SeguimientoItemDto.RegistrosDelDia"/>; solo la
/// tabla los despliega.
/// </para>
///
/// <para>
/// Solo se adjunta cuando el día tiene 2 o más registros: con uno, la fila del día ES el registro y
/// la respuesta queda idéntica a la de siempre.
/// </para>
/// </summary>
public static class SeguimientoProduccionRegistrosDelDiaCalculos
{
    /// <summary>
    /// Día calendario de Bogotá, el mismo que usa la fn para agrupar
    /// (<c>c_ts AT TIME ZONE 'America/Bogota'</c>). Bogotá es UTC-5 sin horario de verano, así que se
    /// resta la hora en vez de depender de la base de zonas horarias del contenedor.
    /// </summary>
    public static DateOnly DiaBogota(DateTime ts)
    {
        var utc = ts.Kind switch
        {
            DateTimeKind.Utc => ts,
            DateTimeKind.Local => ts.ToUniversalTime(),
            _ => DateTime.SpecifyKind(ts, DateTimeKind.Utc)
        };
        return DateOnly.FromDateTime(utc.AddHours(-5));
    }

    /// <summary>
    /// Devuelve las filas del día en el mismo orden; a las que tienen 2+ registros ese día les agrega
    /// <c>RegistrosDelDia</c> ordenados por fecha y luego por id (el orden en que se cargaron).
    /// </summary>
    public static List<SeguimientoItemDto> Adjuntar(
        IReadOnlyList<SeguimientoItemDto> filasDia, IEnumerable<SeguimientoItemDto> registros)
    {
        var porDia = registros
            .GroupBy(r => DiaBogota(r.FechaRegistro))
            .Where(g => g.Count() > 1)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<SeguimientoItemDto>)g.OrderBy(r => r.FechaRegistro).ThenBy(r => r.Id).ToList());

        return filasDia
            .Select(f => porDia.TryGetValue(DiaBogota(f.FechaRegistro), out var delDia)
                ? f with { RegistrosDelDia = delDia }
                : f)
            .ToList();
    }
}
