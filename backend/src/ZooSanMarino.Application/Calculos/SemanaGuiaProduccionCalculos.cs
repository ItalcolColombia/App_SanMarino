// src/ZooSanMarino.Application/Calculos/SemanaGuiaProduccionCalculos.cs
//
// Qué semana usar para cruzar una fila diaria de PRODUCCIÓN contra la guía genética.
//
// El reporte técnico de producción numera sus semanas desde el INICIO DE PRODUCCIÓN (semana 1 = la
// primera semana de postura). La guía compartida (`guia_genetica_sanmarino_colombia`) arranca en
// edad 1 y el reporte la cruza con ese mismo número — comportamiento histórico, que este cálculo
// NO toca.
//
// La guía PROPIA (`guia_genetica_santa_reyes`) está indexada por SEMANA DE VIDA del ave: medido el
// 1-sep-2026, va de la 18 a la 140. Cruzarla con la semana relativa a producción no encuentra nada
// en las primeras 17 semanas y, de ahí en adelante, cruza contra la fila equivocada. Por eso, y
// solo cuando la guía es propia, la semana de cruce pasa a contarse desde el encasetamiento.
namespace ZooSanMarino.Application.Calculos;

public static class SemanaGuiaProduccionCalculos
{
    /// <summary>
    /// Semana (base 1) de <paramref name="fecha"/> contada desde <paramref name="fechaBase"/>.
    /// Aritmética IDÉNTICA a la que tenía inline <c>ReporteTecnicoProduccionService.Tabs</c>
    /// (<c>(int)</c> sobre <c>TotalDays</c> y luego <c>Math.Ceiling((dias + 1.0) / 7)</c>): no se
    /// normaliza a <c>.Date</c> ni se cambia el redondeo, para que el resultado con guía compartida
    /// sea el de siempre.
    /// </summary>
    public static int SemanaDesde(DateTime fecha, DateTime fechaBase)
    {
        var edadDias = (int)(fecha - fechaBase).TotalDays;
        return (int)Math.Ceiling((edadDias + 1.0) / 7);
    }

    /// <summary>
    /// Semana con la que buscar la fila de guía.
    ///
    /// <para><b>Guía compartida</b> (<paramref name="guiaEsPropia"/> <c>false</c>): la semana
    /// relativa al inicio de producción, exactamente como hoy. Delta cero para Sanmarino, Demo,
    /// Ecuador y Panamá — las cuatro medidas con 0 filas de guía propia.</para>
    ///
    /// <para><b>Guía propia</b>: la semana de vida, contada desde el encasetamiento. Si no hay
    /// <paramref name="fechaEncaset"/> se cae a la relativa en vez de lanzar: un lote sin encaset
    /// registrado sigue mostrando el reporte, apenas sin cruzar la guía.</para>
    /// </summary>
    public static int Resolver(
        bool guiaEsPropia,
        DateTime fecha,
        DateTime fechaInicioProduccion,
        DateTime? fechaEncaset)
    {
        if (!guiaEsPropia || !fechaEncaset.HasValue)
            return SemanaDesde(fecha, fechaInicioProduccion);

        return SemanaDesde(fecha, fechaEncaset.Value);
    }
}
