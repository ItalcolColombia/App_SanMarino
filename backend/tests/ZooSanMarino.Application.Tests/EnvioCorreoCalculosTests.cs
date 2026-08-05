using System.Text.Json;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del transporte de correo. Existe porque el envío falla en SILENCIO: si el proveedor se
/// resuelve mal la aplicación arranca igual, los correos se encolan igual y nadie se entera hasta que
/// un usuario reclama que no le llegó la contraseña.
///
/// Contexto: Exchange Online retiró la autenticación básica de SMTP Client Submission
/// (<c>550 5.7.30</c>) y el envío migró a Microsoft Graph con OAuth 2.0. Estos tests son el gate de
/// esa migración, incluida la retrocompatibilidad de desarrollo local (sin Graph configurado ⇒ SMTP).
/// </summary>
public class EnvioCorreoCalculosTests
{
    private const string Tenant = "11111111-1111-1111-1111-111111111111";
    private const string ClientId = "22222222-2222-2222-2222-222222222222";
    private const string Secret = "un-secreto";
    private const string Buzon = "zootecnico@sanmarino.com.co";

    // ── Detección de configuración ──────────────────────────────────────────────────────────

    [Fact]
    public void HayConfiguracionGraph_true_solo_con_los_cuatro_valores()
    {
        Assert.True(EnvioCorreoCalculos.HayConfiguracionGraph(Tenant, ClientId, Secret, Buzon));
    }

    [Theory]
    [InlineData(null, ClientId, Secret, Buzon)]       // falta tenant
    [InlineData(Tenant, null, Secret, Buzon)]         // falta client id
    [InlineData(Tenant, ClientId, null, Buzon)]       // falta secret
    [InlineData(Tenant, ClientId, Secret, null)]      // falta buzón
    [InlineData(Tenant, ClientId, "   ", Buzon)]      // secret en blanco
    [InlineData("", "", "", "")]
    public void HayConfiguracionGraph_parcial_equivale_a_ausente(
        string? tenant, string? clientId, string? secret, string? buzon)
    {
        // Config a medias NO habilita Graph: es preferible caer al SMTP conocido que fallar en cada correo.
        Assert.False(EnvioCorreoCalculos.HayConfiguracionGraph(tenant, clientId, secret, buzon));
    }

    [Fact]
    public void HayConfiguracionSmtp_exige_host_usuario_y_password()
    {
        Assert.True(EnvioCorreoCalculos.HayConfiguracionSmtp("smtp.office365.com", "u", "p"));
        Assert.False(EnvioCorreoCalculos.HayConfiguracionSmtp("smtp.office365.com", "u", null));
        Assert.False(EnvioCorreoCalculos.HayConfiguracionSmtp(null, "u", "p"));
        Assert.False(EnvioCorreoCalculos.HayConfiguracionSmtp("smtp.office365.com", "  ", "p"));
    }

    // ── Resolución del proveedor ────────────────────────────────────────────────────────────

