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
}
