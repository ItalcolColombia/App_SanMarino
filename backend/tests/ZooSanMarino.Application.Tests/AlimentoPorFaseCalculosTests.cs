using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la hoja «ALIMLev» del Informe RA Pesadas: energía y proteína
/// agrupadas por FASE DE ALIMENTO (la fase la fija la guía, no la edad).
/// </summary>
public class AlimentoPorFaseCalculosTests
{
    private static ReporteSemanalLevanteSemanaDto Sem(
        int semana, string? faseH, double? kcalH, double? kcalHGuia,
        string? faseM = null, double? kcalM = null, double? kcalMGuia = null,
        double? protH = null, double? protHGuia = null) =>
        new()
        {
            Semana = semana,
            FaseAlimentoHembras = faseH,
            FaseAlimentoMachos = faseM,
            KcalSemanaHembras = kcalH,
            KcalSemanaHembrasGuia = kcalHGuia,
            KcalSemanaMachos = kcalM,
            KcalSemanaMachosGuia = kcalMGuia,
            ProtSemanaHembras = protH,
            ProtSemanaHembrasGuia = protHGuia
        };

    // ─────────────────────────────────────────────────────────────────────────
    // Agrupación
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Agrupa_PorFaseDeLaGuia_YSumaCadaUna()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 100, 90),
            Sem(2, "INI", 110, 100),
            Sem(3, "LEV", 200, 210),
            Sem(4, "LEV", 220, 220)
        };

        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(3, filas.Count);                       // INI, LEV, Total general
        Assert.Equal("INI", filas[0].Fase);
        Assert.Equal(210, filas[0].Real);
        Assert.Equal(190, filas[0].Guia);
        Assert.Equal(2, filas[0].Semanas);
        Assert.Equal("LEV", filas[1].Fase);
        Assert.Equal(420, filas[1].Real);
        Assert.Equal(430, filas[1].Guia);
    }

    [Fact]
    public void ElOrdenDeLasFasesEsCronologico_NoAlfabetico()
    {
        // Alfabéticamente sería F1 < INI < LEV < PP; el archivo las muestra en el
        // orden en que el lote las va usando.
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 1, 1), Sem(7, "LEV", 1, 1), Sem(20, "PP", 1, 1), Sem(25, "F1", 1, 1)
        };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(new[] { "INI", "LEV", "PP", "F1", AlimentoPorFaseCalculos.TotalGeneral },
                     filas.Select(f => f.Fase).ToArray());
    }

    [Fact]
    public void ElOrdenNoDependeDelOrdenDeEntrada()
    {
        var desordenadas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(25, "F1", 1, 1), Sem(1, "INI", 1, 1), Sem(20, "PP", 1, 1), Sem(7, "LEV", 1, 1)
        };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            desordenadas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(new[] { "INI", "LEV", "PP", "F1", AlimentoPorFaseCalculos.TotalGeneral },
                     filas.Select(f => f.Fase).ToArray());
    }

    [Fact]
    public void SemanaSinFaseEnLaGuia_NoEntraEnNingunaFase()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 100, 90),
            Sem(2, null, 999, 999),      // la guía no trae alimento para esa semana
            Sem(3, "  ", 888, 888)       // en blanco cuenta como sin fase
        };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(2, filas.Count);                 // INI + Total
        Assert.Equal(100, filas[0].Real);
        Assert.Equal(100, filas[^1].Real);            // el total tampoco los suma
    }

    [Fact]
    public void LaFaseSeLimpiaDeEspacios()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto> { Sem(1, " INI ", 10, 10), Sem(2, "INI", 5, 5) };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(2, filas.Count);                 // una sola fase INI + total
        Assert.Equal(15, filas[0].Real);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Diferencias
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DiferenciaYPorcentaje()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto> { Sem(1, "INI", 110, 100) };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(10, filas[0].Diferencia);
        Assert.Equal(10.0, filas[0].DiferenciaPct!.Value, 10);
    }

    [Fact]
    public void GuiaEnCero_NoDivideEntreCero()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto> { Sem(1, "INI", 110, 0) };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(110, filas[0].Diferencia);
        Assert.Null(filas[0].DiferenciaPct);
    }

    [Fact]
    public void SinDatoReal_QuedaNull_NoCero()
    {
        // «No se midió» y «se midió cero» no son lo mismo: la fase sin ninguna
        // semana con dato no puede mostrar 0, porque leería como consumo nulo.
        var semanas = new List<ReporteSemanalLevanteSemanaDto> { Sem(1, "INI", null, 100) };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Null(filas[0].Real);
        Assert.Equal(100, filas[0].Guia);
        Assert.Null(filas[0].Diferencia);
        Assert.Null(filas[0].DiferenciaPct);
    }

    [Fact]
    public void UnaSemanaSinDatoNoAnulaLasOtrasDeLaMismaFase()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 100, 100),
            Sem(2, "INI", null, 100)
        };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Equal(100, filas[0].Real);
        Assert.Equal(200, filas[0].Guia);
        Assert.Equal(2, filas[0].Semanas);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Total general
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TotalGeneral_SumaLasFases()
    {
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 100, 90), Sem(7, "LEV", 200, 210)
        };
        var filas = AlimentoPorFaseCalculos.Agrupar(
            semanas, s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        var total = filas[^1];
        Assert.Equal(AlimentoPorFaseCalculos.TotalGeneral, total.Fase);
        Assert.Equal(300, total.Real);
        Assert.Equal(300, total.Guia);
        Assert.Equal(0, total.Diferencia);
        Assert.Equal(2, total.Semanas);
    }

    [Fact]
    public void SinSemanas_DevuelveListaVacia_SinFilaDeTotal()
    {
        var filas = AlimentoPorFaseCalculos.Agrupar(
            new List<ReporteSemanalLevanteSemanaDto>(),
            s => s.FaseAlimentoHembras, s => s.KcalSemanaHembras, s => s.KcalSemanaHembrasGuia);

        Assert.Empty(filas);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Las cuatro tablas de la hoja
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // Energía del alimento: capturada vs nominal de la guía
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SinAlimentoCapturado_UsaLaEnergiaNominalDeLaGuia()
    {
        // kcal_al_h / prot_al_h NO se cargan en ningún registro del sistema; sin
        // el respaldo nominal la mitad hembra de la hoja saldría vacía.
        var extras = new[] { ExtrasSem(1, avesH: 1000, kgH: 100, kcalAlH: null) };
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>
        {
            [1] = GuiaSem(kcalAlimentoHembras: 2900)
        };

        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(extras, guia);

        // 100 kg / 1000 aves = 100 g/ave ⇒ 100 * 2900 * 0.001 = 290 kcal/ave
        Assert.Equal(290.0, semanas[0].KcalSemanaHembras!.Value, 6);
    }

    [Fact]
    public void ElAlimentoCapturadoLeGanaAlNominal()
    {
        var extras = new[] { ExtrasSem(1, avesH: 1000, kgH: 100, kcalAlH: 3000) };
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>
        {
            [1] = GuiaSem(kcalAlimentoHembras: 2900)
        };

        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(extras, guia);

        Assert.Equal(300.0, semanas[0].KcalSemanaHembras!.Value, 6);   // usa 3000, no 2900
    }

    [Fact]
    public void SinConsumo_NoInventaEnergia()
    {
        var extras = new[] { ExtrasSem(1, avesH: 1000, kgH: 0, kcalAlH: null) };
        var guia = new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaLevante>
        {
            [1] = GuiaSem(kcalAlimentoHembras: 2900)
        };

        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasLevante(extras, guia);

        Assert.Null(semanas[0].KcalSemanaHembras);
    }

    private static ReporteSemanalLevanteExtrasRow ExtrasSem(
        int semana, double avesH, double kgH, double? kcalAlH) =>
        new()
        {
            Semana = semana,
            DiasConRegistro = 7,
            BaseHembras = avesH,
            AvesHembrasInicio = avesH,
            AvesHembrasFin = avesH,
            ConsumoKgHembrasSem = kgH,
            KcalAlimentoHembras = kcalAlH
        };

    private static ReporteTecnicoSemanalCalculos.GuiaSemanaLevante GuiaSem(double? kcalAlimentoHembras) =>
        new(MortSemHembras: null, MortSemMachos: null, RetiroAcHembras: null, RetiroAcMachos: null,
            GrAveDiaHembras: null, GrAveDiaMachos: null, ConsAcHembras: null, ConsAcMachos: null,
            PesoHembras: null, PesoMachos: null, Uniformidad: null,
            AlimentoHembras: "INI", AlimentoMachos: "INI",
            KcalAlimentoHembras: kcalAlimentoHembras);

    [Fact]
    public void Construir_ArmaLasCuatroTablas_ConSusPropiasFases()
    {
        // Las fases de macho NO son las mismas que las de hembra: el macho pasa
        // de LEV directo a M, sin PP ni F1.
        var semanas = new List<ReporteSemanalLevanteSemanaDto>
        {
            Sem(1, "INI", 100, 90, faseM: "INI", kcalM: 150, kcalMGuia: 140, protH: 10, protHGuia: 9),
            Sem(20, "PP", 300, 290, faseM: "LEV", kcalM: 400, kcalMGuia: 380, protH: 20, protHGuia: 19),
            Sem(25, "F1", 500, 480, faseM: "M", kcalM: 600, kcalMGuia: 590, protH: 30, protHGuia: 29)
        };

        var r = AlimentoPorFaseCalculos.Construir(semanas);

        Assert.Equal(new[] { "INI", "PP", "F1", AlimentoPorFaseCalculos.TotalGeneral },
                     r.EnergiaHembras.Select(f => f.Fase).ToArray());
        Assert.Equal(new[] { "INI", "LEV", "M", AlimentoPorFaseCalculos.TotalGeneral },
                     r.EnergiaMachos.Select(f => f.Fase).ToArray());
        Assert.Equal(900, r.EnergiaHembras[^1].Real);
        Assert.Equal(1150, r.EnergiaMachos[^1].Real);
        Assert.Equal(60, r.ProteinaHembras[^1].Real);
        Assert.DoesNotContain(r.ProteinaMachos, f => f.Real.HasValue); // sin proteína macho cargada
    }
}
