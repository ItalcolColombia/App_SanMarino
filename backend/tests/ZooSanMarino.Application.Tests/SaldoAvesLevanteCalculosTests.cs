using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SaldoAvesLevanteCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del saldo de aves de levante. Los casos reproducen la fórmula de
/// <c>fn_reporte_semanal_levante_extras</c> y el cierre real del lote S-369 (informe MANGOS).
/// </summary>
public class SaldoAvesLevanteCalculosTests
{
    // ── Bajas netas ───────────────────────────────────────────────────────────
    [Fact]
    public void BajasNetas_suma_mortalidad_seleccion_y_error_de_sexaje()
    {
        Assert.Equal(10 + 3 + 7, BajasNetas(new MovimientoDia(10, 3, 7)));
    }

    [Fact]
    public void BajasNetas_incluye_el_error_de_sexaje_que_el_reporte_omitia()
    {
        // El bug: el reporte hacia `saldo -= mort + sel` y se olvidaba del error de sexaje.
        var conError = BajasNetas(new MovimientoDia(10, 3, 7));
        var comoAntes = 10 + 3;
        Assert.Equal(7, conError - comoAntes);
    }

    [Fact]
    public void BajasNetas_resta_salidas_y_suma_ingresos_de_traslado()
    {
        Assert.Equal(1 + 2 + 3 + 50 - 40, BajasNetas(new MovimientoDia(1, 2, 3, TrasladoSalida: 50, TrasladoIngreso: 40)));
    }

    [Fact]
    public void BajasNetas_es_negativa_si_el_lote_recibe_mas_de_lo_que_pierde()
    {
        Assert.Equal(-95, BajasNetas(new MovimientoDia(5, 0, 0, TrasladoIngreso: 100)));
    }

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 0, 1)]
    [InlineData(0, 1, 0, 1)]
    [InlineData(0, 0, 1, 1)]
    public void BajasNetas_cada_concepto_pesa_uno(int mort, int sel, int err, int esperado)
    {
        Assert.Equal(esperado, BajasNetas(new MovimientoDia(mort, sel, err)));
    }

    // ── Saldo paso a paso ─────────────────────────────────────────────────────
    [Fact]
    public void Siguiente_descuenta_del_saldo_previo()
    {
        Assert.Equal(980, Siguiente(1000, new MovimientoDia(10, 5, 5)));
    }

    [Fact]
    public void Siguiente_tiene_piso_en_cero()
    {
        Assert.Equal(0, Siguiente(10, new MovimientoDia(100, 0, 0)));
    }

    [Fact]
    public void Siguiente_permite_crecer_con_un_ingreso()
    {
        Assert.Equal(1090, Siguiente(1000, new MovimientoDia(10, 0, 0, TrasladoIngreso: 100)));
    }

    // ── Saldo final ───────────────────────────────────────────────────────────
    [Fact]
    public void SaldoFinal_sin_movimientos_es_el_encasetamiento()
    {
        Assert.Equal(20458, SaldoFinal(20458, Array.Empty<MovimientoDia>()));
    }

    [Fact]
    public void SaldoFinal_lote_S369_hembras_cierra_en_19018()
    {
        // Informe tecnico MANGOS, lote S-369AB: 20.458 encasetadas; en los 174 dias de levante
        // hubo 669 muertas, 157 descartes y 614 errores de sexaje. El maestro y el Excel cierran
        // los dos en 19.018; el reporte, que omitia el sexaje, daba 19.632.
        var movs = new[] { new MovimientoDia(669, 157, 614) };
        Assert.Equal(19018, SaldoFinal(20458, movs));
        Assert.Equal(19632, 20458 - (669 + 157));   // lo que devolvia el reporte con el bug
    }

    [Fact]
    public void SaldoFinal_lote_S369_machos_cierra_en_1957_contando_los_retiros()
    {
        // 2.993 encasetados − 195 muertos − 3 sexaje − 548 descartes − 290 retirados (traslado/venta).
        var movs = new[] { new MovimientoDia(195, 548, 3, TrasladoSalida: 290) };
        Assert.Equal(1957, SaldoFinal(2993, movs));
    }

    [Fact]
    public void SaldoFinal_es_igual_a_aplicar_Siguiente_dia_a_dia_cuando_no_hay_saturacion()
    {
        var movs = new[]
        {
            new MovimientoDia(10, 2, 0),
            new MovimientoDia(5, 0, 133),
            new MovimientoDia(0, 1, 0, TrasladoSalida: 20, TrasladoIngreso: 5),
        };
        var paso = 1000;
        foreach (var m in movs) paso = Siguiente(paso, m);
        Assert.Equal(paso, SaldoFinal(1000, movs));
    }

    [Fact]
    public void SaldoFinal_tiene_piso_en_cero()
    {
        Assert.Equal(0, SaldoFinal(100, new[] { new MovimientoDia(80, 30, 20) }));
    }

    [Fact]
    public void SaldoFinal_un_traslado_entre_dos_lotes_del_mismo_base_se_anula_al_sumarlos()
    {
        // A envia 196 machos a B: el consolidado no cambia.
        var a = SaldoFinal(1472, new[] { new MovimientoDia(71, 240, 0, TrasladoSalida: 196) });
        var b = SaldoFinal(1521, new[] { new MovimientoDia(128, 308, 3, TrasladoIngreso: 196, TrasladoSalida: 290) });
        Assert.Equal(965, a);
        Assert.Equal(988, b);
        Assert.Equal(1472 + 1521 - (71 + 240) - (128 + 308 + 3) - 290, a + b);
    }

    // ── Retiro acumulado ──────────────────────────────────────────────────────
    [Fact]
    public void RetiroAcumulado_no_cuenta_los_traslados()
    {
        var movs = new[] { new MovimientoDia(10, 5, 3, TrasladoSalida: 100, TrasladoIngreso: 50) };
        Assert.Equal(18, RetiroAcumulado(movs));
    }

    [Fact]
    public void RetiroAcumulado_del_lote_S369_es_1440_hembras()
    {
        Assert.Equal(1440, RetiroAcumulado(new[] { new MovimientoDia(669, 157, 614) }));
    }
}
