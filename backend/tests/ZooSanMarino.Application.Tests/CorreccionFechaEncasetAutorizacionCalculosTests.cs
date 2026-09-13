using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Gate de <c>lote.corregir_fecha_encaset</c>: se exige por el DELTA de la fecha y solo en un lote
/// que ya tiene registros. Cualquier otra combinación tiene que dejar pasar, o el permiso se
/// convierte en un segundo <c>editar_registro</c> y traba el alta de lotes.
/// </summary>
public class CorreccionFechaEncasetAutorizacionCalculosTests
{
    private static readonly string[] ConPermiso = { "otro.permiso", "lote.corregir_fecha_encaset" };
    private static readonly string[] SinPermiso = { "editar_registro", "lote.corregir_aves" };

    [Fact]
    public void SinCambioDeFecha_NoPideNada()
    {
        // El mismo PUT guarda el técnico, la regional y el ERP: pedir el permiso para todo el verbo
        // le rompería la pantalla a quien solo vino a corregir un nombre.
        Assert.True(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(false, true, SinPermiso));
        Assert.True(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(false, true, null));
    }

    [Fact]
    public void LoteSinRegistros_NoPideNada()
    {
        // Corregir la fecha de un lote recién creado no está corrigiendo ningún histórico.
        Assert.True(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(true, false, SinPermiso));
    }

    [Fact]
    public void CambioDeFechaEnLoteConRegistros_ExigeLaKey()
    {
        Assert.False(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(true, true, SinPermiso));
        Assert.True(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(true, true, ConPermiso));
    }

    [Fact]
    public void FailClosed_SinListaDePermisosNoPasa()
    {
        Assert.False(CorreccionFechaEncasetAutorizacionCalculos.PuedeAplicar(true, true, null));
        Assert.False(CorreccionFechaEncasetAutorizacionCalculos.TienePermiso(null));
    }

    [Fact]
    public void LaKeyEsExacta_NoUnaFamiliaDeCapitalizaciones()
    {
        Assert.False(CorreccionFechaEncasetAutorizacionCalculos.TienePermiso(new[] { "Lote.Corregir_Fecha_Encaset" }));
        Assert.True(CorreccionFechaEncasetAutorizacionCalculos.TienePermiso(new[] { "lote.corregir_fecha_encaset" }));
    }

    [Fact]
    public void NoSeReusaLaKeyDeAves_SonDosCorreccionesDistintas()
    {
        Assert.NotEqual(CorreccionAvesLoteAutorizacionCalculos.PermisoCorregirAves,
                        CorreccionFechaEncasetAutorizacionCalculos.PermisoCorregirFechaEncaset);
        Assert.False(CorreccionFechaEncasetAutorizacionCalculos.TienePermiso(
            new[] { CorreccionAvesLoteAutorizacionCalculos.PermisoCorregirAves }));
    }
}
