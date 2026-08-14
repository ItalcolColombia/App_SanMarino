using ZooSanMarino.Application.Correos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de los cuerpos de correo.
///
/// Existe por un defecto concreto (12-ago-2026): <c>AuthService</c> emitía un token de un solo uso y
/// lo pasaba por un parámetro llamado <c>newPassword</c>; la plantilla lo imprimía bajo el rótulo
/// «Tu nueva contraseña es». Quien pedía recuperar su cuenta recibía 64 caracteres que nunca fueron
/// una contraseña, y no tenía dónde canjearlos. Estos tests fijan que el correo de restablecimiento
/// lleve un ENLACE y que el secreto no vuelva a viajar disfrazado de credencial.
///
/// El resto cubre lo que un correo mal armado rompe en silencio: HTML sin escapar (un título de
/// ticket con <c>&lt;</c> desarma el mensaje) y el preheader, que si falta hace que la bandeja de
/// entrada muestre "Hola, ..." como resumen.
/// </summary>
public class CorreosCuentaTests
{
    private const string Marca = "ItalGranja";
    private const string Lema = "Gestión de granjas avícolas · Italcol";
    private const string Logo = "https://zootecnico.sanmarino.com.co/assets/brand/logo.png";
    private const string AppUrl = "https://zootecnico.sanmarino.com.co";

    /// <summary>
    /// El render escapa con <c>WebUtility.HtmlEncode</c>, que además de <c>&lt; &gt; &amp;</c>
    /// convierte los acentos y la ñ a entidades numéricas (<c>Contrase&amp;#241;a</c>). Comparar
    /// contra el literal en español daría un <c>DoesNotContain</c> que pasa siempre — y no probaría
    /// nada. Todo texto esperado con acentos pasa por acá.
    /// </summary>
    private static string Esc(string texto) => System.Net.WebUtility.HtmlEncode(texto);

    // ── El defecto que motivó todo ───────────────────────────────────────────────────────────

    [Fact]
    public void Restablecer_no_presenta_el_token_como_contrasena()
    {
        const string token = "aB3xY9zQ7mN2pL5kR8tV4wC6dF1gH0jS";
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, Logo, AppUrl, token, "Moisés");

        // El rótulo que hacía que el usuario intentara entrar con el token.
        Assert.DoesNotContain(Esc("nueva contraseña es"), html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Esc("Contraseña temporal"), html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Restablecer_lleva_el_token_solo_dentro_del_enlace()
    {
        const string token = "TOKEN123";
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, Logo, AppUrl, token, null);

        var enlace = CorreosCuenta.ConstruirEnlaceRestablecer(AppUrl, token);
        Assert.Contains(enlace, html, StringComparison.Ordinal);

        // El token aparece únicamente como parte del enlace (botón + enlace de respaldo), nunca suelto.
        var apariciones = System.Text.RegularExpressions.Regex.Matches(html, token).Count;
        var aparicionesEnEnlace = System.Text.RegularExpressions.Regex
            .Matches(html, System.Text.RegularExpressions.Regex.Escape($"token={token}")).Count;
        Assert.Equal(apariciones, aparicionesEnEnlace);
    }

    [Fact]
    public void Enlace_de_restablecimiento_apunta_a_la_ruta_del_front_y_codifica_el_token()
    {
        // El token es base64 CSPRNG: sin codificar, un '+' llega al frontend como espacio.
        var enlace = CorreosCuenta.ConstruirEnlaceRestablecer("https://app.test/", "a+b/c=d");

        Assert.StartsWith("https://app.test/reset-password?token=", enlace, StringComparison.Ordinal);
        Assert.Contains("a%2Bb%2Fc%3Dd", enlace, StringComparison.Ordinal);
        Assert.DoesNotContain("//reset-password", enlace, StringComparison.Ordinal); // barra final duplicada
    }

    [Fact]
    public void Restablecer_avisa_la_vigencia_y_el_camino_si_no_fue_el_usuario()
    {
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, Logo, AppUrl, "t", "Ana");

