using ZooSanMarino.Application.Correos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Notificaciones de tickets (creación, asignación/transferencia, solución y cierre).
///
/// Desde el 12-ago-2026 no traen su propio HTML: arman el cuerpo con <see cref="EmailComponentes"/>
/// y lo envuelven con <see cref="EmailLayout"/>, el mismo layout que usan los correos de cuenta.
/// Antes convivían tres maquetados distintos y el aviso de "solucionado" ni siquiera pasaba por acá
/// — se armaba a mano dentro de <c>TicketService</c>, sin logo ni pie.
/// </summary>
public static class TicketEmailTemplates
{
    /// <summary>Fila liviana de nota pública para el histórico de chat del correo de cierre.</summary>
    public sealed record NotaResumen(string? Autor, DateTime CreatedAt, string Texto);

    // ───────────────────────────── Layout compartido ─────────────────────────────

    /// <summary>
    /// Envuelve el contenido en el layout branded. <paramref name="brandLine"/> llega como
    /// "Marca · lema"; se le quita el prefijo para no repetir el nombre debajo del logo.
    /// </summary>
    public static string Wrap(string logoUrl, string brandName, string brandLine, string innerHtml)
        => Wrap(logoUrl, brandName, brandLine, innerHtml, brandName, string.Empty, null, string.Empty, null);

    private static string Wrap(
        string logoUrl, string brandName, string brandLine, string innerHtml,
        string titulo, string preheader, string? motivoEnvio,
        string applicationUrl, string? logoSecundarioUrl)
    {
        var prefijo = $"{brandName} · ";
        var lema = brandLine.StartsWith(prefijo, StringComparison.Ordinal)
            ? brandLine[prefijo.Length..]
            : brandLine;

        return EmailLayout.Documento(
            titulo: titulo,
            preheader: preheader,
            logoUrl: logoUrl,
            marca: brandName,
            lema: lema,
            contenidoHtml: innerHtml,
            motivoEnvio: motivoEnvio ?? "Recibís este correo por tu participación en este ticket de soporte.",
            logoSecundarioUrl: EmailMarca.LogoSecundario(applicationUrl, logoSecundarioUrl));
    }

    /// <summary>Etiquetas de tipo y prioridad. La prioridad manda el color; el tipo va neutro.</summary>
    private static string Etiquetas(Ticket ticket)
    {
        var (color, fondo) = (ticket.Prioridad ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "critica" or "crítica" or "urgente" => (EmailTema.Peligro, EmailTema.PeligroFondo),
            "alta"                              => (EmailTema.Aviso, EmailTema.AvisoFondo),
            "baja"                              => (EmailTema.TextoTenue, EmailTema.SuperficieSuave),
            _                                   => (EmailTema.Info, EmailTema.InfoFondo)
        };

        var etiquetas = EmailComponentes.Badge(ticket.Tipo ?? "—", EmailTema.Info, EmailTema.InfoFondo);
        if (!string.IsNullOrWhiteSpace(ticket.Prioridad))
            etiquetas += EmailComponentes.Badge($"Prioridad {ticket.Prioridad}", color, fondo);

        return $"<div style=\"margin:0 0 18px 0;\">{etiquetas}</div>";
    }

    private static string Codigo(Ticket ticket) => ticket.Codigo ?? $"TK-{ticket.Id}";

    private static string UrlTickets(string applicationUrl) =>
        $"{(applicationUrl ?? string.Empty).TrimEnd('/')}/tickets";

    // ───────────────────────────── Creación ─────────────────────────────

    /// <summary>Correo "ticket_creado" a los notificados: info del ticket, quién lo creó y a quién se asignó.</summary>
    public static string Creado(
        Ticket ticket, string? creadorNombre, string? asignadoNombre,
        string logoUrl, string brandName, string brandLine, string applicationUrl,
        string? logoSecundarioUrl = null)
    {
        var codigo = Codigo(ticket);

        var inner = string.Concat(
            EmailComponentes.Titulo($"Nuevo ticket {codigo}"),
            Etiquetas(ticket),
            EmailComponentes.Parrafo("Te incluyeron como notificado en el siguiente caso:"),
            EmailComponentes.Ficha(
                ("Código", codigo),
                ("Título", ticket.Titulo ?? "—"),
                ("Creado por", creadorNombre ?? "—"),
                ("Asignado a", asignadoNombre ?? "—")),
            EmailComponentes.Cita("Descripción", ticket.Descripcion ?? "—", EmailTema.Info),
            EmailComponentes.Boton(UrlTickets(applicationUrl), "Ver ticket"));

        return Wrap(logoUrl, brandName, brandLine, inner,
            titulo: $"Nuevo ticket {codigo} · {brandName}",
            preheader: $"{codigo} — {ticket.Titulo}",
            motivoEnvio: "Recibís este correo porque te incluyeron como notificado de este ticket.",
            applicationUrl: applicationUrl, logoSecundarioUrl: logoSecundarioUrl);
    }

    // ───────────────────────────── Asignado / transferido ─────────────────────────────

