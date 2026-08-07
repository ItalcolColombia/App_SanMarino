using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de la hoja RESUMEN del informe contable. El defecto que originó estos tests: el resumen
/// consolidaba mortalidad, traslados y ventas pero NO la selección, aunque la hoja semanal sí la
/// escribía — con lotes donde la selección pesa miles de aves, el resumen y el detalle no cuadraban.
/// </summary>
public class ReporteContableResumenCalculosTests
{
    private static ReporteContableSemanalDto Semana(
        int semana,
        int mortH = 0, int mortM = 0,
        int selH = 0, int selM = 0,
        int trasH = 0, int trasM = 0,
        int ventaH = 0, int ventaM = 0,
        decimal alimento = 0, decimal agua = 0, decimal medicamento = 0,
        decimal vacuna = 0, decimal otros = 0, decimal totalGeneral = 0) => new()
    {
        SemanaContable = semana,
        FechaInicio = new DateTime(2025, 1, 1).AddDays((semana - 1) * 7),
        FechaFin = new DateTime(2025, 1, 7).AddDays((semana - 1) * 7),
        MortalidadHembrasSemanal = mortH,
        MortalidadMachosSemanal = mortM,
        SeleccionHembrasSemanal = selH,
        SeleccionMachosSemanal = selM,
        TrasladosHembrasSemanal = trasH,
        TrasladosMachosSemanal = trasM,
        VentasHembrasSemanal = ventaH,
        VentasMachosSemanal = ventaM,
        ConsumoTotalAlimento = alimento,
        ConsumoTotalAgua = agua,
        ConsumoTotalMedicamento = medicamento,
        ConsumoTotalVacuna = vacuna,
        OtrosConsumos = otros,
        TotalGeneral = totalGeneral
    };

    [Fact]
    public void AFila_suma_hembras_y_machos_en_cada_conteo_de_aves()
    {
        var fila = ReporteContableResumenCalculos.AFila(
            Semana(3, mortH: 12, mortM: 5, selH: 40, selM: 7, trasH: 2, trasM: 1, ventaH: 9, ventaM: 4));

        Assert.Equal(3, fila.Semana);
        Assert.Equal(17, fila.Mortalidad);
        Assert.Equal(47, fila.Seleccion);
        Assert.Equal(3, fila.Traslados);
        Assert.Equal(13, fila.Ventas);
    }

    [Fact]
    public void AFila_toma_la_seleccion_que_hoy_faltaba_en_el_resumen()
    {
        // Caso K345: la selección de la semana no es residual, es del orden de la mortalidad.
        var fila = ReporteContableResumenCalculos.AFila(Semana(1, mortH: 6, mortM: 1, selH: 2_390, selM: 272));

        Assert.Equal(7, fila.Mortalidad);
        Assert.Equal(2_662, fila.Seleccion);
    }

    [Fact]
    public void Filas_ordena_por_semana_contable()
    {
        var filas = ReporteContableResumenCalculos.Filas(new[] { Semana(3), Semana(1), Semana(2) });

        Assert.Equal(new[] { 1, 2, 3 }, filas.Select(f => f.Semana));
    }

    [Fact]
    public void Total_acumula_columna_a_columna()
    {
        var filas = ReporteContableResumenCalculos.Filas(new[]
        {
            Semana(1, mortH: 10, mortM: 2, selH: 100, selM: 5, trasH: 1, ventaH: 3,
                   alimento: 1_000.50m, agua: 20.25m, medicamento: 5m, vacuna: 2m, otros: 1m, totalGeneral: 1_028.75m),
            Semana(2, mortH: 7, mortM: 3, selH: 50, selM: 0, trasM: 4, ventaM: 6,
                   alimento: 2_000.25m, agua: 30.75m, medicamento: 1m, vacuna: 3m, otros: 4m, totalGeneral: 2_039.00m)
        });

        var total = ReporteContableResumenCalculos.Total(filas);

        Assert.Equal(22, total.Mortalidad);
        Assert.Equal(155, total.Seleccion);
        Assert.Equal(5, total.Traslados);
        Assert.Equal(9, total.Ventas);
        Assert.Equal(3_000.75m, total.Alimento);
        Assert.Equal(51.00m, total.Agua);
        Assert.Equal(6m, total.Medicamento);
        Assert.Equal(5m, total.Vacuna);
        Assert.Equal(5m, total.Otros);
        Assert.Equal(3_067.75m, total.TotalGeneral);
    }

    [Fact]
    public void Total_sobre_reporte_sin_semanas_devuelve_ceros_sin_reventar()
    {
        var total = ReporteContableResumenCalculos.Total(
            ReporteContableResumenCalculos.Filas(Array.Empty<ReporteContableSemanalDto>()));

        Assert.Equal(0, total.Mortalidad);
        Assert.Equal(0, total.Seleccion);
        Assert.Equal(0m, total.Alimento);
        Assert.Equal(0m, total.TotalGeneral);
    }

    [Fact]
    public void Total_no_altera_los_consumos_preexistentes_del_resumen()
    {
        // Regresión: agregar Selección no debe tocar ninguna de las columnas que ya existían.
        var semanas = new[]
        {
            Semana(1, mortH: 4, selH: 999, alimento: 12_345.67m, agua: 10m, medicamento: 1.5m,
                   vacuna: 2.5m, otros: 3.5m, totalGeneral: 12_363.17m)
        };

        var total = ReporteContableResumenCalculos.Total(ReporteContableResumenCalculos.Filas(semanas));

        Assert.Equal(12_345.67m, total.Alimento);
        Assert.Equal(12_363.17m, total.TotalGeneral);
        Assert.Equal(4, total.Mortalidad);
    }
}