        Assert.Contains($"{CorreosCuenta.MinutosVigencia} minutos", html, StringComparison.Ordinal);
        Assert.Contains("Si no pediste este cambio", html, StringComparison.Ordinal);
    }

    // ── El reset del administrador SÍ manda una contraseña ───────────────────────────────────

    [Fact]
    public void Reset_de_administrador_muestra_la_contrasena_asignada()
    {
        var html = CorreosCuenta.ContrasenaRestablecidaPorAdmin(Marca, Lema, Logo, AppUrl, "Temp0ral!", "Luis");

        Assert.Contains("Temp0ral!", html, StringComparison.Ordinal);
        Assert.Contains(Esc("Contraseña temporal"), html, StringComparison.Ordinal);
        Assert.Contains($"{AppUrl}/login", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Bienvenida_incluye_usuario_contrasena_y_acceso()
    {
        var html = CorreosCuenta.Bienvenida(Marca, Lema, Logo, AppUrl, "ana@italcol.com", "Cl4ve!", "Ana Pérez");

        Assert.Contains("ana@italcol.com", html, StringComparison.Ordinal);
        Assert.Contains("Cl4ve!", html, StringComparison.Ordinal);
        Assert.Contains($"{AppUrl}/login", html, StringComparison.Ordinal);
        Assert.Contains(Esc("Hola Ana Pérez,"), html, StringComparison.Ordinal);
    }

    // ── Seguridad del render ────────────────────────────────────────────────────────────────

    [Fact]
    public void El_nombre_del_usuario_se_escapa()
    {
        var html = CorreosCuenta.Bienvenida(
            Marca, Lema, Logo, AppUrl, "x@y.z", "p", "<script>alert('x')</script>");

        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("&lt;script&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void La_contrasena_con_caracteres_html_se_escapa()
    {
        var html = CorreosCuenta.ContrasenaRestablecidaPorAdmin(Marca, Lema, Logo, AppUrl, "a<b>&c", null);

        Assert.Contains("a&lt;b&gt;&amp;c", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_nombre_el_saludo_no_queda_colgado()
    {
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, Logo, AppUrl, "t", null);

        Assert.Contains("Hola,", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Hola ,", html, StringComparison.Ordinal);
    }

    // ── Estructura del documento ────────────────────────────────────────────────────────────

    [Fact]
    public void Todo_correo_trae_preheader_para_la_bandeja_de_entrada()
    {
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, Logo, AppUrl, "t", "Ana");

        // El bloque oculto que la bandeja usa como resumen junto al asunto.
        Assert.Contains("display:none", html, StringComparison.Ordinal);
        Assert.Contains("El enlace vence en", html, StringComparison.Ordinal);
    }

    [Fact]
    public void El_documento_es_html_completo_y_ancho_seguro_de_correo()
    {
        var html = EmailLayout.Documento("T", "P", Logo, Marca, Lema, "<p>hola</p>");

        Assert.StartsWith("<!DOCTYPE html>", html.TrimStart(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("</html>", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"width=\"{EmailTema.AnchoMaximo}\"", html, StringComparison.Ordinal);
        Assert.Contains("<p>hola</p>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Sin_logo_el_encabezado_cae_al_nombre_de_la_marca()
    {
        var html = EmailLayout.Documento("T", "P", "", Marca, Lema, "<p>x</p>");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Marca, html, StringComparison.Ordinal);
    }

    [Fact]
    public void Con_logo_el_texto_alternativo_lleva_tipografia_para_cuando_la_imagen_no_carga()
    {
        // Outlook de escritorio no descarga imágenes remotas nunca, y Gmail tampoco ante un
        // remitente desconocido: ese lector ve el alt, no el logo. Sin tipografía propia queda una
        // leyenda diminuta al lado de un ícono roto.
        var html = EmailLayout.Documento("T", "P", Logo, Marca, Lema, "<p>x</p>");

        var img = html.Substring(html.IndexOf("<img", StringComparison.Ordinal));
        img = img.Substring(0, img.IndexOf("/>", StringComparison.Ordinal));

        Assert.Contains("alt=\"Italcol\"", img, StringComparison.Ordinal);
        Assert.Contains("font-weight:700", img, StringComparison.Ordinal);
        Assert.Contains("font-size:", img, StringComparison.Ordinal);
    }

    // ── Encabezado: los mismos logos que la pantalla de ingreso ─────────────────────────────

    [Fact]
    public void El_encabezado_lleva_los_dos_logos_del_login()
    {
        // El logo principal llega ya resuelto desde el servicio (así se puede configurar); el
        // secundario lo completa el propio cuerpo si no vino.
        var logoResuelto = EmailMarca.LogoPrincipal(AppUrl, null);
        var html = CorreosCuenta.RestablecerContrasena(Marca, Lema, logoResuelto, AppUrl, "t", "Ana");

        Assert.Contains(EmailMarca.RutaItalcol, html, StringComparison.Ordinal);
        Assert.Contains(EmailMarca.RutaSanMarino, html, StringComparison.Ordinal);
        // El alt es un literal del layout (no un dato del usuario), así que no pasa por HtmlEncode.
        Assert.Contains("alt=\"San Marino · Genética Avícola\"", html, StringComparison.Ordinal);

        // El de Italfoods no aparece en ninguna pantalla de la aplicación: no va en los correos.
        Assert.DoesNotContain("intalfoods", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Los_logos_van_sobre_fondo_blanco_fijo_para_que_el_modo_oscuro_no_los_tape()
    {
        // Los archivos están diseñados para fondo claro: el rojo de San Marino y el gris de
        // "Genética avícola" se pierden sobre el lienzo del modo noche.
        var html = CorreosCuenta.Bienvenida(Marca, Lema, Logo, AppUrl, "a@b.c", "p", "Ana");

        var encabezado = html.Substring(0, html.IndexOf("<img", StringComparison.Ordinal));
        Assert.Contains("background-color:#ffffff", encabezado, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://app.test/", "https://app.test/assets/brand/italcol-naraanja.png")]
    [InlineData("https://app.test", "https://app.test/assets/brand/italcol-naraanja.png")]
    public void El_logo_se_deriva_del_frontend_cuando_no_esta_configurado(string appUrl, string esperado)
    {
        Assert.Equal(esperado, EmailMarca.LogoPrincipal(appUrl, null));
        Assert.Equal(esperado, EmailMarca.LogoPrincipal(appUrl, "   "));
    }

    [Fact]
    public void La_url_configurada_del_logo_gana_sobre_la_derivada()
    {
        Assert.Equal("https://cdn.test/x.png", EmailMarca.LogoSecundario("https://app.test", "https://cdn.test/x.png"));
    }

    [Fact]
    public void Sin_logos_el_encabezado_sigue_mostrando_la_marca()
    {
        var html = EmailLayout.Documento("T", "P", "", Marca, Lema, "<p>x</p>", logoSecundarioUrl: "");

        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Marca, html, StringComparison.Ordinal);
    }

    [Fact]
    public void El_layout_no_usa_recursos_que_outlook_descarta()
    {
        var html = CorreosCuenta.Bienvenida(Marca, Lema, Logo, AppUrl, "a@b.c", "p", "Ana");

        // Outlook (motor de Word) ignora estas propiedades y deja el botón ilegible o el layout roto.
        Assert.DoesNotContain("linear-gradient", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display:flex", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("display:grid", html, StringComparison.OrdinalIgnoreCase);
    }

    // ── Componentes ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Boton_conserva_la_url_recibida()
    {
        var html = EmailComponentes.Boton("https://app.test/x?a=1&b=2", "Ir");

        Assert.Contains("https://app.test/x?a=1&amp;b=2", html, StringComparison.Ordinal);
        Assert.Contains(">Ir<", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Ficha_vacia_no_emite_tabla()
    {
        Assert.Equal(string.Empty, EmailComponentes.Ficha());
    }

    [Fact]
    public void Bitacora_sin_notas_avisa_en_vez_de_dejar_una_tabla_hueca()
    {
        var html = EmailComponentes.Bitacora(Array.Empty<(string, string, string)>());

        Assert.Contains("Sin novedades", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<table", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Bitacora_escapa_autor_y_texto()
    {
        var html = EmailComponentes.Bitacora(new[] { ("<b>Ana</b>", "01/01/2026 10:00", "rompe & <esto>") });

        Assert.DoesNotContain("<b>Ana</b>", html, StringComparison.Ordinal);
        Assert.Contains("&lt;b&gt;Ana&lt;/b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("rompe &amp; &lt;esto&gt;", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Pasos_numera_desde_uno()
    {
        var html = EmailComponentes.Pasos("uno", "dos");

        Assert.Contains(">1<", html, StringComparison.Ordinal);
        Assert.Contains(">2<", html, StringComparison.Ordinal);
        Assert.Contains("uno", html, StringComparison.Ordinal);
    }
}
