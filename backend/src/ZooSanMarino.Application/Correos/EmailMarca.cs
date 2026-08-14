namespace ZooSanMarino.Application.Correos;

/// <summary>
/// Resuelve los logos del encabezado de los correos.
///
/// El encabezado replica el de la pantalla de ingreso: **Italcol arriba y San Marino debajo**, que es
/// la identidad que el usuario reconoce al entrar a la plataforma. Hasta el 12-ago-2026 los correos
/// mostraban en su lugar el logo de Italfoods (`logo_intalfoods_zootenico.png`), que no aparece en
/// ninguna pantalla de la aplicación.
///
/// Los archivos los sirve el propio frontend, así que la ruta se arma sobre
/// <c>Email:ApplicationUrl</c> salvo que la configuración traiga una URL explícita.
/// </summary>
public static class EmailMarca
{
    /// <summary>Logo superior (Italcol). Mismo archivo que usa `login.component.html`.</summary>
    public const string RutaItalcol = "/assets/brand/italcol-naraanja.png";

    /// <summary>Logo inferior (San Marino · Genética Avícola).</summary>
    public const string RutaSanMarino = "/assets/brand/Logo-sanmarino-innovacion.png";

    /// <summary>URL del logo principal: la configurada, o la derivada del frontend.</summary>
    public static string LogoPrincipal(string? applicationUrl, string? configurado) =>
        Resolver(applicationUrl, configurado, RutaItalcol);

    /// <summary>URL del logo secundario: la configurada, o la derivada del frontend.</summary>
    public static string LogoSecundario(string? applicationUrl, string? configurado) =>
        Resolver(applicationUrl, configurado, RutaSanMarino);

    private static string Resolver(string? applicationUrl, string? configurado, string rutaPorDefecto)
    {
        if (!string.IsNullOrWhiteSpace(configurado))
            return configurado;

        var baseUrl = (applicationUrl ?? string.Empty).TrimEnd('/');
        return string.IsNullOrEmpty(baseUrl) ? string.Empty : $"{baseUrl}{rutaPorDefecto}";
    }
}
