using System.Net;
using System.Text;

namespace ZooSanMarino.Application.Correos;

/// <summary>
/// Piezas reutilizables para armar el cuerpo de un correo. Todo lo que recibe texto plano se escapa
/// acá dentro: es la única forma de garantizar que un título de ticket con <c>&lt;</c> o el nombre de
/// un usuario no rompan el HTML ni abran un hueco de inyección en el cliente de correo.
///
/// Los métodos que reciben HTML ya armado lo dicen en el nombre del parámetro (<c>...Html</c>).
/// </summary>
public static class EmailComponentes
{
    private static string E(string? texto) => WebUtility.HtmlEncode(texto ?? string.Empty);

    // ───────────────────────────── Texto ─────────────────────────────

    /// <summary>Título principal del correo (uno solo por cuerpo).</summary>
    public static string Titulo(string texto) => $"""
        <h1 class="titulo-h1 txt" style="margin:0 0 16px 0;font-family:{EmailTema.Fuente};font-size:25px;line-height:33px;font-weight:700;letter-spacing:-.02em;color:{EmailTema.Texto};">{E(texto)}</h1>
        """;

    /// <summary>Saludo personal. Cae a "Hola," cuando no se conoce el nombre.</summary>
    public static string Saludo(string? nombre)
    {
        var texto = string.IsNullOrWhiteSpace(nombre) ? "Hola," : $"Hola {nombre},";
        return $"""
        <p class="txt-suave" style="margin:0 0 16px 0;font-family:{EmailTema.Fuente};font-size:16px;line-height:25px;color:{EmailTema.TextoSuave};">{E(texto)}</p>
        """;
    }

    /// <summary>Párrafo de texto plano (se escapa).</summary>
    public static string Parrafo(string texto) => ParrafoHtml(E(texto));

    /// <summary>Párrafo cuyo contenido ya viene como HTML seguro (para negritas puntuales).</summary>
    public static string ParrafoHtml(string html) => $"""
        <p class="txt-suave" style="margin:0 0 16px 0;font-family:{EmailTema.Fuente};font-size:16px;line-height:25px;color:{EmailTema.TextoSuave};">{html}</p>
        """;

    /// <summary>Texto chico y tenue, para aclaraciones al pie de una sección.</summary>
    public static string Nota(string texto) => $"""
        <p class="txt-tenue" style="margin:0 0 12px 0;font-family:{EmailTema.Fuente};font-size:13px;line-height:20px;color:{EmailTema.TextoTenue};">{E(texto)}</p>
        """;

