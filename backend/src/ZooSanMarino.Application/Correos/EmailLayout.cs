using System.Net;

namespace ZooSanMarino.Application.Correos;

/// <summary>
/// Documento HTML de un correo saliente: preheader, encabezado con logo, contenedor y pie.
///
/// Por qué existe: los correos de la aplicación tenían TRES layouts distintos (dos bloques
/// <c>&lt;style&gt;</c> gemelos en <c>EmailService</c> y el <c>Wrap</c> de las plantillas de tickets),
/// más un HTML suelto dentro de <c>TicketService</c>. Cualquier arreglo de maquetación había que
/// hacerlo cuatro veces y siempre quedaba uno afuera.
///
/// **Un correo no es una página web.** Outlook para Windows renderiza con el motor de Word: no hay
/// flexbox ni grid, los <c>&lt;style&gt;</c> del <c>&lt;head&gt;</c> se aplican de forma parcial y los
/// degradados se ignoran dejando texto ilegible. Por eso acá todo es <b>tabla + estilo inline</b>, y
/// el bloque <c>&lt;style&gt;</c> se usa solo como refuerzo progresivo (responsive y modo oscuro),
/// nunca como única fuente del diseño.
/// </summary>
public static class EmailLayout
{
    /// <summary>
    /// Arma el documento completo. <paramref name="contenidoHtml"/> es HTML ya construido con
    /// <see cref="EmailComponentes"/>; el resto son textos planos que se escapan acá.
    /// </summary>
    /// <param name="titulo">Título del documento (pestaña / lectores de pantalla).</param>
    /// <param name="preheader">
    /// Texto de vista previa que la bandeja de entrada muestra junto al asunto. Si va vacío, el
    /// cliente inventa uno con las primeras palabras del cuerpo — que suelen ser "Hola, ...".
    /// </param>
    /// <param name="logoUrl">URL del logo. Si va vacía, el encabezado cae a la marca en texto.</param>
    /// <param name="marca">Nombre de la marca (ItalGranja).</param>
    /// <param name="lema">Bajada de la marca.</param>
    /// <param name="contenidoHtml">Cuerpo ya renderizado.</param>
    /// <param name="motivoEnvio">
    /// Línea del pie que explica POR QUÉ le llegó el correo. Reduce reportes de spam y es lo primero
    /// que busca quien no esperaba el mensaje.
    /// </param>
    public static string Documento(
        string titulo,
        string preheader,
        string logoUrl,
        string marca,
        string lema,
        string contenidoHtml,
        string? motivoEnvio = null)
    {
        var tituloSeguro = WebUtility.HtmlEncode(titulo ?? string.Empty);
        var marcaSegura = WebUtility.HtmlEncode(marca ?? string.Empty);
        var lemaSeguro = WebUtility.HtmlEncode(lema ?? string.Empty);
        var preheaderSeguro = WebUtility.HtmlEncode(preheader ?? string.Empty);
        var motivo = WebUtility.HtmlEncode(
            motivoEnvio ?? "Recibís este correo porque tenés una cuenta activa en la plataforma.");

        var anio = DateTime.UtcNow.Year;

        // Relleno de ancho cero: evita que el cliente complete la vista previa con el cuerpo.
        var relleno = string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 60));

        // El logo va como imagen remota, y buena parte de los clientes de correo (Outlook de
        // escritorio siempre, Gmail ante un remitente desconocido) NO la descarga: el lector ve el
        // texto alternativo. Por eso el <img> lleva tipografía propia — así el respaldo se lee como
        // el nombre de la marca y no como una leyenda minúscula al lado de un ícono roto. La clase
        // "txt" lo aclara en modo oscuro.
        var encabezado = string.IsNullOrWhiteSpace(logoUrl)
            ? $"""
                  <div class="txt" style="font-family:{EmailTema.Fuente};font-size:24px;font-weight:700;letter-spacing:-.02em;color:{EmailTema.Texto};">{marcaSegura}</div>
              """
            : $"""
                  <img src="{WebUtility.HtmlEncode(logoUrl)}" alt="{marcaSegura}" width="150" class="txt"
                       style="display:block;margin:0 auto;max-height:52px;width:auto;border:0;outline:none;text-decoration:none;font-family:{EmailTema.Fuente};font-size:22px;line-height:30px;font-weight:700;letter-spacing:-.02em;color:{EmailTema.Texto};" />
              """;

        return $$"""
            <!DOCTYPE html>
            <html lang="es" xmlns="http://www.w3.org/1999/xhtml" xmlns:v="urn:schemas-microsoft-com:vml" xmlns:o="urn:schemas-microsoft-com:office:office">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <meta http-equiv="X-UA-Compatible" content="IE=edge" />
              <meta name="x-apple-disable-message-reformatting" />
              <meta name="color-scheme" content="light dark" />
              <meta name="supported-color-schemes" content="light dark" />
              <title>{{tituloSeguro}}</title>
              <!--[if mso]>
              <noscript><xml><o:OfficeDocumentSettings><o:PixelsPerInch>96</o:PixelsPerInch></o:OfficeDocumentSettings></xml></noscript>
              <![endif]-->
              <style>
                /* Refuerzo progresivo: si el cliente lo ignora, el inline ya sostiene el diseño. */
                body, table, td, a { -webkit-text-size-adjust:100%; -ms-text-size-adjust:100%; }
                table, td { mso-table-lspace:0pt; mso-table-rspace:0pt; }
                img { -ms-interpolation-mode:bicubic; border:0; height:auto; line-height:100%; outline:none; text-decoration:none; }
                a { text-decoration:none; }

                @media only screen and (max-width:620px) {
                  .contenedor { width:100% !important; max-width:100% !important; }
                  .respiro    { padding-left:20px !important; padding-right:20px !important; }
                  .boton-a    { display:block !important; width:auto !important; }
                  .apilar     { display:block !important; width:100% !important; }
                  .titulo-h1  { font-size:22px !important; line-height:30px !important; }
                }

                @media (prefers-color-scheme: dark) {
                  .lienzo      { background-color:#0f1115 !important; }
                  .tarjeta     { background-color:#171a21 !important; border-color:#2a2f3a !important; }
                  .txt         { color:#e7e9ee !important; }
                  .txt-suave   { color:#b6bcc9 !important; }
                  .txt-tenue   { color:#8b93a3 !important; }
                  .sup-suave   { background-color:#1d2129 !important; }
                  .borde       { border-color:#2a2f3a !important; }
                }
              </style>
            </head>
            <body class="lienzo" style="margin:0;padding:0;width:100%;background-color:{{EmailTema.Lienzo}};font-family:{{EmailTema.Fuente}};">

              <div style="display:none;font-size:1px;color:{{EmailTema.Lienzo}};line-height:1px;max-height:0;max-width:0;opacity:0;overflow:hidden;">
                {{preheaderSeguro}}{{relleno}}
              </div>

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0" class="lienzo"
                     style="background-color:{{EmailTema.Lienzo}};">
                <tr>
                  <td align="center" style="padding:32px 12px;">

                    <table role="presentation" class="contenedor" width="{{EmailTema.AnchoMaximo}}" cellpadding="0" cellspacing="0" border="0"
                           style="width:{{EmailTema.AnchoMaximo}}px;max-width:{{EmailTema.AnchoMaximo}}px;">

                      <!-- Encabezado -->
                      <tr>
                        <td align="center" style="padding:0 0 20px 0;">
                          {{encabezado}}
                          <div class="txt-tenue" style="margin-top:8px;font-size:13px;line-height:18px;color:{{EmailTema.TextoTenue}};">
                            {{lemaSeguro}}
                          </div>
                        </td>
                      </tr>

                      <!-- Tarjeta principal -->
                      <tr>
                        <td class="tarjeta" style="background-color:{{EmailTema.Superficie}};border:1px solid {{EmailTema.Borde}};border-radius:12px;">
                          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                            <tr>
                              <td class="respiro" style="padding:32px 36px;">
                                {{contenidoHtml}}
                              </td>
                            </tr>
                          </table>
                        </td>
                      </tr>

                      <!-- Pie -->
                      <tr>
                        <td class="respiro" style="padding:24px 36px 8px 36px;text-align:center;">
                          <div class="txt-tenue" style="font-size:12px;line-height:19px;color:{{EmailTema.TextoTenue}};">
                            {{motivo}}<br />
                            Este es un correo automático, por favor no respondas a esta dirección.
                          </div>
                          <div class="txt-tenue" style="margin-top:12px;font-size:12px;line-height:18px;color:{{EmailTema.TextoTenue}};">
                            &copy; {{anio}} {{marcaSegura}} &middot; Todos los derechos reservados
                          </div>
                        </td>
                      </tr>

                    </table>

                  </td>
                </tr>
              </table>
            </body>
            </html>
            """;
    }
}
