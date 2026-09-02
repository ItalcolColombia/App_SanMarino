// src/ZooSanMarino.Application/Calculos/GrafiaEdadGuiaCalculos.cs
//
// Cual de las dos filas de la SEMANA DE TRANSICION corresponde a cada reporte.
//
// En la guia de esquema completo (`guia_genetica_sanmarino_colombia`) la semana en que el lote pasa
// de levante a produccion aparece DOS VECES, con dos grafias:
//
//   "25"   -> la semana 25 todavia en LEVANTE. Cierra la serie del levante.
//   "25P"  -> la misma semana 25 ya en PRODUCCION ("P" de produccion). ABRE la serie de postura.
//
// No son duplicados ni un error de carga: son dos momentos del mismo calendario, y se distinguen
// porque los ACUMULADOS SE REINICIAN en la fila con P. Medido en AP 2026 de Sanmarino:
//
//                    cons_ac_h     retiro_ac_h   uniformidad   peso_huevo
//     edad "24"        10.678,5        3,93           90            -
//     edad "25"        11.501,2        4,03           90            -        <- fin del levante
//     edad "25P"          847,0        0,10            -          50,1       <- arranca produccion
//     edad "26"           920,9        0,33            -          52,3       <- sigue desde 847
//
// Por eso importa elegir bien: un reporte de PRODUCCION que tomara la fila "25" mostraria 11.501 g
// de consumo acumulado —el del levante entero— en vez de 847. Trece veces mas grande, y sin que
// nada se vea "roto".
//
// Aplica a las empresas cuya guia vive en la tabla de esquema completo y que registran huevo en
// levante antes del paso a produccion (`captura_huevos_en_levante`: hoy Sanmarino y Demo). La
// guia dedicada de esquema simple no tiene esta duplicidad: arranca directamente en produccion.
namespace ZooSanMarino.Application.Calculos;

public static class GrafiaEdadGuiaCalculos
{
    /// <summary>
    /// Sufijo que marca la fila de produccion de la semana de transicion.
    /// </summary>
    public const char SufijoProduccion = 'P';

    /// <summary>
    /// ¿Esta grafia es la variante de PRODUCCION de la semana de transicion (<c>"25P"</c>)?
    /// Se compara sin distinguir mayusculas y tolerando espacios, como el resto del parseo de la
    /// guia, que viene de un Excel cargado a mano.
    /// </summary>
    public static bool EsFilaDeProduccion(string? edad)
    {
        if (string.IsNullOrWhiteSpace(edad)) return false;
        var s = edad.Trim();
        return s.Length > 1
               && char.ToUpperInvariant(s[^1]) == SufijoProduccion
               && s[..^1].All(char.IsDigit);
    }

    /// <summary>
    /// ¿Esta grafia es un numero puro (<c>"25"</c>), o sea la fila de LEVANTE de esa semana?
    /// </summary>
    public static bool EsFilaDeLevante(string? edad) =>
        !string.IsNullOrWhiteSpace(edad) && edad.Trim().All(char.IsDigit);

    /// <summary>
    /// Primera semana de vida que la guia de esquema completo considera PRODUCCION. Es el corte
    /// historico de <c>ObtenerGuiaGeneticaProduccionAsync</c> y coincide con el dato: en esa guia
    /// la primera edad con <c>prod_porcentaje</c> es la 25/26.
    /// </summary>
    public const int PrimeraSemanaProduccion = 26;

    /// <summary>
    /// ¿Esta fila pertenece a la SERIE DE PRODUCCION de la guia?
    ///
    /// <para>Lo es si su edad ya llego al corte de produccion <b>o</b> si su grafia la marca como
    /// tal. Esa segunda condicion es la que rescata a <c>"25P"</c>: numericamente es 25 —por debajo
    /// del corte— pero es la fila que <b>abre</b> la serie de postura, con los acumulados
    /// reiniciados. Sin ella, la semana de transicion se queda sin sus valores standard de consumo,
    /// peso y mortalidad.</para>
    /// </summary>
    public static bool EsSerieDeProduccion(string? grafia, int edadNumerica, int desdeSemana = PrimeraSemanaProduccion)
        => edadNumerica >= desdeSemana || EsFilaDeProduccion(grafia);

    /// <summary>
    /// Peso de preferencia para ordenar candidatas de una misma semana: <b>menor gana</b>.
    ///
    /// <para>Con <paramref name="paraProduccion"/> la fila con <c>P</c> va primero; sin el, la
    /// numerica pura. Lo que no encaja en ninguna de las dos formas queda ultimo, nunca
    /// intercalado: una grafia nueva del Excel no se cuela sin que alguien lo decida.</para>
    /// </summary>
    public static int Preferencia(string? edad, bool paraProduccion)
    {
        var esProd = EsFilaDeProduccion(edad);
        var esLev  = EsFilaDeLevante(edad);

        if (!esProd && !esLev) return 2;          // grafia desconocida: al final
        return (paraProduccion ? esProd : esLev) ? 0 : 1;
    }
}
