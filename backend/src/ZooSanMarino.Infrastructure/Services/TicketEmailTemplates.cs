using System.Net;
using System.Text;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Plantillas HTML "pro" para las notificaciones de tickets (creación, transferencia/asignación,
/// cierre). Comparten el layout branded de <see cref="EmailService"/> (marca <c>#f4b428</c>,
/// contenedor redondeado, footer con año) pero con el logo como IMAGEN (no texto), configurable
/// vía <c>Email:LogoUrl</c>.
/// </summary>
public static class TicketEmailTemplates
{
    /// <summary>Fila liviana de nota pública para el histórico de chat del correo de cierre.</summary>
    public sealed record NotaResumen(string? Autor, DateTime CreatedAt, string Texto);

    // ───────────────────────────── Layout compartido ─────────────────────────────

    /// <summary>Envuelve el contenido interno en el layout branded (header con logo + footer).</summary>
    public static string Wrap(string logoUrl, string brandName, string brandLine, string innerHtml)
    {
        var safeLogo = WebUtility.HtmlEncode(logoUrl);
        var safeBrandName = WebUtility.HtmlEncode(brandName);
        var safeBrandLine = WebUtility.HtmlEncode(brandLine);

        return $@"
<!DOCTYPE html>
<html lang='es'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>{safeBrandName}</title>
    <style>
        body {{
            font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
            line-height: 1.6;
            color: #333;
            max-width: 600px;
            margin: 0 auto;
            padding: 20px;
            background-color: #f4f4f4;
        }}
        .container {{
            background-color: #ffffff;
            border-radius: 10px;
            padding: 30px;
            box-shadow: 0 2px 10px rgba(0,0,0,0.1);
        }}
        .header {{
            text-align: center;
            margin-bottom: 30px;
            padding-bottom: 20px;
            border-bottom: 3px solid #f4b428;
        }}
        .header img {{
            max-height: 48px;
            margin-bottom: 10px;
        }}
        .subtitle {{
            color: #6b7280;
            font-size: 14px;
        }}
        .content {{
            margin: 30px 0;
        }}
        .greeting {{
            font-size: 18px;
            color: #2b2b2b;
            margin-bottom: 20px;
        }}
        .message {{
            font-size: 16px;
            color: #4b5563;
            margin-bottom: 25px;
            line-height: 1.8;
        }}
        .info-box {{
            background-color: #f9fafb;
            border: 2px solid #e5e7eb;
            border-radius: 8px;
            padding: 20px;
            margin: 25px 0;
        }}
        .info-item {{
            margin: 12px 0;
            padding: 12px;
            background-color: #ffffff;
            border-radius: 6px;
            border-left: 4px solid #f4b428;
        }}
        .info-label {{
            font-weight: 600;
            color: #374151;
            font-size: 13px;
            margin-bottom: 4px;
        }}
        .info-value {{
            font-size: 15px;
            color: #1f2937;
            word-break: break-word;
        }}
        .solucion-box {{
            background-color: #f0fdf4;
            border-left: 4px solid #2d7a3e;
            padding: 15px;
            margin: 20px 0;
            border-radius: 6px;
        }}
        .chat-table {{
            width: 100%;
            border-collapse: collapse;
            margin: 15px 0;
            font-size: 13px;
        }}
        .chat-table th {{
            text-align: left;
            background-color: #f9fafb;
            padding: 8px 10px;
            border-bottom: 2px solid #e5e7eb;
            color: #374151;
        }}
        .chat-table td {{
            padding: 8px 10px;
            border-bottom: 1px solid #f1f5f9;
            color: #4b5563;
            vertical-align: top;
        }}
        .button-container {{
            text-align: center;
            margin: 30px 0;
        }}
        .button {{
            display: inline-block;
            background: linear-gradient(180deg, #f4b428, #e6a41c);
            color: #1a1a1a !important;
            padding: 14px 30px;
            text-decoration: none;
            border-radius: 8px;
            font-weight: 600;
            font-size: 16px;
            box-shadow: 0 4px 6px rgba(244, 180, 40, 0.3);
        }}
        .footer {{
            margin-top: 40px;
            padding-top: 20px;
            border-top: 1px solid #e5e7eb;
            text-align: center;
            color: #6b7280;
            font-size: 12px;
        }}
        .footer-text {{
            margin: 5px 0;
        }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <img src='{safeLogo}' alt='{safeBrandName}'/>
            <div class='subtitle'>{safeBrandLine}</div>
        </div>
        <div class='content'>
            {innerHtml}
        </div>
        <div class='footer'>
            <p class='footer-text'>© {DateTime.Now.Year} {safeBrandName}</p>
            <p class='footer-text'>Todos los derechos reservados</p>
            <p class='footer-text'>Este es un correo automático, por favor no responder.</p>
        </div>
    </div>
</body>
</html>";
    }

    // ───────────────────────────── Creación ─────────────────────────────

    /// <summary>Correo "ticket_creado" a los notificados: info del ticket, quién lo creó y a quién se asignó.</summary>
    public static string Creado(
        Ticket ticket, string? creadorNombre, string? asignadoNombre,
        string logoUrl, string brandName, string brandLine, string applicationUrl)
    {
        var codigo = WebUtility.HtmlEncode(ticket.Codigo ?? $"TK-{ticket.Id}");
        var titulo = WebUtility.HtmlEncode(ticket.Titulo);
        var tipo = WebUtility.HtmlEncode(ticket.Tipo);
        var descripcion = WebUtility.HtmlEncode(ticket.Descripcion);
        var creador = WebUtility.HtmlEncode(creadorNombre ?? "—");
        var asignado = WebUtility.HtmlEncode(asignadoNombre ?? "—");
        var safeUrl = WebUtility.HtmlEncode(applicationUrl);

        var inner = $@"
            <div class='greeting'>Se creó un nuevo ticket</div>
            <div class='message'>Te incluyeron como notificado en el siguiente ticket:</div>
            <div class='info-box'>
                <div class='info-item'>
                    <div class='info-label'>Código</div>
                    <div class='info-value'>{codigo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Título</div>
                    <div class='info-value'>{titulo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Tipo</div>
                    <div class='info-value'>{tipo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Descripción</div>
                    <div class='info-value'>{descripcion}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Creado por</div>
                    <div class='info-value'>{creador}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Asignado a</div>
                    <div class='info-value'>{asignado}</div>
                </div>
            </div>
            <div class='button-container'>
                <a href='{safeUrl}/tickets' class='button'>Ver ticket</a>
            </div>";

        return Wrap(logoUrl, brandName, brandLine, inner);
    }

    // ───────────────────────────── Asignado / transferido ─────────────────────────────

    /// <summary>Correo "ticket_transferido" al nuevo resolutor: le acaban de asignar un ticket.</summary>
    public static string Asignado(
        Ticket ticket, string? nombreDestinatario, string? asignadorNombre,
        string logoUrl, string brandName, string brandLine, string applicationUrl)
    {
        var saludo = string.IsNullOrWhiteSpace(nombreDestinatario) ? "Hola" : $"Hola {WebUtility.HtmlEncode(nombreDestinatario)}";
        var codigo = WebUtility.HtmlEncode(ticket.Codigo ?? $"TK-{ticket.Id}");
        var titulo = WebUtility.HtmlEncode(ticket.Titulo);
        var tipo = WebUtility.HtmlEncode(ticket.Tipo);
        var transferidoPor = WebUtility.HtmlEncode(asignadorNombre ?? "—");
        var safeUrl = WebUtility.HtmlEncode(applicationUrl);

        var inner = $@"
            <div class='greeting'>{saludo},</div>
            <div class='message'>Te asignaron/transfirieron un ticket para que lo gestiones:</div>
            <div class='info-box'>
                <div class='info-item'>
                    <div class='info-label'>Código</div>
                    <div class='info-value'>{codigo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Título</div>
                    <div class='info-value'>{titulo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Tipo</div>
                    <div class='info-value'>{tipo}</div>
                </div>
                <div class='info-item'>
                    <div class='info-label'>Transferido/asignado por</div>
                    <div class='info-value'>{transferidoPor}</div>
                </div>
            </div>
            <div class='button-container'>
                <a href='{safeUrl}/tickets' class='button'>Ver ticket</a>
            </div>";

        return Wrap(logoUrl, brandName, brandLine, inner);
    }

    // ───────────────────────────── Cierre ─────────────────────────────

    /// <summary>Correo "ticket_cerrado": resumen de la solución + histórico de chat (notas públicas).</summary>
    public static string Cerrado(
        Ticket ticket, string? nombreDestinatario, IReadOnlyList<NotaResumen> notasPublicas,
        string logoUrl, string brandName, string brandLine, string applicationUrl)
    {
        var saludo = string.IsNullOrWhiteSpace(nombreDestinatario) ? "Hola" : $"Hola {WebUtility.HtmlEncode(nombreDestinatario)}";
        var codigo = WebUtility.HtmlEncode(ticket.Codigo ?? $"TK-{ticket.Id}");
        var titulo = WebUtility.HtmlEncode(ticket.Titulo);
        var solucion = WebUtility.HtmlEncode(ticket.SolucionDescripcion ?? "—");
        var safeUrl = WebUtility.HtmlEncode(applicationUrl);

        var chatRows = new StringBuilder();
        foreach (var n in notasPublicas)
        {
            var autor = WebUtility.HtmlEncode(n.Autor ?? "—");
            var fecha = n.CreatedAt.ToString("dd/MM/yyyy HH:mm");
            var texto = WebUtility.HtmlEncode(n.Texto);
            chatRows.Append($@"
                <tr>
                    <td>{autor}</td>
                    <td>{fecha}</td>
                    <td>{texto}</td>
                </tr>");
        }

        var chatHtml = notasPublicas.Count == 0
            ? "<p class='message'>Sin novedades registradas en la bitácora pública.</p>"
            : $@"
                <table class='chat-table'>
                    <thead>
                        <tr><th>Autor</th><th>Fecha</th><th>Nota</th></tr>
                    </thead>
                    <tbody>{chatRows}</tbody>
                </table>";

        var inner = $@"
            <div class='greeting'>{saludo},</div>
            <div class='message'>El ticket <strong>{codigo}</strong> — “{titulo}” fue <strong>cerrado</strong>. Este es el resumen:</div>
            <div class='solucion-box'>
                <strong>Solución:</strong><br/>{solucion}
            </div>
            <div class='message'><strong>Histórico de la bitácora (público):</strong></div>
            {chatHtml}
            <div class='button-container'>
                <a href='{safeUrl}/tickets' class='button'>Ver ticket</a>
            </div>";

        return Wrap(logoUrl, brandName, brandLine, inner);
    }
}
