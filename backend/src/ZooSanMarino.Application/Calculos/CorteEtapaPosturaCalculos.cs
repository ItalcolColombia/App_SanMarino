// src/ZooSanMarino.Application/Calculos/CorteEtapaPosturaCalculos.cs
// Regla PURA del corte entre las etapas de postura: un mismo día no puede aportar consumo ni bajas
// desde levante y desde producción a la vez. Sin EF, sin estado.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Un día del lote pertenece a UNA etapa. Cuando levante y producción registran el mismo día con
/// consumo o bajas, los reportes que suman el ciclo cuentan dos veces lo mismo — es lo que pasó en
/// el lote K345, con 14 días de julio-2025 duplicados en las dos tablas (16.952 kg de alimento y
/// 10 aves).
/// <para>
/// El corte NO es «no puede haber dos filas el mismo día»: el arrastre de huevos del levante crea
/// legítimamente una fila de producción para un día que ya tiene su fila de levante (ver el merge
/// de <c>CrearSeguimientoAsync</c>). Esa fila solo trae huevos. Lo que se bloquea es que **las dos
/// filas aporten consumo o bajas**, que es el doble conteo real.
/// </para>
/// </summary>
public static class CorteEtapaPosturaCalculos
{
    /// <summary>
    /// Aportes de una fila diaria que sí se suman en los reportes del ciclo. Los huevos quedan
    /// fuera a propósito: son el caso legítimo del arrastre.
    /// </summary>
    public readonly record struct AporteDia(decimal ConsumoKg, int Mortalidad, int Seleccion)
    {
        /// <summary>La fila mueve alimento o aves (no es una fila de solo huevos ni una fila vacía).</summary>
        public bool Aporta => ConsumoKg > 0m || Mortalidad > 0 || Seleccion > 0;
    }

    public static readonly AporteDia SinAporte = new(0m, 0, 0);

    /// <summary>
    /// Hay doble conteo cuando el registro que se va a guardar y el que ya existe en la OTRA etapa,
    /// para el mismo día del mismo lote, aportan ambos consumo o bajas.
    /// </summary>
    public static bool HayDobleConteo(AporteDia nuevo, AporteDia otraEtapa) =>
        nuevo.Aporta && otraEtapa.Aporta;

    /// <summary>
    /// Mensaje único para las dos direcciones del corte: el usuario tiene que entender que el dato
    /// no se pierde, sino que ya está registrado del otro lado.
    /// </summary>
    public static string Mensaje(string etapaQueSeIntenta, string etapaQueYaTiene, DateTime dia) =>
        $"El {dia:dd/MM/yyyy} ya tiene un registro de {etapaQueYaTiene} con consumo o bajas para este lote. " +
        $"Un mismo día no puede registrarse en {etapaQueSeIntenta} y en {etapaQueYaTiene} a la vez: " +
        "los reportes del ciclo sumarían dos veces el mismo alimento y las mismas aves. " +
        $"Corregí el registro de {etapaQueYaTiene} de ese día o cambiá la fecha.";

    /// <summary>Mensaje del alta de LEVANTE que choca con un día ya registrado en producción.</summary>
    public static string MensajeLevanteChocaConProduccion(DateTime dia) =>
        Mensaje("levante", "producción", dia);

    /// <summary>Mensaje del alta de PRODUCCIÓN que choca con un día ya registrado en levante.</summary>
    public static string MensajeProduccionChocaConLevante(DateTime dia) =>
        Mensaje("producción", "levante", dia);
}
