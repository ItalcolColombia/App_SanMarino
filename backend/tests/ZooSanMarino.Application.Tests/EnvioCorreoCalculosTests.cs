using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del envío de correo por SMTP. Existe porque el envío falla en SILENCIO: los correos se
/// encolan igual y nadie se entera hasta que un usuario reclama que no le llegó la contraseña, así
/// que lo único que queda es el <c>error_type</c> y el diagnóstico guardados en <c>email_queue</c>.
///
/// Contexto (05-ago-2026): un rechazo por POLÍTICA del tenant se venía leyendo durante meses como
/// "contraseña incorrecta" o "hay que habilitar SMTP AUTH". Se verificó que las credenciales
/// autentican (235) y que este mismo código envía correctamente; estos tests fijan la clasificación
/// para que el motivo guardado no vuelva a mentir.
/// </summary>
public class EnvioCorreoCalculosTests
{
    // ── Configuración ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void HayConfiguracionSmtp_exige_host_usuario_y_password()
    {
        Assert.True(EnvioCorreoCalculos.HayConfiguracionSmtp("smtp.office365.com", "u", "p"));
    }

    [Theory]
    [InlineData(null, "u", "p")]
    [InlineData("smtp.office365.com", null, "p")]
    [InlineData("smtp.office365.com", "u", null)]
    [InlineData("smtp.office365.com", "  ", "p")]
    [InlineData("", "", "")]
    public void HayConfiguracionSmtp_falta_cualquiera_y_no_alcanza(string? host, string? user, string? pass)
    {
        Assert.False(EnvioCorreoCalculos.HayConfiguracionSmtp(host, user, pass));
    }

    [Fact]
    public void DiagnosticoSinConfiguracion_enumera_las_tres_variables()
    {
        var texto = EnvioCorreoCalculos.DiagnosticoSinConfiguracion();

        Assert.Contains("Email:Smtp:Host", texto);
        Assert.Contains("Email:Smtp:Username", texto);
        Assert.Contains("Email:Smtp:Password", texto);
        Assert.Contains("Email__Smtp__Host", texto); // el nombre que se usa en ECS
    }

    // ── Clasificación del error (valores históricos de email_queue.error_type) ───────────────

    [Theory]
    [InlineData("535 5.7.139 Authentication unsuccessful", "smtp_auth")]
    [InlineData("Authentication unsuccessful, the request did not meet the criteria", "smtp_auth")]
    [InlineData("Status Code: 535", "smtp_auth")]
    public void ClasificarErrorSmtp_reconoce_el_rechazo_de_autenticacion(string detalle, string esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.ClasificarErrorSmtp(detalle));
    }

    [Theory]
    [InlineData("network unreachable", "network")]
    [InlineData("timeout expired", "network")]
    [InlineData("connection refused", "network")]
    public void ClasificarErrorSmtp_reconoce_los_problemas_de_red(string detalle, string esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.ClasificarErrorSmtp(detalle));
    }

    [Fact]
    public void ClasificarErrorSmtp_hueco_conocido_timed_out_no_se_reconoce_como_red()
    {
        // El clasificador busca "timeout", pero el mensaje real de .NET dice "timed out" (con espacio),
        // así que un timeout de conexión se guarda como "unknown". Es el comportamiento HISTÓRICO de la
        // tabla y se conserva a propósito: cambiarlo alteraría el error_type de filas ya existentes.
        // Queda documentado acá para que el hueco sea visible y no se descubra de nuevo desde cero.
        Assert.Equal("unknown", EnvioCorreoCalculos.ClasificarErrorSmtp("The operation has timed out."));
    }

    [Theory]
    [InlineData("invalid recipient", "invalid_email")]
    [InlineData("bad address format", "invalid_email")]
    public void ClasificarErrorSmtp_reconoce_la_direccion_invalida(string detalle, string esperado)
    {
        Assert.Equal(esperado, EnvioCorreoCalculos.ClasificarErrorSmtp(detalle));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("algo que no matchea ninguna regla")]
    public void ClasificarErrorSmtp_sin_pistas_devuelve_unknown(string? detalle)
    {
        Assert.Equal("unknown", EnvioCorreoCalculos.ClasificarErrorSmtp(detalle));
    }

    [Fact]
    public void ClasificarErrorSmtp_la_autenticacion_gana_sobre_las_demas_reglas()
    {
        // El mensaje real de Office 365 menciona la conexión Y la autenticación; debe primar el auth.
        const string real =
            "The SMTP server requires a secure connection or the client was not authenticated. " +
            "The server response was: 5.7.57 Client not authenticated to send mail. " +
            "Error: 535 5.7.139 Authentication unsuccessful.";

        Assert.Equal("smtp_auth", EnvioCorreoCalculos.ClasificarErrorSmtp(real));
    }

    // ── Rechazo por política (lo que NO se arregla cambiando la contraseña) ──────────────────

    [Theory]
    [InlineData("Error: 535 5.7.139 Authentication unsuccessful")]
    [InlineData("The server response was: 5.7.57 Client not authenticated to send mail")]
    [InlineData("550 5.7.30 Basic authentication is not supported for Client Submission")]
    [InlineData("Client not authenticated")]
    public void EsRechazoPorPolitica_reconoce_los_codigos_administrativos(string mensaje)
    {
        Assert.True(EnvioCorreoCalculos.EsRechazoPorPolitica(mensaje));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("421 Service not available")]
    [InlineData("550 Mailbox unavailable")]
    public void EsRechazoPorPolitica_no_confunde_otros_fallos(string? mensaje)
    {
        Assert.False(EnvioCorreoCalculos.EsRechazoPorPolitica(mensaje));
    }
}
