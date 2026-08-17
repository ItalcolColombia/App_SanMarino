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

/// <summary>
/// La VENTA de aves entra al saldo igual que una salida. En producción venía quedando fuera porque
/// la venta solo dejaba una nota de texto en la fila diaria: el reporte cerraba por encima del real
/// en exactamente el total vendido.
/// </summary>
public class SaldoAvesVentaCalculosTests
{
    [Fact]
    public void BajasNetas_cuenta_la_venta_como_salida()
    {
        Assert.Equal(100, BajasNetas(new MovimientoDia(0, 0, 0, Venta: 100)));
    }

    [Fact]
    public void BajasNetas_cuenta_el_retiro_como_salida()
    {
        Assert.Equal(7, BajasNetas(new MovimientoDia(0, 0, 0, Retiro: 7)));
    }

    [Fact]
    public void BajasNetas_suma_todos_los_conceptos()
    {
        var m = new MovimientoDia(10, 5, 3, TrasladoSalida: 20, TrasladoIngreso: 8, Venta: 50, Retiro: 2);
        Assert.Equal(10 + 5 + 3 + 20 + 50 + 2 - 8, BajasNetas(m));
    }

    [Fact]
    public void SaldoFinal_produccion_S369A_cierra_en_9020()
    {
        // Informe MANGOS, hoja DIARIO A: arranca produccion con 9.484 hembras; en 168 dias hubo
        // 312 muertas, 38 descartes y 114 vendidas. El informe cierra en 9.020.
        Assert.Equal(9020, SaldoFinal(9484, new[] { new MovimientoDia(312, 38, 0, Venta: 114) }));
        // Sin contar la venta —el bug— daba 9.134, que es lo que mostraba el reporte.
        Assert.Equal(9134, SaldoFinal(9484, new[] { new MovimientoDia(312, 38, 0) }));
    }

    [Fact]
    public void SaldoFinal_produccion_S369B_cierra_en_8952()
    {
        // DIARIO B: 9.534 iniciales, 309 muertas, 49 descartes, 224 vendidas.
        Assert.Equal(8952, SaldoFinal(9534, new[] { new MovimientoDia(309, 49, 0, Venta: 224) }));
        Assert.Equal(9176, SaldoFinal(9534, new[] { new MovimientoDia(309, 49, 0) }));
    }

    [Fact]
    public void SaldoFinal_produccion_machos_de_los_dos_sublotes()
    {
        Assert.Equal(810, SaldoFinal(966, new[] { new MovimientoDia(32, 61, 0, Venta: 63) }));
        Assert.Equal(813, SaldoFinal(991, new[] { new MovimientoDia(34, 77, 0, Venta: 67) }));
    }

    [Fact]
    public void RetiroAcumulado_no_cuenta_la_venta()
    {
        // El % de retiro acumulado de los reportes es mortalidad+seleccion+sexaje; la venta se
        // reporta en su propia columna y no debe inflarlo.
        Assert.Equal(350, RetiroAcumulado(new[] { new MovimientoDia(312, 38, 0, Venta: 114) }));
    }

    [Fact]
    public void Siguiente_una_venta_mayor_que_el_saldo_satura_en_cero()
    {
        Assert.Equal(0, Siguiente(50, new MovimientoDia(0, 0, 0, Venta: 200)));
    }

    // ─── Contrato que fn_indicadores_levante_postura tiene que cumplir (17ago26) ───────────────
    //
    // Esta clase es la especificacion ejecutable del saldo, pero durante meses convivio con una fn
    // que no la cumplia: `fn_indicadores_levante_postura` no descontaba la venta, asi que el mismo
    // lote y la misma semana mostraban dos conteos segun la pantalla (lote 143 semana 24: 10.619 en
    // Indicadores contra 10.329 en fn_reporte_semanal_levante_extras — la diferencia era exactamente
    // la venta acumulada). Migracion 20260817230000_FnIndicadoresLevanteDescuentaVenta.
    //
    // Lo que estos dos tests fijan es POR QUE la venta va en el saldo, para que el proximo que mire
    // la formula no la vuelva a sacar por parecer "un dato de reporte".

    [Fact]
    public void Vender_y_trasladar_afuera_sacan_las_mismas_aves_del_lote()
    {
        // Una ave vendida sale del lote igual que una trasladada: la unica diferencia es que la
        // vendida no llega a ningun otro lote. Para el saldo del lote de origen son lo mismo, y esa
        // es la equivalencia que la fn SQL implementa desde la migracion.
        var vendidas   = Siguiente(10_000, new MovimientoDia(0, 0, 0, Venta: 290));
        var trasladadas = Siguiente(10_000, new MovimientoDia(0, 0, 0, TrasladoSalida: 290));

        Assert.Equal(trasladadas, vendidas);
        Assert.Equal(9_710, vendidas);
    }

    [Fact]
    public void El_saldo_del_lote_143_reproduce_el_numero_del_reporte_semanal()
    {
        // Caso real que destapo la divergencia (Agroavicola Sanmarino, lote 143), con el kardex
        // acumulado del ciclo leido de seguimiento_diario_levante: 11.812 encasetadas, 432 de
        // mortalidad, 379 de seleccion, 382 de error de sexaje, sin traslados y 290 vendidas.
        const int avesIniciales = 11_812;
        var bajasSinVenta = new MovimientoDia(Mortalidad: 432, Seleccion: 379, ErrorSexaje: 382);
        var venta = new MovimientoDia(0, 0, 0, Venta: 290);

        // 10.329 es exactamente lo que muestran fn_reporte_semanal_levante_extras y, desde la
        // migracion, fn_indicadores_levante_postura.
        Assert.Equal(10_329, SaldoFinal(avesIniciales, new[] { bajasSinVenta, venta }));

        // Y 10.619 es lo que mostraba Indicadores antes: 290 aves de mas, la venta sin descontar.
        Assert.Equal(10_619, SaldoFinal(avesIniciales, new[] { bajasSinVenta }));
    }
}
