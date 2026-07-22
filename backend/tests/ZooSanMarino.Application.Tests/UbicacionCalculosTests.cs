using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas puras de gestión de ubicación (mover/eliminar núcleo·galpón·lote):
///  - eliminar bloqueado si hay hijos activos (evita huérfanos, incidente de prod),
///  - precondición de re-key (no mover a la misma granja),
///  - mensajes de bloqueo/impacto consistentes.
/// </summary>
public class UbicacionCalculosTests
{
    // ── Eliminar núcleo ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(0, 0, true)]   // sin galpones ni lotes → se puede
    [InlineData(1, 0, false)]  // con galpones → bloqueado
    [InlineData(0, 1, false)]  // con lotes → bloqueado
    [InlineData(2, 3, false)]  // con ambos → bloqueado
    public void PuedeEliminarNucleo_SoloSinHijosActivos(int galpones, int lotes, bool esperado)
        => Assert.Equal(esperado, UbicacionCalculos.PuedeEliminarNucleo(galpones, lotes));

    // ── Eliminar galpón ───────────────────────────────────────────────────────
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(5, false)]
    public void PuedeEliminarGalpon_SoloSinLotesActivos(int lotes, bool esperado)
        => Assert.Equal(esperado, UbicacionCalculos.PuedeEliminarGalpon(lotes));

    // ── Precondición re-key núcleo ────────────────────────────────────────────
    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    public void EsMismaGranja(int origen, int destino, bool esperado)
        => Assert.Equal(esperado, UbicacionCalculos.EsMismaGranja(origen, destino));

    // ── Mensajes ──────────────────────────────────────────────────────────────
    [Fact]
    public void MensajeBloqueoEliminarNucleo_IncluyeConteos()
    {
        var msg = UbicacionCalculos.MensajeBloqueoEliminarNucleo(2, 3);
        Assert.Contains("2 galpón", msg);
        Assert.Contains("3 lote", msg);
    }

    [Fact]
    public void MensajeBloqueoEliminarGalpon_IncluyeConteo()
    {
        var msg = UbicacionCalculos.MensajeBloqueoEliminarGalpon(4);
        Assert.Contains("4 lote", msg);
    }

    [Fact]
    public void Mensajes_NoMuestranNegativos()
    {
        // Defensivo: conteos negativos (no debería pasar) se muestran como 0.
        Assert.Contains("0 galpón", UbicacionCalculos.MensajeBloqueoEliminarNucleo(-1, -2));
        Assert.Contains("0 lote", UbicacionCalculos.MensajeBloqueoEliminarGalpon(-5));
        Assert.Contains("0 galpón", UbicacionCalculos.MensajeMovido("Núcleo", -1, -1));
    }

    [Fact]
    public void MensajeMovido_IncluyeEntidadYConteos()
    {
        var msg = UbicacionCalculos.MensajeMovido("Galpón", 1, 7);
        Assert.Contains("Galpón", msg);
        Assert.Contains("1 galpón", msg);
        Assert.Contains("7 lote", msg);
    }
}
