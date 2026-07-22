using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Regla de cierre del lote reproductora engorde: cierra SOLO cuando los 7 días están CONFIRMADOS.
/// Antes cerraba por nº de registros o por aves agotadas; ese comportamiento cambió A PROPÓSITO
/// (la confirmación es la que sincroniza hacia pollo engorde).
/// </summary>
public class ReproductoraEngordeCalculosTests
{
    // ── Estado por confirmados ───────────────────────────────────────────────────
    [Theory]
    [InlineData(0, "Vigente")]
    [InlineData(1, "Vigente")]
    [InlineData(6, "Vigente")]   // 6 de 7 confirmados: aún no cierra
    [InlineData(7, "Cerrado")]   // los 7 confirmados: cierra
    public void Estado_DependeSoloDeConfirmados(int numConfirmados, string esperado)
    {
        var (estado, _) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 10_000, ventas: 0, mortalidad: 100, seleccion: 0, errorSexaje: 0,
            numConfirmados: numConfirmados);

        Assert.Equal(esperado, estado);
    }

    // ── Aves agotadas NO cierra si faltan confirmar (decisión: cierre 100% por confirmación) ─────
    [Fact]
    public void AvesAgotadas_ConMenosDe7Confirmados_SigueVigente()
    {
        // encaset 1000, bajas 1000 → avesActuales 0, pero solo 3 confirmados
        var (estado, avesActuales) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 1_000, ventas: 400, mortalidad: 500, seleccion: 100, errorSexaje: 0,
            numConfirmados: 3);

        Assert.Equal("Vigente", estado);
        Assert.Equal(0, avesActuales);
    }

    [Fact]
    public void AvesAgotadas_Con7Confirmados_Cierra()
    {
        var (estado, avesActuales) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 1_000, ventas: 400, mortalidad: 500, seleccion: 100, errorSexaje: 0,
            numConfirmados: 7);

        Assert.Equal("Cerrado", estado);
        Assert.Equal(0, avesActuales);
    }

    // ── AvesActuales: saldo físico, nunca negativo ───────────────────────────────
    [Fact]
    public void AvesActuales_EsSaldoFisico()
    {
        var (_, avesActuales) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 10_000, ventas: 200, mortalidad: 300, seleccion: 120, errorSexaje: 30,
            numConfirmados: 0);

        Assert.Equal(10_000 - 300 - 120 - 30 - 200, avesActuales); // 9_350
    }

    [Fact]
    public void AvesActuales_NuncaNegativo()
    {
        var (_, avesActuales) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 100, ventas: 50, mortalidad: 80, seleccion: 0, errorSexaje: 0,
            numConfirmados: 0);

        Assert.Equal(0, avesActuales);
    }

    // ── Umbral configurable ──────────────────────────────────────────────────────
    [Fact]
    public void Dias_ParametroConfigurable()
    {
        var (estado, _) = ReproductoraEngordeCalculos.CalcularEstado(
            avesEncasetadas: 10_000, ventas: 0, mortalidad: 0, seleccion: 0, errorSexaje: 0,
            numConfirmados: 3, dias: 3);

        Assert.Equal("Cerrado", estado);
    }

    // ── Edad en días: Vigente crece con el reloj; Cerrado se congela en la fecha de cierre ─────────
    [Fact]
    public void Edad_SinFechaEncasetamiento_EsCero()
    {
        var hoy = new DateTime(2026, 7, 22);
        Assert.Equal(0, ReproductoraEngordeCalculos.CalcularEdadDias(null, hoy, cerrado: false, fechaCierre: null));
    }

    [Fact]
    public void Edad_Vigente_UsaHoy_ComoAntes()
    {
        var encaset = new DateTime(2026, 7, 16);
        var hoy = new DateTime(2026, 7, 22);
        // Comportamiento previo: hoy − encasetamiento (6 días). La fechaCierre se ignora si no está cerrado.
        Assert.Equal(6, ReproductoraEngordeCalculos.CalcularEdadDias(encaset, hoy, cerrado: false, fechaCierre: new DateTime(2026, 7, 30)));
    }

    [Fact]
    public void Edad_Cerrado_SeCongelaEnFechaCierre_NoCreceConElReloj()
    {
        var encaset = new DateTime(2026, 7, 16);
        var hoy = new DateTime(2026, 7, 30);              // ya pasaron 14 días
        var fechaCierre = new DateTime(2026, 7, 22);       // último registro de recogida (día 6)
        // Congelado: 6 (fechaCierre − encaset), NO 14 (hoy − encaset).
        Assert.Equal(6, ReproductoraEngordeCalculos.CalcularEdadDias(encaset, hoy, cerrado: true, fechaCierre: fechaCierre));
    }

    [Fact]
    public void Edad_Cerrado_SinFechaCierre_CaeAHoy()
    {
        var encaset = new DateTime(2026, 7, 16);
        var hoy = new DateTime(2026, 7, 22);
        // Defensa: si por algún motivo no hay fecha de cierre, no rompe → usa hoy.
        Assert.Equal(6, ReproductoraEngordeCalculos.CalcularEdadDias(encaset, hoy, cerrado: true, fechaCierre: null));
    }

    [Fact]
    public void Edad_NuncaNegativa()
    {
        var encaset = new DateTime(2026, 7, 22);
        var hoy = new DateTime(2026, 7, 16);               // "hoy" anterior al encasetamiento (borde)
        Assert.Equal(0, ReproductoraEngordeCalculos.CalcularEdadDias(encaset, hoy, cerrado: false, fechaCierre: null));
    }

    // ── Edad de un registro de seguimiento y su validez para cruzar (edad ∈ [1, 7]) ────────────────
    [Theory]
    [InlineData("2026-07-13", 0)]   // mismo día del encaset
    [InlineData("2026-07-14", 1)]   // encaset + 1
    [InlineData("2026-07-20", 7)]   // encaset + 7
    [InlineData("2026-07-21", 8)]   // encaset + 8
    [InlineData("2026-07-12", -1)]  // anterior al encaset
    public void EdadSeguimientoDias_CuentaDiasCalendario(string fecha, int esperado)
    {
        var encaset = new DateTime(2026, 7, 13);
        var reg = DateTime.Parse(fecha);
        Assert.Equal(esperado, ReproductoraEngordeCalculos.EdadSeguimientoDias(encaset, reg));
    }

    [Fact]
    public void EdadSeguimientoDias_IgnoraLaHora()
    {
        // Fechas puras ancladas a mediodía UTC: la hora no debe mover el día de calendario.
        var encaset = new DateTime(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);
        var reg = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(1, ReproductoraEngordeCalculos.EdadSeguimientoDias(encaset, reg));
    }

    [Theory]
    [InlineData(0, false)]   // edad 0 (mismo día del encaset) → no cruza
    [InlineData(1, true)]    // primer día válido
    [InlineData(7, true)]    // último día válido
    [InlineData(8, false)]   // supera los 7 días
    [InlineData(-1, false)]  // anterior al encaset
    public void EsEdadSeguimientoValida_SoloEntre1y7(int edad, bool esperado)
    {
        Assert.Equal(esperado, ReproductoraEngordeCalculos.EsEdadSeguimientoValida(edad));
    }
}
