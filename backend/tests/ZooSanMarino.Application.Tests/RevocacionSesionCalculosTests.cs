using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// B1 — revocación de sesión. Estos tests son el contrato del hook que corre en el camino de
/// <b>TODO</b> request autenticado (<c>JwtBearerEvents.OnTokenValidated</c>): un bug ahí es un outage
/// total, así que la decisión vive acá —pura, sin EF— y el hook queda de ~15 líneas.
///
/// <para>
/// Los dos invariantes que no se pueden romper: <b>fail-closed</b> (un <c>jti</c> sin fila no pasa) y
/// <b>ventana de gracia</b> (un token sin <c>jti</c> sí pasa, o el despliegue desloguea a todo el
/// mundo de golpe, tablets con capturas sin subir incluidas).
/// </para>
/// </summary>
public class RevocacionSesionCalculosTests
{
    private static readonly DateTime Ahora = new(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc);
    private const string Jti = "9f1c4b6e-0000-4a2b-8c11-abcdef123456";

    // ───────────────────────── Evaluar ─────────────────────────

    [Fact]
    public void T1_TokenSinJti_EsLegado_yPasa()
    {
        // Ventana de gracia: al desplegar B1 todos los tokens vivos son de antes.
        var estado = RevocacionSesionCalculos.Evaluar(
            jti: null, hayFila: false, revokedAt: null, expiresAt: null, ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Legado, estado);
        Assert.True(RevocacionSesionCalculos.EsSesionValida(estado));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void T1b_JtiVacioOEnBlanco_TambienEsLegado(string jti)
    {
        // Un claim presente pero vacío es un token sin jti: no se puede buscar fila con eso.
        var estado = RevocacionSesionCalculos.Evaluar(jti, false, null, null, Ahora);
        Assert.Equal(EstadoSesion.Legado, estado);
    }

    [Fact]
    public void T2_JtiSinFila_NoPasa_failClosed()
    {
        // El corazón del fail-closed: sin fila NO hay sesión. Una lista negra haría lo contrario.
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: false, revokedAt: null, expiresAt: Ahora.AddHours(8), ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.NoRegistrada, estado);
        Assert.False(RevocacionSesionCalculos.EsSesionValida(estado));
    }

