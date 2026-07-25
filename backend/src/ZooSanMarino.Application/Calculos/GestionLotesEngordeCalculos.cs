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

    // ────────────────────────────────────────────────────────────────────────
    // Código ERP de la GRANJA (solo Panamá): "{prefijo}{lote base}" (ej. "4001017"
    // = prefijo 4001 + base 17). Los lotes nuevos lo capturan en lote_erp; al
    // cerrar TODOS los lotes del lote base en la granja el código avanza +1:
    // 4001017 → 4001018 … 4001099 → 4001100 → 4001101 (el +1 es numérico sobre
    // el código completo, conservando ceros a la izquierda si la longitud encoge).
    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Código válido para configurar en la granja: null/vacío (sin código = comportamiento
    /// actual) o SOLO dígitos con máximo 18 (cabe en long para poder avanzar +1).
    /// </summary>
    public static bool EsCodigoErpGranjaValido(string? codigo)
    {
        var c = (codigo ?? string.Empty).Trim();
        if (c.Length == 0) return true;
        if (c.Length > 18) return false;
        foreach (var ch in c)
            if (ch is < '0' or > '9') return false;
        return true;
    }

    /// <summary>
    /// Siguiente código ERP de la granja al cerrarse el ciclo completo del lote base:
    /// +1 numérico sobre el código completo, preservando la longitud original con ceros a la
    /// izquierda ("0099" → "0100"). Devuelve <c>null</c> si el código no es numérico (no avanzar).
    /// </summary>
    public static string? SiguienteCodigoErpGranja(string? codigoActual)
    {
        var codigo = (codigoActual ?? string.Empty).Trim();
        if (codigo.Length == 0 || codigo.Length > 18) return null;
        foreach (var ch in codigo)
            if (ch is < '0' or > '9') return null;
        if (!long.TryParse(codigo, out var valor)) return null;
        var siguiente = (valor + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return siguiente.Length < codigo.Length ? siguiente.PadLeft(codigo.Length, '0') : siguiente;
    }
}
