using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fija la clasificación de los fallos de correo y, sobre todo, el ORDEN de evaluación: el defecto
/// que estos tests impiden es que un fallo de autenticación se lea como «falta STARTTLS», porque .NET
/// mapea a `MustIssueStartTlsFirst` el 530 posterior a un AUTH fallido.
///
/// Los mensajes largos son TEXTUALES de `email_queue` en producción, no inventados.
/// </summary>
public class EmailErrorCalculosTests
{
    // El mensaje exacto del id 164 (28-ago-2026), con su StatusCode engañoso incluido.
    private const string MensajeCuentaBloqueada =
        "The SMTP server requires a secure connection or the client was not authenticated. " +
        "The server response was: 5.7.57 Client not authenticated to send mail. " +
        "Error: 535 5.7.139 Authentication unsuccessful, account locked. Contact your administrator. " +
        "[CH2PR19CA0002.namprd19.prod.outlook.com 2026-08-28T19:45:50.437Z 08DF04C6D9A404E7]";

    // El de agosto, antes del bloqueo: mismo 535 pero con otro motivo.
    private const string MensajeRechazoPorPolitica =
        "535 5.7.139 Authentication unsuccessful, the request did not meet the criteria to be " +
        "authenticated successfully. Contact your administrator.";

    [Fact]
    public void CuentaBloqueada_GanaAunqueElStatusCodeDigaStartTls()
    {
        // ESTE es el test que importa: con el StatusCode que reporta .NET, el emisor lo clasificaba
        // como «falta STARTTLS» y guardaba «verificar que EnableSsl=true» — mientras EnableSsl ya
        // era true y la cuenta estaba bloqueada.
        var clase = EmailErrorCalculos.Clasificar(MensajeCuentaBloqueada, "MustIssueStartTlsFirst");

        Assert.Equal(ClaseErrorCorreo.CuentaBloqueada, clase);
        Assert.NotEqual(ClaseErrorCorreo.RequiereStartTls, clase);
    }

    [Fact]
    public void RechazoPorPolitica_EsAutenticacionRechazada_NoCuentaBloqueada()
    {
        var clase = EmailErrorCalculos.Clasificar(MensajeRechazoPorPolitica, "MustIssueStartTlsFirst");

        Assert.Equal(ClaseErrorCorreo.AutenticacionRechazada, clase);
    }

    [Fact]
    public void StartTlsDeVerdad_SigueClasificandoComoTal()
    {
        // Sin ningún código de autenticación en el mensaje, el StatusCode ya no es un espejismo.
        var clase = EmailErrorCalculos.Clasificar(
            "The server response was: 530 5.7.0 Must issue a STARTTLS command first.", "MustIssueStartTlsFirst");

        Assert.Equal(ClaseErrorCorreo.RequiereStartTls, clase);
    }

    [Fact]
    public void AuthBasicaRetirada_NoSeConfundeConElRechazoGenerico()
    {
        var clase = EmailErrorCalculos.Clasificar(
            "535 5.7.30 Basic authentication is not supported for Client Submission.");

        Assert.Equal(ClaseErrorCorreo.AuthBasicaRetirada, clase);
    }

    [Theory]
    [InlineData("550 5.1.1 The email account that you tried to reach does not exist.")]
    [InlineData("RecipientNotFound; not found")]
    public void BuzonInvalido(string mensaje)
    {
        Assert.Equal(ClaseErrorCorreo.BuzonInvalido, EmailErrorCalculos.Clasificar(mensaje));
    }

    [Theory]
    [InlineData("The operation has timed out.")]
    [InlineData("451 4.7.0 Temporary server error. Please try again later.")]
    public void Transitorio(string mensaje)
    {
        Assert.Equal(ClaseErrorCorreo.Transitorio, EmailErrorCalculos.Clasificar(mensaje));
    }

