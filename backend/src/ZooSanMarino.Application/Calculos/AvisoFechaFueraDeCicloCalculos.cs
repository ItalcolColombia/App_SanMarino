// Aviso cuando un movimiento de alimento se fecha FUERA del ciclo vigente del galpón.
//
// POR QUÉ EXISTE
// Kilometro 86 / G0040 recibió 182.630 kg fechados DESPUÉS de que su ciclo cerró, y el sistema los
// aceptó sin decir nada. El lote se había comido ese alimento, pero como el ingreso quedó fechado
// fuera de su ventana la tabla diaria mostró un déficit de 9.020 kg que parecía un error del
// aplicativo. Fue la única causa de negativos que NO se pudo arreglar con código: el dato ya estaba mal.
//
// La captura es el punto más barato para atajarlo. Acá vive la decisión, pura y testeada.
//
// NO BLOQUEA, AVISA. Retrofechar es legítimo —la operación a veces registra el lunes lo que llegó el
// viernes— y bloquearlo tendría un costo operativo real. Lo que no puede pasar es que lo haga sin
// enterarse.
using System.Globalization;

namespace ZooSanMarino.Application.Calculos;

/// <summary>Un ciclo del galpón, visto desde su rango de seguimiento.</summary>
/// <param name="LoteId">Id del lote de engorde.</param>
/// <param name="Nombre">Nombre del lote (en Ecuador identifica la corrida: 2601, 2602…).</param>
/// <param name="SegMin">Primer día con seguimiento cargado.</param>
/// <param name="SegMax">Último día con seguimiento cargado.</param>
public readonly record struct CicloGalpon(int LoteId, string Nombre, DateTime SegMin, DateTime SegMax);

public static class AvisoFechaFueraDeCicloCalculos
{
    /// <summary>
    /// Devuelve el aviso a mostrar, o <c>null</c> si la fecha es normal.
    /// </summary>
    /// <param name="fechaMovimiento">Fecha de operación del ingreso o traslado.</param>
    /// <param name="ciclosDelGalpon">Lotes de engorde del galpón que ya tienen seguimiento.</param>
    /// <param name="diasAlimentoPrevioEncaset">
    /// Ventana previa al encaset de la empresa: el preiniciador llega antes que los pollitos, así que
    /// una fecha dentro de esa ventana es <b>normal</b> y no genera aviso.
    /// </param>
    public static string? Evaluar(
        DateTime fechaMovimiento,
        IEnumerable<CicloGalpon> ciclosDelGalpon,
        int diasAlimentoPrevioEncaset)
    {
        var ciclos = ciclosDelGalpon.ToList();

        // Galpón sin lotes de engorde con seguimiento: el movimiento no afecta ninguna tabla diaria.
        if (ciclos.Count == 0)
            return null;

        var f = fechaMovimiento.Date;
        var dias = Math.Max(0, diasAlimentoPrevioEncaset);

        // El ciclo vigente es el que llega más lejos en el tiempo.
        var activo = ciclos.OrderByDescending(c => c.SegMax).ThenByDescending(c => c.LoteId).First();
        var desdeActivo = activo.SegMin.Date.AddDays(-dias);

        // Dentro del ciclo activo o de su ventana previa —y también cualquier fecha posterior, que es
        // el caso corriente de registrar hoy lo que llegó hoy—: nada que avisar.
        if (f >= desdeActivo)
            return null;

        // La fecha es anterior al ciclo vigente. ¿Cae dentro de un ciclo que ya cerró?
        var cicloViejo = ciclos
            .Where(c => c.LoteId != activo.LoteId && f >= c.SegMin.Date && f <= c.SegMax.Date)
            .OrderByDescending(c => c.SegMax)
            .Select(c => (CicloGalpon?)c)
            .FirstOrDefault();

        if (cicloViejo is { } viejo)
            return $"La fecha {Fecha(f)} cae dentro del ciclo «{viejo.Nombre}», que cerró el " +
                   $"{Fecha(viejo.SegMax)}. El ciclo vigente del galpón es «{activo.Nombre}» " +
                   $"(desde {Fecha(activo.SegMin)}). Verificá que el movimiento sea de ese ciclo anterior.";

        return $"La fecha {Fecha(f)} es anterior al ciclo vigente «{activo.Nombre}», que cuenta el " +
               $"alimento desde el {Fecha(desdeActivo)}, y no cae dentro de ningún ciclo cerrado. " +
               "Ningún lote va a reflejar este movimiento en su tabla diaria.";
    }

    private static string Fecha(DateTime d) => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
