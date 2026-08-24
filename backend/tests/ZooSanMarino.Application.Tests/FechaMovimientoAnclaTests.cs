using ZooSanMarino.Application.Calculos;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// F2.0 — la ancla horaria que separa ingreso de consumo, para no empatar el orden intra-día que
/// <c>fn_seguimiento_diario_engorde</c> usa para el saldo corriente.
/// </summary>
public class FechaMovimientoAnclaTests
{
    [Fact]
    public void AnclaIngreso_ES_mediodia_el_comportamiento_de_siempre()
    {
        // ResolveMovimientoCreatedAt ancló SIEMPRE a las 12:00Z; esto lo fija para que nadie lo mueva
        // sin darse cuenta y rompa la paridad con el histórico.
        Assert.Equal(12, FechaMovimientoSeguimientoCalculos.AnclaIngresoUtc);
    }

    [Fact]
    public void AnclaConsumo_es_POSTERIOR_a_la_de_ingreso()
    {
        // El orden físico correcto: primero entra el alimento, después se come. Si algún día se
        // cambia una de las dos constantes, este test obliga a mantener el orden.
        Assert.True(
            FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc > FechaMovimientoSeguimientoCalculos.AnclaIngresoUtc,
            "El consumo tiene que anclar DESPUÉS del ingreso, o vuelve el empate de las 12:00 que F2 existe para evitar.");
    }

    [Fact]
    public void AnclaConsumo_es_18()
    {
        Assert.Equal(18, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc);
    }

    [Fact]
    public void Anclar_construye_la_hora_exacta_en_UTC()
    {
        var dia = new DateTime(2026, 8, 22);
        var r = FechaMovimientoSeguimientoCalculos.Anclar(dia, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc);

        Assert.Equal(new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero), r);
    }

    [Fact]
    public void Anclar_con_ancla_de_ingreso_da_mediodia()
    {
        var r = FechaMovimientoSeguimientoCalculos.Anclar(
            new DateTime(2026, 8, 22), FechaMovimientoSeguimientoCalculos.AnclaIngresoUtc);

        Assert.Equal(new DateTimeOffset(2026, 8, 22, 12, 0, 0, TimeSpan.Zero), r);
    }

    [Fact]
    public void Anclar_ignora_la_hora_del_DateTime_recibido()
    {
        // Sólo importa el día: si alguien pasa una hora ya puesta, se descarta — la ancla la decide
        // el llamador, no el valor de entrada.
        var conHora = new DateTime(2026, 8, 22, 23, 59, 59);
        var r = FechaMovimientoSeguimientoCalculos.Anclar(conHora, 18);

        Assert.Equal(new DateTimeOffset(2026, 8, 22, 18, 0, 0, TimeSpan.Zero), r);
    }

    [Theory]
    [InlineData(2026, 1, 1)]
    [InlineData(2026, 12, 31)]
    [InlineData(2028, 2, 29)] // año bisiesto
    public void Anclar_conserva_el_dia_exacto_para_fechas_borde(int y, int m, int d)
    {
        var r = FechaMovimientoSeguimientoCalculos.Anclar(new DateTime(y, m, d), 12);
        Assert.Equal((y, m, d), (r.Year, r.Month, r.Day));
    }

    [Fact]
    public void Ingreso_y_consumo_del_mismo_dia_NO_empatan()
    {
        // Es la razón de ser de F2.0: sin esto, fn_seguimiento_diario_engorde desempata por un orden
        // no documentado y el saldo corriente puede cerrar el día en rojo sin que falte un kilo real.
        var dia = new DateTime(2026, 8, 22);
        var ingreso = FechaMovimientoSeguimientoCalculos.Anclar(dia, FechaMovimientoSeguimientoCalculos.AnclaIngresoUtc);
        var consumo = FechaMovimientoSeguimientoCalculos.Anclar(dia, FechaMovimientoSeguimientoCalculos.AnclaConsumoUtc);

        Assert.NotEqual(ingreso, consumo);
        Assert.True(ingreso < consumo, "El ingreso tiene que quedar ANTES que el consumo del mismo día.");
    }
}
