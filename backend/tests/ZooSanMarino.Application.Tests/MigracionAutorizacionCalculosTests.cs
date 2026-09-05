using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Migracion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Gate por permiso del módulo de Migraciones Masivas. Lo que estos tests blindan:
/// (1) es FAIL-CLOSED — sin permisos no pasa nadie, que era justo lo que faltaba: el módulo estaba
///     gateado solo en la UI y bastaba escribir la URL a mano;
/// (2) las dos líneas no se cruzan: el permiso de postura no habilita engorde ni al revés;
/// (3) la clasificación de tipos es la MISMA que usa el front para decidir qué tiles ofrece — si se
///     desincronizaran, el front ofrecería un tile que el backend rechaza con 403.
/// </summary>
public class MigracionAutorizacionCalculosTests
{
    private static readonly string[] SoloPostura = { MigracionAutorizacionCalculos.PermisoPostura };
    private static readonly string[] SoloEngorde = { MigracionAutorizacionCalculos.PermisoPolloEngorde };
    private static readonly string[] Ambos =
        { MigracionAutorizacionCalculos.PermisoPostura, MigracionAutorizacionCalculos.PermisoPolloEngorde };

    // ── Fail-closed ──────────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TipoMigracion.SeguimientoLevante)]
    [InlineData(TipoMigracion.SeguimientoProduccion)]
    [InlineData(TipoMigracion.SeguimientoPolloEngorde)]
    [InlineData(TipoMigracion.Granjas)]
    public void SinPermisos_NoPasaNadie(TipoMigracion tipo)
    {
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(Array.Empty<string>(), tipo));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(null, tipo));
    }

    [Fact]
    public void SinPermisos_TampocoPasaSinTipo()
    {
        // El caso del historial y las consultas auxiliares: no hay línea que distinguir, pero
        // tampoco corresponde abrirlo a cualquier sesión autenticada.
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(Array.Empty<string>(), tipo: null));
    }

    [Fact]
    public void UnPermisoAjenoNoHabilitaNada()
    {
        var otros = new[] { "usuarios.gestionar", "tickets.admin" };
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(otros, TipoMigracion.SeguimientoLevante));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(otros, tipo: null));
    }

    // ── Las dos líneas no se cruzan ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TipoMigracion.SeguimientoLevante)]
    [InlineData(TipoMigracion.SeguimientoProduccion)]
    public void Postura_LaHabilitaSuPermisoYNoElDeEngorde(TipoMigracion tipo)
    {
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloPostura, tipo));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(SoloEngorde, tipo));
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(Ambos, tipo));
    }

    [Theory]
    [InlineData(TipoMigracion.LotesPolloEngorde)]
    [InlineData(TipoMigracion.SeguimientoPolloEngorde)]
    [InlineData(TipoMigracion.SeguimientoReproductoraEngorde)]
    [InlineData(TipoMigracion.VentaPolloEngorde)]
    public void Engorde_LaHabilitaSuPermisoYNoElDePostura(TipoMigracion tipo)
    {
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloEngorde, tipo));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(SoloPostura, tipo));
    }

    // ── Estructura y "sin tipo": alcanza con cualquiera ──────────────────────────────────────────

    [Theory]
    [InlineData(TipoMigracion.Granjas)]
    [InlineData(TipoMigracion.Nucleos)]
    [InlineData(TipoMigracion.Galpones)]
    public void Estructura_AlcanzaConCualquieraDeLosDos(TipoMigracion tipo)
    {
        Assert.Null(MigracionAutorizacionCalculos.PermisoRequerido(tipo));
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloPostura, tipo));
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloEngorde, tipo));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(Array.Empty<string>(), tipo));
    }

    [Fact]
    public void SinTipo_AlcanzaConCualquieraDeLosDos()
    {
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloPostura, tipo: null));
        Assert.True(MigracionAutorizacionCalculos.PuedeUsar(SoloEngorde, tipo: null));
    }

    [Fact]
    public void LaComparacionEsOrdinal()
    {
        // Una key de permiso es un identificador, no texto de usuario: distinta caja NO habilita.
        // Mismo contrato que fija GestionUsuariosAutorizacionCalculosTests para su propia key.
        var otraCaja = new[] { "CARGA_MASIVA_POSTURA", "Carga_Masiva_Postura" };
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(otraCaja, TipoMigracion.SeguimientoLevante));
        Assert.False(MigracionAutorizacionCalculos.PuedeUsar(otraCaja, tipo: null));
    }

    // ── La clasificación, que tiene que espejar al front ─────────────────────────────────────────

    [Fact]
    public void LaLineaEngordeEsExactamenteLaDelFront()
    {
        // Espejo de TIPOS_POLLO_ENGORDE en agrupar-tipo-migracion.funcion.ts. Si el front agrega un
        // tipo a esa lista y acá no, el tile se ofrece y el backend lo rechaza con 403.
        var esperados = new[]
        {
            TipoMigracion.LotesPolloEngorde,
            TipoMigracion.SeguimientoPolloEngorde,
            TipoMigracion.SeguimientoReproductoraEngorde,
            TipoMigracion.VentaPolloEngorde,
        };
        var reales = Enum.GetValues<TipoMigracion>()
            .Where(MigracionAutorizacionCalculos.EsLineaEngorde)
            .ToArray();

        Assert.Equal(esperados, reales);
    }

    [Fact]
    public void TodoTipoDelCatalogoTieneUnaLineaResuelta()
    {
        // Ningún tipo puede quedar sin clasificar: uno que no sea engorde ni estructura cae en
        // postura, y eso tiene que ser una decisión, no un descuido.
        foreach (var tipo in Enum.GetValues<TipoMigracion>())
        {
            var permiso = MigracionAutorizacionCalculos.PermisoRequerido(tipo);
            if (MigracionAutorizacionCalculos.EsEstructura(tipo))
                Assert.Null(permiso);
            else
                Assert.Equal(
                    MigracionAutorizacionCalculos.EsLineaEngorde(tipo)
                        ? MigracionAutorizacionCalculos.PermisoPolloEngorde
                        : MigracionAutorizacionCalculos.PermisoPostura,
                    permiso);
        }
    }

    [Fact]
    public void ElMensajeNombraElPermisoQueFalta()
    {
        // El admin tiene que poder leer el 403 y saber qué asignar sin abrir el código.
        Assert.Contains(MigracionAutorizacionCalculos.PermisoPostura,
            MigracionAutorizacionCalculos.MensajeSinPermiso(TipoMigracion.SeguimientoLevante));
        Assert.Contains(MigracionAutorizacionCalculos.PermisoPolloEngorde,
            MigracionAutorizacionCalculos.MensajeSinPermiso(TipoMigracion.VentaPolloEngorde));

        var sinTipo = MigracionAutorizacionCalculos.MensajeSinPermiso(null);
        Assert.Contains(MigracionAutorizacionCalculos.PermisoPostura, sinTipo);
        Assert.Contains(MigracionAutorizacionCalculos.PermisoPolloEngorde, sinTipo);
    }
}
