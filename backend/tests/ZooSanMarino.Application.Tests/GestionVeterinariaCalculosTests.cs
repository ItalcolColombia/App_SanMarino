using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

public class GestionVeterinariaCalculosTests
{
    private static readonly DateTime Hoy = new(2026, 9, 25, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidarPeriodo_AceptaMismoDia()
        => GestionVeterinariaCalculos.ValidarPeriodo(Hoy, Hoy);

    [Fact]
    public void ValidarPeriodo_RechazaRangoInvertido()
        => Assert.Throws<InvalidOperationException>(() =>
            GestionVeterinariaCalculos.ValidarPeriodo(Hoy, Hoy.AddDays(-1)));

    [Theory]
    [InlineData(-1, 1, "PROXIMA")]
    [InlineData(0, 1, "ACTIVA")]
    [InlineData(1, -1, "VENCIDA")]
    public void EstadoTemporal_ClasificaPendiente(int inicioOffset, int finOffset, string esperado)
        => Assert.Equal(esperado, GestionVeterinariaCalculos.EstadoTemporal(
            Hoy.AddDays(-inicioOffset), Hoy.AddDays(finOffset), EstadoTareaCampo.Pendiente, Hoy));

    [Theory]
    [InlineData(EstadoTareaCampo.Realizada)]
    [InlineData(EstadoTareaCampo.Cancelada)]
    public void EstadoTemporal_TareaNoPendiente_EstaCerrada(string estado)
        => Assert.Equal("CERRADA", GestionVeterinariaCalculos.EstadoTemporal(
            Hoy.AddDays(-10), Hoy.AddDays(-5), estado, Hoy));

    [Fact]
    public void PuedeAcceder_SinGranja_FailClosed()
    {
        var acceso = Acceso(false, true);
        Assert.False(GestionVeterinariaCalculos.PuedeAcceder(acceso, null, null, null));
    }

    [Fact]
    public void PuedeAcceder_ScopeGlobal_PermiteCualquierNivel()
    {
        var acceso = Acceso(true, true);
        Assert.True(GestionVeterinariaCalculos.PuedeAcceder(acceso, "N9", "G9", 99));
    }

    [Fact]
    public void PuedeAcceder_ScopeRestringido_RespetaNivelMasEspecifico()
    {
        var acceso = Acceso(true, false, new[] { "N1" }, new[] { "G1" }, new[] { 7 });
        Assert.True(GestionVeterinariaCalculos.PuedeAcceder(acceso, "N1", "G1", 7));
        Assert.False(GestionVeterinariaCalculos.PuedeAcceder(acceso, "N1", "G1", 8));
        Assert.True(GestionVeterinariaCalculos.PuedeAcceder(acceso, "N1", "G1", null));
        Assert.True(GestionVeterinariaCalculos.PuedeAcceder(acceso, "N1", null, null));
    }

    [Fact]
    public void PuedeAcceder_TareaGeneralGranja_LlegaAUsuarioRestringidoAsignado()
        => Assert.True(GestionVeterinariaCalculos.PuedeAcceder(
            Acceso(true, false), null, null, null));

    [Theory]
    [InlineData(true, false, null, 0, "La observación")]
    [InlineData(false, true, "ok", 0, "fotografía")]
    [InlineData(false, false, "ok", 4, "máximo")]
    public void ValidarCumplimiento_RechazaFaltantes(
        bool reqObs, bool reqFoto, string? obs, int fotos, string fragmento)
    {
        var error = GestionVeterinariaCalculos.ValidarCumplimiento(reqObs, reqFoto, obs, fotos);
        Assert.NotNull(error);
        Assert.Contains(fragmento, error!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidarCumplimiento_AceptaRequisitosCompletos()
        => Assert.Null(GestionVeterinariaCalculos.ValidarCumplimiento(true, true, "Aplicada sin novedad", 1));

    [Theory]
    [InlineData("image/gif", "data:image/gif;base64,AAAA", 10)]
    [InlineData("image/jpeg", "data:image/png;base64,AAAA", 10)]
    [InlineData("image/jpeg", "data:image/jpeg;base64,AAAA", 2_500_001)]
    public void ValidarImagen_RechazaTipoContenidoOTamano(string tipo, string contenido, int bytes)
        => Assert.NotNull(GestionVeterinariaCalculos.ValidarImagen(contenido, tipo, bytes));

    [Fact]
    public void ValidarImagen_AceptaJpegComprimido()
        => Assert.Null(GestionVeterinariaCalculos.ValidarImagen(
            "data:image/jpeg;base64,AAAA", "image/jpeg", 3));

    [Theory]
    [InlineData("data:image/jpeg;base64,AAAA", 2, "tamaño declarado")]
    [InlineData("data:image/jpeg;base64,***", 2, "Base64 válido")]
    public void ValidarImagen_RechazaContenidoManipulado(string contenido, int bytes, string fragmento)
    {
        var error = GestionVeterinariaCalculos.ValidarImagen(contenido, "image/jpeg", bytes);
        Assert.NotNull(error);
        Assert.Contains(fragmento, error!, StringComparison.OrdinalIgnoreCase);
    }

    private static GestionVeterinariaCalculos.UbicacionAcceso Acceso(
        bool granja, bool global,
        IEnumerable<string>? nucleos = null,
        IEnumerable<string>? galpones = null,
        IEnumerable<int>? lotes = null)
        => new(
            granja,
            global,
            (nucleos ?? Array.Empty<string>()).ToHashSet(),
            (galpones ?? Array.Empty<string>()).ToHashSet(),
            (lotes ?? Array.Empty<int>()).ToHashSet());
}
