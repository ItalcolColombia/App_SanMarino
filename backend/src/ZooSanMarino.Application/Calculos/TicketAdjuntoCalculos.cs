// src/ZooSanMarino.Application/Calculos/TicketAdjuntoCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Contenido de los documentos adjuntos de un ticket (<c>ticket_adjuntos.contenido_base64</c>).
/// <para>
/// La columna debe guardar base64 PURO. El formulario de creación mandaba la data URL completa
/// (<c>data:application/pdf;base64,JVBER…</c>) y la descarga le anteponía otro prefijo ⇒ el archivo
/// salía dañado. Se normaliza al escribir (no entran filas nuevas con prefijo) y al leer (las filas
/// viejas se sirven bien sin backfill).
/// </para>
/// </summary>
public static class TicketAdjuntoCalculos
{
    /// <summary>
    /// Devuelve el base64 sin el prefijo de data URL (<c>data:&lt;tipo&gt;;base64,</c>) y sin espacios
    /// ni saltos de línea. <c>null</c>/vacío → cadena vacía. Un base64 ya puro vuelve igual.
    /// </summary>
    public static string NormalizarBase64(string? contenido)
    {
        if (string.IsNullOrWhiteSpace(contenido)) return string.Empty;

        var s = contenido.Trim();
        if (s.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var coma = s.IndexOf(',');
            s = coma >= 0 ? s[(coma + 1)..] : string.Empty;
        }

        return string.Concat(s.Where(c => !char.IsWhiteSpace(c)));
    }
}