    [Fact]
    public void SinMensaje_EsDesconocidoYSeReintenta()
    {
        var clase = EmailErrorCalculos.Clasificar(null);

        Assert.Equal(ClaseErrorCorreo.Desconocido, clase);
        Assert.True(EmailErrorCalculos.ValeLaPenaReintentar(clase));
    }

    // ─── Reintentos ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ClaseErrorCorreo.CuentaBloqueada)]
    [InlineData(ClaseErrorCorreo.AutenticacionRechazada)]
    [InlineData(ClaseErrorCorreo.AuthBasicaRetirada)]
    [InlineData(ClaseErrorCorreo.BuzonInvalido)]
    public void LosPermanentes_NoSeReintentan(ClaseErrorCorreo clase)
    {
        // No solo no pueden funcionar: cada reintento es otra autenticación fallida contra el tenant,
        // que es lo que sostiene el bloqueo. Entre el 26 y el 28-ago fueron 30.
        Assert.False(EmailErrorCalculos.ValeLaPenaReintentar(clase));
    }

    [Theory]
    [InlineData(ClaseErrorCorreo.Transitorio)]
    [InlineData(ClaseErrorCorreo.RequiereStartTls)]
    [InlineData(ClaseErrorCorreo.Desconocido)]
    public void LosDemas_SiSeReintentan(ClaseErrorCorreo clase)
    {
        Assert.True(EmailErrorCalculos.ValeLaPenaReintentar(clase));
    }

    [Fact]
    public void LaEspera_Crece_YSeMantieneAcotada()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), EmailErrorCalculos.EsperaAntesDelProximoIntento(1));
        Assert.Equal(TimeSpan.FromMinutes(5), EmailErrorCalculos.EsperaAntesDelProximoIntento(2));
        Assert.Equal(TimeSpan.FromMinutes(15), EmailErrorCalculos.EsperaAntesDelProximoIntento(3));
        // Acotada: ni un intento tardío queda esperando horas.
        Assert.Equal(TimeSpan.FromMinutes(15), EmailErrorCalculos.EsperaAntesDelProximoIntento(99));
    }

    // ─── Lo que se guarda ────────────────────────────────────────────────────────────────────

    [Fact]
    public void ElTipoDeLaCola_DiceLaCAUSA_NoQueSeAcabaronLosIntentos()
    {
        Assert.Equal("cuenta_bloqueada", EmailErrorCalculos.TipoParaLaCola(ClaseErrorCorreo.CuentaBloqueada));
        Assert.Equal("buzon_invalido", EmailErrorCalculos.TipoParaLaCola(ClaseErrorCorreo.BuzonInvalido));
    }

    [Fact]
    public void ElDiagnosticoDeCuentaBloqueada_NoMandaATocarLaConfiguracion()
    {
        var d = EmailErrorCalculos.Diagnostico(ClaseErrorCorreo.CuentaBloqueada, "zootecnico@sanmarino.com.co");

        Assert.Contains("BLOQUEADA", d);
        Assert.Contains("zootecnico@sanmarino.com.co", d);
        Assert.Contains("desbloquear", d, StringComparison.OrdinalIgnoreCase);
        // Lo que NO puede decir: el texto viejo mandaba a revisar EnableSsl, que ya estaba bien.
        Assert.DoesNotContain("EnableSsl", d);
        Assert.DoesNotContain("STARTTLS", d);
    }

    [Fact]
    public void ElDiagnosticoDeRechazo_AvisaQueLaIpNoSirveComoSolucion()
    {
        var d = EmailErrorCalculos.Diagnostico(ClaseErrorCorreo.AutenticacionRechazada);

        Assert.Contains("ORIGEN", d);
        Assert.Contains("efímera", d);
    }

    [Fact]
    public void ElDiagnosticoSinBuzon_NoRompeNiDejaHuecos()
    {
        var d = EmailErrorCalculos.Diagnostico(ClaseErrorCorreo.CuentaBloqueada, null);

        Assert.Contains("el buzón emisor", d);
    }
}
