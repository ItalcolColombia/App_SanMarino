// file: src/ZooSanMarino.Application/Calculos/UbicacionCalculos.cs
// Reglas PURAS (sin EF/estado) de la gestión de ubicación (mover/eliminar núcleo·galpón·lote).
// Los servicios delegan aquí las decisiones y los mensajes para que sean testeables por xUnit.
namespace ZooSanMarino.Application.Calculos;

public static class UbicacionCalculos
{
    // ─────────── Eliminación con flujo completo ───────────

    /// <summary>Un núcleo puede eliminarse solo si no tiene galpones ni lotes activos.</summary>
    public static bool PuedeEliminarNucleo(int galponesActivos, int lotesActivos)
        => galponesActivos <= 0 && lotesActivos <= 0;

    /// <summary>Un galpón puede eliminarse solo si no tiene lotes activos.</summary>
    public static bool PuedeEliminarGalpon(int lotesActivos)
        => lotesActivos <= 0;

    /// <summary>Mensaje de bloqueo al intentar eliminar un núcleo con hijos activos.</summary>
    public static string MensajeBloqueoEliminarNucleo(int galponesActivos, int lotesActivos)
        => $"No se puede eliminar el núcleo: tiene {Math.Max(0, galponesActivos)} galpón(es) y " +
           $"{Math.Max(0, lotesActivos)} lote(s) activos. Muévalos o elimínelos primero.";

    /// <summary>Mensaje de bloqueo al intentar eliminar un galpón con lotes activos.</summary>
    public static string MensajeBloqueoEliminarGalpon(int lotesActivos)
        => $"No se puede eliminar el galpón: tiene {Math.Max(0, lotesActivos)} lote(s) activos. " +
           $"Muévalos o elimínelos primero.";

    // ─────────── Precondiciones de "mover" ───────────

    /// <summary>Mover a la misma granja no tiene sentido (para el re-key de núcleo).</summary>
    public static bool EsMismaGranja(int granjaOrigen, int granjaDestino)
        => granjaOrigen == granjaDestino;

    /// <summary>Mensaje de impacto tras mover (galpón o núcleo).</summary>
    public static string MensajeMovido(string entidad, int galponesAfectados, int lotesAfectados)
        => $"{entidad} movido correctamente. {Math.Max(0, galponesAfectados)} galpón(es) y " +
           $"{Math.Max(0, lotesAfectados)} lote(s) reubicados.";
}