    /// <summary>Línea divisoria entre bloques.</summary>
    public static string Separador() => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
          <td class="borde" style="border-top:1px solid {EmailTema.Borde};font-size:0;line-height:0;height:1px;">&nbsp;</td>
        </tr></table>
        <div style="height:24px;line-height:24px;font-size:0;">&nbsp;</div>
        """;

    // ───────────────────────────── Acción ─────────────────────────────

    /// <summary>
    /// Botón principal. El color va en el <c>bgcolor</c> de la celda además del inline del enlace:
    /// es lo que hace que Outlook lo pinte. Sin degradados a propósito — Outlook los descarta y deja
    /// texto blanco sobre fondo blanco.
    /// </summary>
    public static string Boton(string url, string texto, string? colorFondo = null, string? colorBorde = null)
    {
        var fondo = colorFondo ?? EmailTema.Accion;
        var borde = colorBorde ?? EmailTema.AccionOscuro;
        return $"""
        <table role="presentation" cellpadding="0" cellspacing="0" border="0" align="center" style="margin:28px auto 20px auto;">
          <tr>
            <td align="center" bgcolor="{fondo}" style="border-radius:8px;">
              <a href="{E(url)}" class="boton-a" target="_blank" rel="noopener"
                 style="display:inline-block;padding:15px 34px;font-family:{EmailTema.Fuente};font-size:16px;line-height:20px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:8px;background-color:{fondo};border:1px solid {borde};">{E(texto)}</a>
            </td>
          </tr>
        </table>
        """;
    }

    /// <summary>
    /// Enlace de respaldo en texto. Va siempre debajo del botón: hay clientes corporativos que
    /// bloquean los enlaces con estilo y usuarios que copian y pegan en otro navegador.
    /// </summary>
    public static string EnlaceRespaldo(string url) => $"""
        <p class="txt-tenue" style="margin:0 0 8px 0;font-family:{EmailTema.Fuente};font-size:13px;line-height:20px;color:{EmailTema.TextoTenue};">
          Si el botón no funciona, copiá y pegá este enlace en tu navegador:
        </p>
        <p style="margin:0 0 20px 0;font-family:{EmailTema.FuenteMono};font-size:12px;line-height:19px;word-break:break-all;">
          <a href="{E(url)}" target="_blank" rel="noopener" style="color:{EmailTema.Accion};text-decoration:underline;">{E(url)}</a>
        </p>
        """;

    // ───────────────────────────── Datos ─────────────────────────────

    /// <summary>
    /// Ficha de datos etiqueta/valor. La etiqueta va arriba del valor (no en columna aparte) para que
    /// no se rompa en pantallas angostas, que es donde se leen la mayoría de estos correos.
    /// </summary>
    public static string Ficha(params (string Etiqueta, string Valor)[] filas)
    {
        if (filas.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="sup-suave"
               style="background-color:{EmailTema.SuperficieSuave};border:1px solid {EmailTema.Borde};border-radius:10px;margin:0 0 24px 0;">
          <tr><td style="padding:6px 20px;">
        """);

        for (var i = 0; i < filas.Length; i++)
        {
            var (etiqueta, valor) = filas[i];
            var borde = i < filas.Length - 1
                ? $"border-bottom:1px solid {EmailTema.BordeSuave};"
                : string.Empty;

            sb.Append($"""
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"><tr>
              <td class="borde" style="padding:13px 0;{borde}">
                <div class="txt-tenue" style="font-family:{EmailTema.Fuente};font-size:11px;line-height:15px;font-weight:600;letter-spacing:.07em;text-transform:uppercase;color:{EmailTema.TextoTenue};">{E(etiqueta)}</div>
                <div class="txt" style="margin-top:5px;font-family:{EmailTema.Fuente};font-size:15px;line-height:23px;color:{EmailTema.Texto};word-break:break-word;">{E(valor)}</div>
              </td>
            </tr></table>
            """);
        }

        sb.Append("</td></tr></table>");
        return sb.ToString();
    }

    /// <summary>Valor destacado en monoespaciado (una credencial, un código). No usar para secretos que viajan por enlace.</summary>
    public static string BloqueCodigo(string etiqueta, string valor) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="sup-suave"
               style="background-color:{EmailTema.SuperficieSuave};border:1px solid {EmailTema.Borde};border-radius:10px;margin:0 0 20px 0;">
          <tr>
            <td style="padding:16px 20px;border-left:4px solid {EmailTema.Accion};border-radius:10px;">
              <div class="txt-tenue" style="font-family:{EmailTema.Fuente};font-size:11px;line-height:15px;font-weight:600;letter-spacing:.07em;text-transform:uppercase;color:{EmailTema.TextoTenue};">{E(etiqueta)}</div>
              <div class="txt" style="margin-top:7px;font-family:{EmailTema.FuenteMono};font-size:17px;line-height:25px;font-weight:600;color:{EmailTema.Texto};word-break:break-all;">{E(valor)}</div>
            </td>
          </tr>
        </table>
        """;

    /// <summary>Etiqueta de estado en línea (tipo, prioridad, estado del ticket).</summary>
    public static string Badge(string texto, string color, string fondo) => $"""
        <span style="display:inline-block;padding:5px 11px;margin:0 6px 6px 0;font-family:{EmailTema.Fuente};font-size:11px;line-height:15px;font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:{color};background-color:{fondo};border-radius:20px;">{E(texto)}</span>
        """;

    // ───────────────────────────── Avisos ─────────────────────────────

    /// <summary>Bloque de aviso con barra lateral de color. <paramref name="texto"/> se escapa.</summary>
    public static string Callout(string color, string fondo, string titulo, string texto) => $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
               style="background-color:{fondo};border-radius:10px;margin:0 0 22px 0;">
          <tr>
            <td style="padding:15px 18px;border-left:4px solid {color};border-radius:10px;">
              <div style="font-family:{EmailTema.Fuente};font-size:14px;line-height:20px;font-weight:700;color:{color};">{E(titulo)}</div>
              <div style="margin-top:5px;font-family:{EmailTema.Fuente};font-size:14px;line-height:21px;color:{EmailTema.TextoSuave};">{E(texto)}</div>
            </td>
          </tr>
        </table>
        """;

    public static string CalloutAviso(string titulo, string texto) =>
        Callout(EmailTema.Aviso, EmailTema.AvisoFondo, titulo, texto);

    public static string CalloutExito(string titulo, string texto) =>
        Callout(EmailTema.Exito, EmailTema.ExitoFondo, titulo, texto);

    public static string CalloutPeligro(string titulo, string texto) =>
        Callout(EmailTema.Peligro, EmailTema.PeligroFondo, titulo, texto);

    public static string CalloutInfo(string titulo, string texto) =>
        Callout(EmailTema.Info, EmailTema.InfoFondo, titulo, texto);

    /// <summary>Bloque largo de texto libre (la solución de un ticket, una descripción).</summary>
    public static string Cita(string titulo, string texto, string? color = null)
    {
        var c = color ?? EmailTema.Exito;
        return $"""
        <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="sup-suave"
               style="background-color:{EmailTema.SuperficieSuave};border-radius:10px;margin:0 0 22px 0;">
          <tr>
            <td style="padding:16px 20px;border-left:4px solid {c};border-radius:10px;">
              <div class="txt-tenue" style="font-family:{EmailTema.Fuente};font-size:11px;line-height:15px;font-weight:600;letter-spacing:.07em;text-transform:uppercase;color:{EmailTema.TextoTenue};">{E(titulo)}</div>
              <div class="txt-suave" style="margin-top:7px;font-family:{EmailTema.Fuente};font-size:15px;line-height:24px;color:{EmailTema.TextoSuave};white-space:pre-wrap;word-break:break-word;">{E(texto)}</div>
            </td>
          </tr>
        </table>
        """;
    }

    // ───────────────────────────── Listas ─────────────────────────────

    /// <summary>Pasos numerados ("qué hacer ahora"). Cada paso se escapa.</summary>
    public static string Pasos(params string[] pasos)
    {
        if (pasos.Length == 0) return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 22px 0;">""");

        for (var i = 0; i < pasos.Length; i++)
        {
            sb.Append($"""
            <tr>
              <td width="30" valign="top" style="padding:0 0 12px 0;">
                <div style="width:24px;height:24px;background-color:{EmailTema.Accion};border-radius:50%;font-family:{EmailTema.Fuente};font-size:13px;line-height:24px;font-weight:700;color:#ffffff;text-align:center;">{i + 1}</div>
              </td>
              <td valign="top" class="txt-suave" style="padding:2px 0 12px 10px;font-family:{EmailTema.Fuente};font-size:15px;line-height:22px;color:{EmailTema.TextoSuave};">{E(pasos[i])}</td>
            </tr>
            """);
        }

        sb.Append("</table>");
        return sb.ToString();
    }

    /// <summary>
    /// Bitácora del ticket. En pantalla angosta una tabla de 3 columnas se vuelve ilegible, así que
    /// cada nota se renderiza como una tarjeta con autor y fecha arriba del texto.
    /// </summary>
    public static string Bitacora(IEnumerable<(string Autor, string Fecha, string Texto)> notas)
    {
        var lista = notas.ToList();
        if (lista.Count == 0)
            return Nota("Sin novedades registradas en la bitácora pública.");

        var sb = new StringBuilder();
        sb.Append("""<table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" style="margin:0 0 22px 0;">""");

        foreach (var (autor, fecha, texto) in lista)
        {
            sb.Append($"""
            <tr><td class="borde" style="padding:12px 0;border-bottom:1px solid {EmailTema.BordeSuave};">
              <div style="font-family:{EmailTema.Fuente};font-size:13px;line-height:19px;">
                <span class="txt" style="font-weight:600;color:{EmailTema.Texto};">{E(autor)}</span>
                <span class="txt-tenue" style="color:{EmailTema.TextoTenue};"> &middot; {E(fecha)}</span>
              </div>
              <div class="txt-suave" style="margin-top:4px;font-family:{EmailTema.Fuente};font-size:14px;line-height:21px;color:{EmailTema.TextoSuave};word-break:break-word;">{E(texto)}</div>
            </td></tr>
            """);
        }

        sb.Append("</table>");
        return sb.ToString();
    }
}
