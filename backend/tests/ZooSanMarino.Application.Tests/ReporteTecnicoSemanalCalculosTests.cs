using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del cálculo puro del Reporte Técnico Semanal (Sanmarino postura):
/// parseo de guía, fórmulas del Excel (base fija, acumulados, incrementos,
/// nutrición, masa, conversión, apareo) y consolidación multi-galpón.
/// </summary>
public class ReporteTecnicoSemanalCalculosTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Parseo
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("  ", null)]
    [InlineData("-", null)]
    [InlineData("abc", null)]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData(" 3 ", 3.0)]
    public void ParseGuia_tolera_vacios_guiones_y_coma_decimal(string? entrada, double? esperado)
        => Assert.Equal(esperado, ReporteTecnicoSemanalCalculos.ParseGuia(entrada));

    [Theory]
    [InlineData("25", 25)]
    [InlineData("25.0", 25)]
    [InlineData("025", 25)]
    [InlineData("SEM 25", 25)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("sin numero", null)]
    public void ParseEdadSemana_tolerante(string? entrada, int? esperado)
        => Assert.Equal(esperado, ReporteTecnicoSemanalCalculos.ParseEdadSemana(entrada));

    [Fact]
    public void Pct_y_DesvPct_no_dividen_por_cero()
    {
        Assert.Null(ReporteTecnicoSemanalCalculos.Pct(5, 0));
        Assert.Equal(50.0, ReporteTecnicoSemanalCalculos.Pct(5, 10)!.Value, 10);
        Assert.Null(ReporteTecnicoSemanalCalculos.DesvPct(5, 0));
        Assert.Null(ReporteTecnicoSemanalCalculos.DesvPct(null, 10));
        Assert.Equal(10.0, ReporteTecnicoSemanalCalculos.DesvPct(110, 100)!.Value, 10);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Levante
    // ─────────────────────────────────────────────────────────────────────────
    private static ReporteSemanalLevanteExtrasRow FilaLevante(
        int semana, double baseH = 1000, double baseM = 100,
        int mortH = 0, int selH = 0, int errH = 0, double kgH = 0,
        double avesHFin = 1000, double avesMFin = 100,
        double? pesoH = null, double? kcal = null, double? prot = null,
        int dias = 7) => new()
    {
        Semana = semana,
        FechaFinSemana = new DateTime(2025, 1, 1).AddDays(semana * 7 - 1),
        DiasConRegistro = dias,
        BaseHembras = baseH,
        BaseMachos = baseM,
        AvesHembrasInicio = avesHFin + mortH + selH + errH,
        AvesHembrasFin = avesHFin,
        AvesMachosInicio = avesMFin,
        AvesMachosFin = avesMFin,
        MortalidadHembrasSem = mortH,
        SeleccionHembrasSem = selH,
        ErrorHembrasSem = errH,
        ConsumoKgHembrasSem = kgH,
        PesoHembrasSem = pesoH,
        KcalAlimentoHembras = kcal,
        ProtAlimentoHembras = prot
    };

    [Fact]
    public void Levante_porcentajes_usan_base_fija_de_aves_iniciales()
    {
        // Excel: F7 = mort / $C$7 (base fija), no sobre el saldo decreciente.
        var filas = new[]
        {
            FilaLevante(1, baseH: 1000, mortH: 10, avesHFin: 990),
            FilaLevante(2, baseH: 1000, mortH: 10, avesHFin: 980)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Equal(1.0, semanas[0].MortalidadHembrasPct!.Value, 10);
        Assert.Equal(1.0, semanas[1].MortalidadHembrasPct!.Value, 10); // 10/1000, NO 10/990
        Assert.Equal(2.0, semanas[1].MortalidadHembrasAcumPct!.Value, 10);
    }

    [Fact]
    public void Levante_retiro_acumulado_suma_mort_sel_error()
    {
        var filas = new[]
        {
            FilaLevante(1, baseH: 1000, mortH: 5, selH: 3, errH: 2, avesHFin: 990),
            FilaLevante(2, baseH: 1000, mortH: 5, selH: 0, errH: 0, avesHFin: 985)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Equal(1.0, semanas[0].RetiroAcumHembrasPct!.Value, 10);   // (5+3+2)/1000
        Assert.Equal(1.5, semanas[1].RetiroAcumHembrasPct!.Value, 10);   // 15/1000
    }

    [Fact]
    public void Levante_alimento_acumulado_incremento_y_consumo_por_ave()
    {
        var filas = new[]
        {
            FilaLevante(1, kgH: 700, avesHFin: 1000),   // gr/a/d = 700*1000/(1000*7) = 100
            FilaLevante(2, kgH: 1400, avesHFin: 1000)   // gr/a/d = 200
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Equal(100.0, semanas[0].GrAveDiaHembras!.Value, 8);
        Assert.Equal(100.0, semanas[0].IncrementoGrAveDiaHembras!.Value, 8); // 1ª semana = valor
        Assert.Equal(200.0, semanas[1].GrAveDiaHembras!.Value, 8);
        Assert.Equal(100.0, semanas[1].IncrementoGrAveDiaHembras!.Value, 8);
        Assert.Equal(2100.0, semanas[1].ConsumoKgHembrasAcum, 8);
        // Acumulado gr/ave = kg acumulados * 1000 / aves fin (Excel W = R*1000/C).
        Assert.Equal(2100.0, semanas[1].ConsumoAcumGrAveHembras!.Value, 8);
    }

    [Fact]
    public void Levante_nutricion_acumulada_replica_formula_excel()
    {
        // Excel AH = (kcal*0.001)*(gr_ave_semana) acumulado; AI con prot*0.01.
        var filas = new[]
        {
            FilaLevante(1, kgH: 700, avesHFin: 1000, kcal: 2900, prot: 19),  // gr_ave_sem = 700
            FilaLevante(2, kgH: 700, avesHFin: 1000, kcal: 2730, prot: 13.5)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Equal(2900 * 0.001 * 700, semanas[0].KcalAveAcumHembras!.Value, 6);
        Assert.Equal(2900 * 0.001 * 700 + 2730 * 0.001 * 700, semanas[1].KcalAveAcumHembras!.Value, 6);
        Assert.Equal(19 * 0.01 * 700 + 13.5 * 0.01 * 700, semanas[1].ProtAveAcumHembras!.Value, 6);
    }

    [Fact]
    public void Levante_peso_ganancia_y_desviacion_vs_guia()
    {
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>
        {
            [1] = new(null, null, null, null, null, null, null, null, PesoHembras: 500, PesoMachos: null, Uniformidad: 70),
            [2] = new(null, null, null, null, null, null, null, null, PesoHembras: 600, PesoMachos: null, Uniformidad: 75)
        };
        var filas = new[]
        {
            FilaLevante(1, pesoH: 550),
            FilaLevante(2, pesoH: 660)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(filas, guia);

        Assert.Equal(550, semanas[0].GananciaHembras!.Value, 8);   // 1ª semana = peso
        Assert.Equal(110, semanas[1].GananciaHembras!.Value, 8);
        Assert.Equal(10.0, semanas[0].DesviacionPesoHembrasPct!.Value, 8); // 550/500
        Assert.Equal(10.0, semanas[1].DesviacionPesoHembrasPct!.Value, 8); // 660/600
        Assert.Equal(70, semanas[0].UniformidadGuia);
    }

    [Fact]
    public void Levante_semana_sin_guia_deja_comparativos_null_sin_excepcion()
    {
        var filas = new[] { FilaLevante(1, mortH: 3, kgH: 100, pesoH: 500) };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Null(semanas[0].MortSelHembrasGuiaPct);
        Assert.Null(semanas[0].GrAveDiaHembrasGuia);
        Assert.Null(semanas[0].PesoHembrasGuia);
        Assert.Null(semanas[0].DesviacionPesoHembrasPct);
        Assert.Equal(3, semanas[0].MortalidadHembras);
    }

    [Fact]
    public void Levante_hueco_de_semana_no_rompe_acumulados()
    {
        var filas = new[]
        {
            FilaLevante(1, mortH: 10, kgH: 100),
            FilaLevante(3, mortH: 10, kgH: 100)  // semana 2 sin registros
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>());

        Assert.Equal(2, semanas.Count);
        Assert.Equal(3, semanas[1].Semana);
        Assert.Equal(2.0, semanas[1].MortalidadHembrasAcumPct!.Value, 10);
        Assert.Equal(200.0, semanas[1].ConsumoKgHembrasAcum, 8);
    }

    [Fact]
    public void ConsolidarLevante_suma_conteos_y_promedia_pesos()
    {
        var tab1 = new ReporteSemanalLevanteTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { LoteNombre = "G1", BaseHembras = 1000, BaseMachos = 100 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
                new[] { FilaLevante(1, baseH: 1000, mortH: 10, kgH: 700, avesHFin: 990, pesoH: 500) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>())
        };
        var tab2 = new ReporteSemanalLevanteTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { LoteNombre = "G2", BaseHembras = 2000, BaseMachos = 200 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
                new[] { FilaLevante(1, baseH: 2000, mortH: 20, kgH: 1400, avesHFin: 1980, pesoH: 700) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>())
        };

        var consolidado = ReporteTecnicoSemanalCalculos.ConsolidarLevante(new[] { tab1, tab2 });

        Assert.True(consolidado.Header.EsConsolidado);
        Assert.Equal(3000, consolidado.Header.BaseHembras);
        var s1 = Assert.Single(consolidado.Semanas);
        Assert.Equal(30, s1.MortalidadHembras);                       // suma
        Assert.Equal(1.0, s1.MortalidadHembrasPct!.Value, 10);        // 30/3000
        Assert.Equal(2100, s1.ConsumoKgHembras, 8);                   // suma
        Assert.Equal(600, s1.PesoHembras!.Value, 8);                  // promedio simple (500+700)/2
        Assert.Equal(2970, s1.AvesHembrasFin, 8);
    }

    [Fact]
    public void ConsolidarLevante_galpon_sin_dato_no_promedia_peso()
    {
        var tab1 = new ReporteSemanalLevanteTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { BaseHembras = 1000 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
                new[] { FilaLevante(1, pesoH: 500) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>())
        };
        var tab2 = new ReporteSemanalLevanteTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { BaseHembras = 1000 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(
                new[] { FilaLevante(1, pesoH: null) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>())
        };

        var consolidado = ReporteTecnicoSemanalCalculos.ConsolidarLevante(new[] { tab1, tab2 });
        Assert.Equal(500, Assert.Single(consolidado.Semanas).PesoHembras!.Value, 8);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Producción
    // ─────────────────────────────────────────────────────────────────────────
    private static IndicadorProduccionSemanalBdRow FilaProduccion(
        int semana, int avesHFin = 6000, int avesMFin = 600,
        int mortH = 0, int selH = 0, int mortM = 0,
        double kgH = 0, double kgM = 0,
        int huevosTot = 0, int huevosInc = 0,
        double? pesoHuevo = null, double eficiencia = 0,
        double? pesoHKg = null, double? pesoMKg = null,
        int dias = 7) => new()
    {
        Semana = semana,
        FechaInicioSemana = new DateTime(2025, 7, 1).AddDays((semana - 25) * 7),
        FechaFinSemana = new DateTime(2025, 7, 7).AddDays((semana - 25) * 7),
        TotalRegistros = dias,
        MortalidadHembras = mortH,
        MortalidadMachos = mortM,
        SeleccionHembras = selH,
        ConsumoKgHembras = kgH,
        ConsumoKgMachos = kgM,
        HuevosTotales = huevosTot,
        HuevosIncubables = huevosInc,
        PesoHuevoPromedio = pesoHuevo,
        EficienciaProduccion = eficiencia,
        AvesHembrasFinSemana = avesHFin,
        AvesMachosFinSemana = avesMFin,
        PesoPromedioHembras = pesoHKg,
        PesoPromedioMachos = pesoMKg
    };

    [Fact]
    public void Produccion_derivadas_masa_conversion_apareo_y_pct_incubables()
    {
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>
        {
            [26] = new(AprovSem: 82.1, AprovAc: 70.0, MasaHuevo: 21.9, GrHuevoInc: 400,
                       Apareo: 9.5, NacimPorcentaje: 80, PollitoAa: 0.5,
                       MortSemHembras: 0.15, MortSemMachos: 0.2)
        };
        var filas = new[]
        {
            FilaProduccion(26, avesHFin: 6000, avesMFin: 600, kgH: 5000, kgM: 500,
                huevosTot: 10000, huevosInc: 8000, pesoHuevo: 53, eficiencia: 25)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(filas, guia);
        var s = Assert.Single(semanas);

        Assert.Equal(80.0, s.PorcentajeIncubables!.Value, 8);                 // 8000/10000
        Assert.Equal(82.1, s.PorcentajeIncubablesGuia);
        Assert.Equal((5000.0 + 500.0) * 1000.0 / 8000.0, s.ConversionGrHuevoInc!.Value, 8);
        Assert.Equal(400, s.ConversionGrHuevoIncGuia);
        Assert.Equal(25.0 / 100.0 * 53.0, s.MasaHuevoLote!.Value, 8);         // %prod * pesoHuevo
        Assert.Equal(21.9, s.MasaHuevoGuia);
        Assert.Equal(10.0, s.ApareoPct!.Value, 8);                            // 600/6000
        Assert.Equal(9.5, s.ApareoGuiaPct);
        Assert.Equal(80, s.NacimientoGuiaPct);
        Assert.Equal(10000, s.HuevosTotalesAcum);
        Assert.Equal(8000, s.HuevosIncubablesAcum);
    }

    [Fact]
    public void Produccion_semana_25_sin_guia_no_rompe_y_guia_acumulada_corre()
    {
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>
        {
            [26] = new(null, null, null, null, null, null, null, MortSemHembras: 0.15, MortSemMachos: null),
            [27] = new(null, null, null, null, null, null, null, MortSemHembras: 0.15, MortSemMachos: null)
        };
        var filas = new[]
        {
            FilaProduccion(25, huevosTot: 100),
            FilaProduccion(26, huevosTot: 200),
            FilaProduccion(27, huevosTot: 300)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(filas, guia);

        Assert.Null(semanas[0].MortalidadHembrasAcumGuiaPct);                // 25 sin guía
        Assert.Equal(0.15, semanas[1].MortalidadHembrasAcumGuiaPct!.Value, 8);
        Assert.Equal(0.30, semanas[2].MortalidadHembrasAcumGuiaPct!.Value, 8);
        Assert.Equal(600, semanas[2].HuevosTotalesAcum);
    }

    [Fact]
    public void Produccion_pesos_se_exponen_en_gramos()
    {
        var filas = new[] { FilaProduccion(26, pesoHKg: 3.635, pesoMKg: 3.935) };
        var s = Assert.Single(ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>()));

        Assert.Equal(3635.0, s.PesoHembras!.Value, 6);
        Assert.Equal(3935.0, s.PesoMachos!.Value, 6);
    }

    [Fact]
    public void Produccion_mort_sel_acumulado_usa_base_de_primera_semana()
    {
        var filas = new[]
        {
            FilaProduccion(25, avesHFin: 5990, mortH: 8, selH: 2),   // base = 5990+10 = 6000
            FilaProduccion(26, avesHFin: 5980, mortH: 10, selH: 0)
        };
        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
            filas, new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>());

        Assert.Equal(10.0 / 6000.0 * 100.0, semanas[0].MortSelHembrasAcumPct!.Value, 8);
        Assert.Equal(20.0 / 6000.0 * 100.0, semanas[1].MortSelHembrasAcumPct!.Value, 8);
    }

    [Fact]
    public void ConsolidarProduccion_suma_huevos_y_recalcula_porcentajes()
    {
        var tab1 = new ReporteSemanalProduccionTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { BaseHembras = 6000, BaseMachos = 600 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                new[] { FilaProduccion(26, avesHFin: 6000, huevosTot: 7000, huevosInc: 6000, kgH: 5000, pesoHuevo: 50) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>())
        };
        var tab2 = new ReporteSemanalProduccionTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { BaseHembras = 6000, BaseMachos = 600 },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                new[] { FilaProduccion(26, avesHFin: 6000, huevosTot: 7000, huevosInc: 5000, kgH: 5000, pesoHuevo: 54) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>())
        };

        var consolidado = ReporteTecnicoSemanalCalculos.ConsolidarProduccion(new[] { tab1, tab2 });
        var s = Assert.Single(consolidado.Semanas);

        Assert.Equal(14000, s.HuevosTotales);
        Assert.Equal(11000, s.HuevosIncubables);
        Assert.Equal(11000.0 / 14000.0 * 100.0, s.PorcentajeIncubables!.Value, 8);
        Assert.Equal(12000, s.AvesHembrasFin);
        Assert.Equal(10000, s.ConsumoKgHembras, 8);
        Assert.Equal(52.0, s.PesoHuevo!.Value, 8);                    // promedio simple
        // %producción recalculado sobre las sumas: (14000/7) / 12000 * 100
        Assert.Equal(14000.0 / 7.0 / 12000.0 * 100.0, s.PorcentajeProduccion!.Value, 8);
    }
}