    [Fact]
    public void T3_FilaVivaYVigente_EsValida()
    {
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: true, revokedAt: null, expiresAt: Ahora.AddHours(8), ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Valida, estado);
        Assert.True(RevocacionSesionCalculos.EsSesionValida(estado));
    }

    [Fact]
    public void T4_FilaRevocada_NoPasa()
    {
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: true, revokedAt: Ahora.AddMinutes(-1), expiresAt: Ahora.AddHours(8), ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Revocada, estado);
        Assert.False(RevocacionSesionCalculos.EsSesionValida(estado));
    }

    [Fact]
    public void T5_FilaVencida_NoPasa()
    {
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: true, revokedAt: null, expiresAt: Ahora.AddMinutes(-1), ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Vencida, estado);
        Assert.False(RevocacionSesionCalculos.EsSesionValida(estado));
    }

    [Fact]
    public void T6_Borde_ExpiraExactamenteAhora_YaEstaVencida()
    {
        // `<=`, coherente con ClockSkew = Zero en TokenValidationParameters.
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: true, revokedAt: null, expiresAt: Ahora, ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Vencida, estado);
    }

    [Fact]
    public void T7_RevocadaYVencida_GanaRevocada()
    {
        // Precedencia estable: el mensaje al usuario debe ser el útil ("la apagaron"), no el genérico.
        var estado = RevocacionSesionCalculos.Evaluar(
            Jti, hayFila: true, revokedAt: Ahora.AddHours(-2), expiresAt: Ahora.AddHours(-1), ahoraUtc: Ahora);

        Assert.Equal(EstadoSesion.Revocada, estado);
    }

    // ───────────────────────── MotivoParaCliente ─────────────────────────

    [Fact]
    public void T8_MotivoParaCliente_PorEstado()
    {
        Assert.Equal("sesion-revocada", RevocacionSesionCalculos.MotivoParaCliente(EstadoSesion.Revocada));
        Assert.Equal("sesion-revocada", RevocacionSesionCalculos.MotivoParaCliente(EstadoSesion.NoRegistrada));
        Assert.Equal("token-expirado", RevocacionSesionCalculos.MotivoParaCliente(EstadoSesion.Vencida));

        // Los que pasan no tienen motivo de fallo.
        Assert.Null(RevocacionSesionCalculos.MotivoParaCliente(EstadoSesion.Valida));
        Assert.Null(RevocacionSesionCalculos.MotivoParaCliente(EstadoSesion.Legado));
    }

    [Fact]
    public void T8b_NingunEstadoInvalido_DevuelveMotivoNulo()
    {
        // El front decide si cierra la sesión leyendo este valor: un null acá sería una sesión
        // que muere en el servidor y sigue viva en la tablet.
        foreach (var estado in new[] { EstadoSesion.NoRegistrada, EstadoSesion.Revocada, EstadoSesion.Vencida })
            Assert.False(string.IsNullOrWhiteSpace(RevocacionSesionCalculos.MotivoParaCliente(estado)));
    }

    // ───────────────────────── DebeActualizarUltimaVista ─────────────────────────

    [Fact]
    public void T9_SinMarcaPrevia_SiempreMarca()
    {
        Assert.True(RevocacionSesionCalculos.DebeActualizarUltimaVista(null, Ahora));
    }

    [Fact]
    public void T10_DentroDelUmbral_NoEscribe()
    {
        // El heartbeat llega cada 90 s: sin throttle sería un UPDATE por minuto y medio por tablet.
        var hace1Min = Ahora.AddMinutes(-1);
        Assert.False(RevocacionSesionCalculos.DebeActualizarUltimaVista(
            hace1Min, Ahora, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void T11_PasadoElUmbral_Escribe()
    {
        var hace6Min = Ahora.AddMinutes(-6);
        Assert.True(RevocacionSesionCalculos.DebeActualizarUltimaVista(
            hace6Min, Ahora, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void T11b_UmbralPorDefecto_EsDe5Minutos()
    {
        Assert.False(RevocacionSesionCalculos.DebeActualizarUltimaVista(Ahora.AddMinutes(-4), Ahora));
        Assert.True(RevocacionSesionCalculos.DebeActualizarUltimaVista(Ahora.AddMinutes(-5), Ahora));
    }

    // ───────────────────────── PuedeRevocarSesionDeOtro ─────────────────────────

    [Theory]
    [InlineData(true, new[] { "otro.permiso" }, true)]      // super admin: pasa igual
    [InlineData(false, new[] { "usuarios.revocar_sesion" }, true)]
    [InlineData(false, new[] { "USUARIOS.REVOCAR_SESION" }, true)] // el permiso no distingue caja
    [InlineData(false, new[] { "usuarios.editar" }, false)]
    [InlineData(false, new string[0], false)]
    public void T12_PuedeRevocarSesionDeOtro(bool esSuperAdmin, string[] permisos, bool esperado)
    {
        Assert.Equal(esperado, RevocacionSesionCalculos.PuedeRevocarSesionDeOtro(esSuperAdmin, permisos));
    }

    [Fact]
    public void T12b_SinPermisos_NoPuede_failClosed()
    {
        Assert.False(RevocacionSesionCalculos.PuedeRevocarSesionDeOtro(false, null));
    }

    // ───────────────────────── PuedeRevocarSesionPropia ─────────────────────────

    [Fact]
    public void T13_SoloSobreSusPropiasSesiones()
    {
        var yo = Guid.NewGuid();
        var otro = Guid.NewGuid();

        Assert.True(RevocacionSesionCalculos.PuedeRevocarSesionPropia(yo, yo));
        Assert.False(RevocacionSesionCalculos.PuedeRevocarSesionPropia(yo, otro));
        Assert.False(RevocacionSesionCalculos.PuedeRevocarSesionPropia(null, otro));
        // Guid.Empty es "no se pudo identificar al usuario", no un usuario.
        Assert.False(RevocacionSesionCalculos.PuedeRevocarSesionPropia(Guid.Empty, Guid.Empty));
    }

    // ───────────────────────── TtlCache ─────────────────────────

    [Fact]
    public void T14_TtlCache_ValidaSonSesentaSegundos()
    {
        // Es la cota de cuánto tarda una revocación en surtir efecto: lo que se promete en la UI.
        var ttl = RevocacionSesionCalculos.TtlCache(EstadoSesion.Valida, Ahora.AddHours(16), Ahora);
        Assert.Equal(TimeSpan.FromSeconds(60), ttl);
    }

    [Fact]
    public void T14b_TtlCache_MuertasHastaElExp()
    {
        // Una sesión revocada no resucita: volver a preguntar es gasto puro.
        var ttl = RevocacionSesionCalculos.TtlCache(EstadoSesion.Revocada, Ahora.AddHours(3), Ahora);
        Assert.Equal(TimeSpan.FromHours(3), ttl);
    }

    [Fact]
    public void T14c_TtlCache_NuncaNegativo()
    {
        var ttl = RevocacionSesionCalculos.TtlCache(EstadoSesion.Vencida, Ahora.AddHours(-1), Ahora);
        Assert.Equal(TimeSpan.Zero, ttl);
        Assert.True(ttl >= TimeSpan.Zero);
    }

    // ───────────── Equivalencia con el comportamiento previo (exigencia del repo) ─────────────

    [Fact]
    public void T15_ConTokenLegado_NadaCambiaRespectoDeHoy()
    {
        // Antes de B1 el backend aceptaba todo token bien firmado y no vencido. Con `jti = null`
        // sigue haciendo exactamente eso, aunque la tabla esté vacía y aunque la fila no exista:
        // es la prueba de que la ventana de gracia hace lo que dice.
        foreach (var hayFila in new[] { true, false })
        {
            var estado = RevocacionSesionCalculos.Evaluar(
                jti: null, hayFila, revokedAt: Ahora.AddDays(-1), expiresAt: Ahora.AddDays(-1), ahoraUtc: Ahora);

            Assert.Equal(EstadoSesion.Legado, estado);
            Assert.True(RevocacionSesionCalculos.EsSesionValida(estado));
            Assert.Null(RevocacionSesionCalculos.MotivoParaCliente(estado));
        }
    }
}