    [Theory]
    // provider explícito con su config completa
    [InlineData("graph", true, true, ProveedorCorreo.Graph)]
    [InlineData("graph", true, false, ProveedorCorreo.Graph)]
    [InlineData("smtp", true, true, ProveedorCorreo.Smtp)]
    [InlineData("smtp", false, true, ProveedorCorreo.Smtp)]
    // provider explícito SIN su config ⇒ no cae al otro por su cuenta
    [InlineData("graph", false, true, ProveedorCorreo.NoConfigurado)]
    [InlineData("smtp", true, false, ProveedorCorreo.NoConfigurado)]
    // auto-detección
    [InlineData(null, true, true, ProveedorCorreo.Graph)]
    [InlineData("", true, false, ProveedorCorreo.Graph)]
    [InlineData("auto", true, true, ProveedorCorreo.Graph)]
    [InlineData(null, false, true, ProveedorCorreo.Smtp)]
    [InlineData("auto", false, true, ProveedorCorreo.Smtp)]
    [InlineData(null, false, false, ProveedorCorreo.NoConfigurado)]
    public void ResolverProveedor_cubre_la_tabla_de_decision(
        string? provider, bool hayGraph, bool haySmtp, ProveedorCorreo esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.ResolverProveedor(provider, hayGraph, haySmtp));
    }

    [Fact]
    public void ResolverProveedor_sin_graph_configurado_conserva_el_comportamiento_de_desarrollo_local()
    {
        // appsettings.Development.json no tiene sección Email:Graph ⇒ debe seguir usando SMTP como siempre.
        Assert.Equal(
            ProveedorCorreo.Smtp,
            EnvioCorreoCalculos.ResolverProveedor(provider: null, hayGraph: false, haySmtp: true));
    }

    [Theory]
    [InlineData(" GRAPH ")]
    [InlineData("Graph")]
    [InlineData("gRaPh")]
    public void ResolverProveedor_ignora_mayusculas_y_espacios(string provider)
    {
        Assert.Equal(
            ProveedorCorreo.Graph,
            EnvioCorreoCalculos.ResolverProveedor(provider, hayGraph: true, haySmtp: true));
    }

    [Fact]
    public void ResolverProveedor_un_valor_desconocido_cae_en_la_auto_deteccion()
    {
        // Un typo en la variable de entorno no debe dejar la aplicación sin correo si hay config válida.
        Assert.Equal(
            ProveedorCorreo.Graph,
            EnvioCorreoCalculos.ResolverProveedor("grahp", hayGraph: true, haySmtp: true));
    }

    // ── Vigencia del token ──────────────────────────────────────────────────────────────────

    [Fact]
    public void TokenVigente_falso_cuando_ya_vencio()
    {
        var ahora = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.False(EnvioCorreoCalculos.TokenVigente(ahora.AddMinutes(-1), ahora));
    }

    [Fact]
    public void TokenVigente_falso_dentro_del_margen_de_renovacion()
    {
        // Vence en 4 min: entra en el margen de 5 ⇒ se renueva antes de usarlo.
        var ahora = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.False(EnvioCorreoCalculos.TokenVigente(ahora.AddMinutes(4), ahora));
    }

    [Fact]
    public void TokenVigente_true_fuera_del_margen()
    {
        var ahora = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);
        Assert.True(EnvioCorreoCalculos.TokenVigente(ahora.AddMinutes(30), ahora));
    }

    [Fact]
    public void CalcularVencimientoToken_usa_expires_in_en_segundos()
    {
        var ahora = new DateTimeOffset(2026, 8, 5, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(ahora.AddSeconds(3599), EnvioCorreoCalculos.CalcularVencimientoToken(3599, ahora));
        // Un expires_in ausente o absurdo no debe producir un token "eterno".
        Assert.Equal(ahora, EnvioCorreoCalculos.CalcularVencimientoToken(0, ahora));
        Assert.Equal(ahora, EnvioCorreoCalculos.CalcularVencimientoToken(-10, ahora));
    }

    // ── Payload de /sendMail ────────────────────────────────────────────────────────────────

    [Fact]
    public void ConstruirPayloadSendMail_serializa_la_estructura_que_espera_graph()
    {
        var payload = EnvioCorreoCalculos.ConstruirPayloadSendMail(
            buzonRemitente: Buzon,
            nombreRemitente: "ZooSanMarino - Sistema Zootécnico",
            destinatario: "usuario@granja.com",
            asunto: "Recuperación de contraseña",
            cuerpoHtml: "<html><body>hola</body></html>",
            guardarEnEnviados: false);

        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(payload));
        var raiz = doc.RootElement;
        var mensaje = raiz.GetProperty("message");

        Assert.Equal("Recuperación de contraseña", mensaje.GetProperty("subject").GetString());
        Assert.Equal("HTML", mensaje.GetProperty("body").GetProperty("contentType").GetString());
        Assert.Equal("<html><body>hola</body></html>", mensaje.GetProperty("body").GetProperty("content").GetString());

        var destinatarios = mensaje.GetProperty("toRecipients");
        Assert.Equal(1, destinatarios.GetArrayLength());
        Assert.Equal(
            "usuario@granja.com",
            destinatarios[0].GetProperty("emailAddress").GetProperty("address").GetString());

        Assert.False(raiz.GetProperty("saveToSentItems").GetBoolean());
    }

    [Fact]
    public void ConstruirPayloadSendMail_el_from_es_el_mismo_buzon_de_la_url()
    {
        // Enviar "from" con OTRA dirección exigiría el permiso SendAs y Graph respondería 403.
        // Con la misma dirección se conserva el nombre visible del remitente sin permisos extra.
        var payload = EnvioCorreoCalculos.ConstruirPayloadSendMail(
            Buzon, "ZooSanMarino - Sistema Zootécnico", "usuario@granja.com", "Asunto", "<p>x</p>", false);

        Assert.Equal(Buzon, payload.Message.From.EmailAddress.Address);
        Assert.Equal("ZooSanMarino - Sistema Zootécnico", payload.Message.From.EmailAddress.Name);
    }

    [Fact]
    public void ConstruirPayloadSendMail_sin_nombre_de_remitente_omite_el_nombre()
    {
        var payload = EnvioCorreoCalculos.ConstruirPayloadSendMail(
            Buzon, "   ", "usuario@granja.com", "Asunto", "<p>x</p>", true);

        Assert.Null(payload.Message.From.EmailAddress.Name);
        Assert.True(payload.SaveToSentItems);
    }

    // ── Clasificación de errores ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(401, "graph_auth")]
    [InlineData(403, "graph_permisos")]
    [InlineData(404, "graph_buzon")]
    [InlineData(429, "graph_throttling")]
    [InlineData(500, "graph_transitorio")]
    [InlineData(503, "graph_transitorio")]
    [InlineData(400, "graph_http_400")]
    [InlineData(413, "graph_http_413")]
    public void ClasificarErrorGraph_mapea_el_estado_http_a_un_tipo_estable(int httpStatus, string esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.ClasificarErrorGraph(httpStatus));
    }

    [Theory]
    [InlineData(429, true)]
    [InlineData(500, true)]
    [InlineData(503, true)]
    [InlineData(401, false)]
    [InlineData(403, false)]
    [InlineData(404, false)]
    [InlineData(400, false)]
    public void EsErrorTransitorioGraph_solo_throttling_y_fallas_del_servicio(int httpStatus, bool esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.EsErrorTransitorioGraph(httpStatus));
    }

    // ── Diagnósticos accionables ────────────────────────────────────────────────────────────

    [Fact]
    public void DiagnosticoGraph_403_explica_el_permiso_que_falta()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoGraph(
            403, "ErrorAccessDenied", "Access is denied.", Buzon, ClientId);

        Assert.Contains("Mail.Send", texto);
        Assert.Contains("consentimiento", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(Buzon, texto);
        Assert.Contains(ClientId, texto);
    }

    [Fact]
    public void DiagnosticoGraph_401_apunta_al_client_secret()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoGraph(
            401, "InvalidAuthenticationToken", "Access token is empty.", Buzon, ClientId);

        Assert.Contains("ClientSecret", texto);
        Assert.Contains("HTTP Status: 401", texto);
    }

    [Fact]
    public void DiagnosticoGraph_404_apunta_al_buzon()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoGraph(404, "ResourceNotFound", null, Buzon, ClientId);

        Assert.Contains("SenderMailbox", texto);
        Assert.Contains("(no informado)", texto); // el mensaje ausente no rompe el diagnóstico
    }

    [Fact]
    public void DiagnosticoTokenGraph_invalid_client_dice_que_el_secreto_vencio()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoTokenGraph(
            401, "invalid_client", "AADSTS7000215: Invalid client secret provided.", Tenant, ClientId);

        Assert.Contains("client secret", texto, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Certificates & secrets", texto);
        Assert.Contains(Tenant, texto);
    }

    [Fact]
    public void DiagnosticoSinProveedor_enumera_lo_que_falta_y_advierte_del_retiro_de_smtp()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoSinProveedor("graph");

        Assert.Contains("Email:Graph:TenantId", texto);
        Assert.Contains("Email:Graph:ClientSecret", texto);
        Assert.Contains("Email:Smtp:Host", texto);
        Assert.Contains("550 5.7.30", texto);
        Assert.Contains("graph", texto);
    }

    [Fact]
    public void DiagnosticoSinProveedor_sin_provider_explicito_lo_reporta_como_auto()
    {
        Assert.Contains("(auto)", EnvioCorreoCalculos.DiagnosticoSinProveedor(null));
        Assert.Contains("(auto)", EnvioCorreoCalculos.DiagnosticoSinProveedor("   "));
    }
}