    /// <summary>Correo "ticket_transferido" al nuevo resolutor: le acaban de asignar un ticket.</summary>
    public static string Asignado(
        Ticket ticket, string? nombreDestinatario, string? asignadorNombre,
        string logoUrl, string brandName, string brandLine, string applicationUrl,
        string? logoSecundarioUrl = null)
    {
        var codigo = Codigo(ticket);

        var inner = string.Concat(
            EmailComponentes.Titulo($"Te asignaron el ticket {codigo}"),
            EmailComponentes.Saludo(nombreDestinatario),
            Etiquetas(ticket),
            EmailComponentes.ParrafoHtml(
                $"<strong>{System.Net.WebUtility.HtmlEncode(asignadorNombre ?? "Un compañero")}</strong> te transfirió este caso para que lo gestiones."),
            EmailComponentes.Ficha(
                ("Código", codigo),
                ("Título", ticket.Titulo ?? "—"),
                ("Estado actual", ticket.Estado ?? "—")),
            EmailComponentes.Cita("Descripción", ticket.Descripcion ?? "—", EmailTema.Info),
            EmailComponentes.Boton(UrlTickets(applicationUrl), "Gestionar ticket"));

        return Wrap(logoUrl, brandName, brandLine, inner,
            titulo: $"Te asignaron el ticket {codigo} · {brandName}",
            preheader: $"{codigo} — {ticket.Titulo}",
            motivoEnvio: "Recibís este correo porque quedaste como responsable de este ticket.",
            applicationUrl: applicationUrl, logoSecundarioUrl: logoSecundarioUrl);
    }

    // ───────────────────────────── Solucionado ─────────────────────────────

    /// <summary>
    /// Correo "ticket_solucionado" al solicitante: la solución y el pedido explícito de confirmar el
    /// cierre. Es el paso donde la plataforma necesita una acción del usuario, así que el botón
    /// nombra esa acción y no un genérico "ver ticket".
    /// </summary>
    public static string Solucionado(
        Ticket ticket, string? nombreSolicitante,
        string logoUrl, string brandName, string brandLine, string applicationUrl,
        string? logoSecundarioUrl = null)
    {
        var codigo = Codigo(ticket);

        var inner = string.Concat(
            EmailComponentes.Titulo("Tu ticket fue solucionado"),
            EmailComponentes.Saludo(nombreSolicitante),
            EmailComponentes.ParrafoHtml(
                $"El ticket <strong>{System.Net.WebUtility.HtmlEncode(codigo)}</strong> — " +
                $"“{System.Net.WebUtility.HtmlEncode(ticket.Titulo ?? "—")}” fue marcado como <strong>solucionado</strong>."),
            EmailComponentes.Cita("Solución aplicada", ticket.SolucionDescripcion ?? "—", EmailTema.Exito),
            EmailComponentes.CalloutExito(
                "Falta tu confirmación",
                "Revisá la solución y confirmá el cierre. Si algo quedó pendiente, podés reabrir el caso desde la plataforma."),
            EmailComponentes.Boton(UrlTickets(applicationUrl), "Revisar y confirmar", EmailTema.Exito, "#20602f"));

        return Wrap(logoUrl, brandName, brandLine, inner,
            titulo: $"Ticket {codigo} solucionado · {brandName}",
            preheader: $"{codigo} solucionado. Revisá la solución y confirmá el cierre.",
            motivoEnvio: "Recibís este correo porque sos el solicitante de este ticket.",
            applicationUrl: applicationUrl, logoSecundarioUrl: logoSecundarioUrl);
    }

    // ───────────────────────────── Cierre ─────────────────────────────

    /// <summary>Correo "ticket_cerrado": resumen de la solución + histórico de chat (notas públicas).</summary>
    public static string Cerrado(
        Ticket ticket, string? nombreDestinatario, IReadOnlyList<NotaResumen> notasPublicas,
        string logoUrl, string brandName, string brandLine, string applicationUrl,
        string? logoSecundarioUrl = null)
    {
        var codigo = Codigo(ticket);

        var inner = string.Concat(
            EmailComponentes.Titulo($"Ticket {codigo} cerrado"),
            EmailComponentes.Saludo(nombreDestinatario),
            EmailComponentes.ParrafoHtml(
                $"El caso “{System.Net.WebUtility.HtmlEncode(ticket.Titulo ?? "—")}” quedó <strong>cerrado</strong>. Este es el resumen:"),
            EmailComponentes.Cita("Solución", ticket.SolucionDescripcion ?? "—", EmailTema.Exito),
            EmailComponentes.Separador(),
            EmailComponentes.ParrafoHtml("<strong>Bitácora pública del caso</strong>"),
            EmailComponentes.Bitacora(notasPublicas.Select(n => (
                Autor: n.Autor ?? "—",
                Fecha: n.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                Texto: n.Texto))),
            EmailComponentes.Boton(UrlTickets(applicationUrl), "Ver historial completo"));

        return Wrap(logoUrl, brandName, brandLine, inner,
            titulo: $"Ticket {codigo} cerrado · {brandName}",
            preheader: $"{codigo} cerrado. Resumen de la solución y de la bitácora.",
            motivoEnvio: "Recibís este correo por tu participación en este ticket de soporte.",
            applicationUrl: applicationUrl, logoSecundarioUrl: logoSecundarioUrl);
    }
}
