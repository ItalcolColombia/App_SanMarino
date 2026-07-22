// src/ZooSanMarino.Application/Calculos/GestionLotesEngordeCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo PURO de la numeración de "corrida" del Lote de pollo engorde (sin EF ni estado).
///
/// Regla (solo Panamá): al abrir un lote se elige un <b>lote base</b> (ej. "96") y un galpón; el
/// número de corrida se incrementa cada vez que el <b>mismo lote base</b> se abre en el <b>mismo
/// galpón</b> — primero "96 - 1", luego "96 - 2", etc. En otro galpón la numeración reinicia en 1.
/// La consulta del máximo actual (MAX por company+base+galpón) vive en el service; aquí solo la aritmética.
/// </summary>
public static class GestionLotesEngordeCalculos
{
    /// <summary>
    /// Siguiente número de corrida dado el máximo actual de la combinación (lote base, galpón).
    /// <paramref name="maxActual"/> es NULL cuando aún no hay ningún lote de esa base en ese galpón → arranca en 1.
    /// </summary>
    public static int SiguienteNumeroCorrida(int? maxActual) => (maxActual ?? 0) + 1;

    /// <summary>
    /// Nombre a mostrar del lote: nombre del lote base + " - " + número de corrida (ej. "96 - 1").
    /// </summary>
    public static string ConstruirNombreCorrida(string baseNombre, int numero) =>
        $"{(baseNombre ?? string.Empty).Trim()} - {numero}";
}
