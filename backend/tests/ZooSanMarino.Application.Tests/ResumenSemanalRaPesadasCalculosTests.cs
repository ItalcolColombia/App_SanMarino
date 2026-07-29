using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del cálculo puro de la hoja «RESUMEN SEMANAL» del Informe RA Pesadas:
/// semana calendario con la convención WEEKNUM de Excel, participación y
/// promedios ponderados por saldo de hembras.
/// </summary>
public class ResumenSemanalRaPesadasCalculosTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Semana del año — WEEKNUM de Excel, NO ISO
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Casos tomados del archivo fuente (hoja «Datos semanal LEV»): la columna
    /// SemAño coincide con WEEKNUM en las 1825 filas y solo en 1736 con ISO.
    /// </summary>
    [Theory]
    [InlineData(2024, 6, 6, 23)]    // jueves — SemAño del archivo = 23
    [InlineData(2024, 6, 13, 24)]
    [InlineData(2024, 6, 20, 25)]
    [InlineData(2024, 6, 27, 26)]
    [InlineData(2024, 7, 4, 27)]
    [InlineData(2024, 7, 11, 28)]
    public void SemanaExcel_CoincideConElArchivoFuente(int a, int m, int d, int esperada)
        => Assert.Equal(esperada, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(a, m, d)));

    [Fact]
    public void SemanaExcel_ElPrimeroDeEneroSiempreEsSemana1()
    {
        for (var anio = 2020; anio <= 2030; anio++)
            Assert.Equal(1, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(anio, 1, 1)));
    }

    [Fact]
    public void SemanaExcel_LaSemanaAvanzaLosDomingos()
    {
        // 2025-01-01 es miércoles ⇒ la semana 2 arranca el domingo 2025-01-05.
        Assert.Equal(1, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(2025, 1, 4)));  // sábado
        Assert.Equal(2, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(2025, 1, 5)));  // domingo
        Assert.Equal(2, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(2025, 1, 11)));
        Assert.Equal(3, ResumenSemanalRaPesadasCalculos.SemanaExcel(new DateOnly(2025, 1, 12)));
    }

    [Fact]
    public void SemanaExcel_DifiereDeISO_EnLosDiasDeArranqueDeAnio()
    {
        // 2025-01-01 (miércoles): ISO lo pone en la semana 1 del 2025 también,
        // pero el 2024-12-30 (lunes) es ISO semana 1 de 2025 y WEEKNUM semana 53
        // de 2024. El reporte usa WEEKNUM: el dato pertenece al año calendario.
        var d = new DateOnly(2024, 12, 30);
        Assert.Equal(53, ResumenSemanalRaPesadasCalculos.SemanaExcel(d));
        Assert.NotEqual(System.Globalization.ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue)),
                        ResumenSemanalRaPesadasCalculos.SemanaExcel(d));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Rango de la semana
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void RangoSemanaExcel_VaDeDomingoASabado()
    {
        var (inicio, fin) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(2024, 23);
        Assert.Equal(DayOfWeek.Sunday, inicio.DayOfWeek);
        Assert.Equal(DayOfWeek.Saturday, fin.DayOfWeek);
        Assert.Equal(6, fin.DayNumber - inicio.DayNumber);
    }

    [Fact]
    public void RangoSemanaExcel_ContieneLasFechasQueMapeanAEsaSemana()
    {
        var (inicio, fin) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(2024, 23);
        for (var d = inicio; d <= fin; d = d.AddDays(1))
        {
            if (d.Year != 2024) continue;   // la semana 1 puede arrancar en diciembre anterior
            Assert.Equal(23, ResumenSemanalRaPesadasCalculos.SemanaExcel(d));
        }
    }

    [Fact]
    public void RangoSemanaExcel_LaSemana1PuedeArrancarEnElAnioAnterior()
    {
        // 2025-01-01 es miércoles ⇒ la semana 1 arranca el domingo 2024-12-29.
        var (inicio, _) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(2025, 1);
        Assert.Equal(new DateOnly(2024, 12, 29), inicio);
    }

    [Fact]
    public void RangoSemanaExcel_IdaYVuelta_ParaTodoElAnio()
    {
        for (var d = new DateOnly(2025, 1, 1); d <= new DateOnly(2025, 12, 31); d = d.AddDays(1))
        {
            var sem = ResumenSemanalRaPesadasCalculos.SemanaExcel(d);
            var (inicio, fin) = ResumenSemanalRaPesadasCalculos.RangoSemanaExcel(2025, sem);
            Assert.True(d >= inicio && d <= fin, $"{d} quedó fuera del rango de su propia semana {sem}");
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Etapa
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("levante", "levante")]
    [InlineData("LEVANTE", "levante")]
    [InlineData("  Produccion  ", "produccion")]
    [InlineData("produccion", "produccion")]
    public void NormalizarEtapa_AceptaLasDosValidas(string entrada, string esperada)
        => Assert.Equal(esperada, ResumenSemanalRaPesadasCalculos.NormalizarEtapa(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("producción")]   // con tilde: NO es la clave del contrato
    [InlineData("engorde")]
    public void NormalizarEtapa_RechazaLoDemas(string? entrada)
        => Assert.Null(ResumenSemanalRaPesadasCalculos.NormalizarEtapa(entrada));

    // ─────────────────────────────────────────────────────────────────────────
    // Participación
    // ─────────────────────────────────────────────────────────────────────────

    private static ResumenSemanalLevanteRow Lev(string nombre, double saldoH, double saldoM = 0) =>
        new() { LoteNombre = nombre, SaldoHembras = saldoH, SaldoMachos = saldoM };

    [Fact]
    public void Participacion_EsElSaldoDeHembrasSobreElTotal()
    {
        // Caso real del archivo: EC33-G tiene 9.312 hembras sobre 630.702 de la
        // selección ⇒ PART = 0,014764500508956686 (valor exacto de la hoja).
        var filas = new List<ResumenSemanalLevanteRow> { Lev("EC33-G", 9312), Lev("resto", 630702 - 9312) };
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(filas);
        Assert.Equal(0.014764500508956686, filas[0].Part!.Value, 15);
    }

    [Fact]
    public void Participacion_SumaUno()
    {
        var filas = new List<ResumenSemanalLevanteRow> { Lev("a", 9312), Lev("b", 28582), Lev("c", 24349) };
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(filas);
        Assert.Equal(1.0, filas.Sum(f => f.Part ?? 0), 12);
    }

    [Fact]
    public void Participacion_SeRecalculaAlRecortarPorAlcance()
    {
        // La fn SQL trae los 3 lotes; el alcance del usuario deja solo 2.
        // Si no se recalcula, las participaciones suman 0,61 y los ponderados
        // quedan mal. Este es el bug que RecalcularParticipacion evita.
        var todos = new List<ResumenSemanalLevanteRow> { Lev("a", 9312), Lev("b", 28582), Lev("c", 24349) };
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(todos);

        var visibles = todos.Take(2).ToList();
        Assert.NotEqual(1.0, visibles.Sum(f => f.Part ?? 0), 3);   // antes de recalcular

        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(visibles);
        Assert.Equal(1.0, visibles.Sum(f => f.Part ?? 0), 12);     // después
    }

    [Fact]
    public void Participacion_SinSaldoQuedaNull_NoCero()
    {
        var filas = new List<ResumenSemanalLevanteRow> { Lev("a", 0), Lev("b", 0) };
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(filas);
        Assert.All(filas, f => Assert.Null(f.Part));
    }

    [Fact]
    public void Participacion_SinFilasNoRevienta()
    {
        var filas = new List<ResumenSemanalLevanteRow>();
        ResumenSemanalRaPesadasCalculos.RecalcularParticipacionLevante(filas);
        Assert.Empty(filas);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Promedio ponderado
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PromedioPonderado_PesaPorSaldo_NoEsPromedioSimple()
    {
        // 90 con 9.000 aves y 80 con 1.000 ⇒ ponderado 89, simple 85.
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 9000, UniformidadHembras = 90 },
            new() { SaldoHembras = 1000, UniformidadHembras = 80 }
        };
        var p = ResumenSemanalRaPesadasCalculos.PromedioPonderado(filas, f => f.UniformidadHembras, f => f.SaldoHembras);
        Assert.Equal(89.0, p!.Value, 10);
        Assert.NotEqual(85.0, p.Value, 10);
    }

    [Fact]
    public void PromedioPonderado_LosLotesSinValorNoCuentanComoCero()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 1000, UniformidadHembras = 90 },
            new() { SaldoHembras = 1000, UniformidadHembras = null }   // sin pesaje esa semana
        };
        var p = ResumenSemanalRaPesadasCalculos.PromedioPonderado(filas, f => f.UniformidadHembras, f => f.SaldoHembras);
        Assert.Equal(90.0, p!.Value, 10);   // no 45
    }

    [Fact]
    public void PromedioPonderado_ConPesoCeroElLoteNoAporta()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 1000, UniformidadHembras = 90 },
            new() { SaldoHembras = 0,    UniformidadHembras = 10 }
        };
        var p = ResumenSemanalRaPesadasCalculos.PromedioPonderado(filas, f => f.UniformidadHembras, f => f.SaldoHembras);
        Assert.Equal(90.0, p!.Value, 10);
    }

    [Fact]
    public void PromedioPonderado_SinDatosDevuelveNull()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 1000, UniformidadHembras = null }
        };
        Assert.Null(ResumenSemanalRaPesadasCalculos.PromedioPonderado(
            filas, f => f.UniformidadHembras, f => f.SaldoHembras));
        Assert.Null(ResumenSemanalRaPesadasCalculos.PromedioPonderado(
            new List<ResumenSemanalLevanteRow>(), f => f.UniformidadHembras, f => f.SaldoHembras));
    }

    [Fact]
    public void PromedioPonderado_AdmiteValoresNegativos()
    {
        // Las columnas de desviación (%DifPeso, %DifCons) son negativas cuando
        // el lote está por debajo de la guía: no se pueden descartar.
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 1000, DifPesoHembrasPct = -6.0 },
            new() { SaldoHembras = 1000, DifPesoHembrasPct = 2.0 }
        };
        var p = ResumenSemanalRaPesadasCalculos.PromedioPonderado(filas, f => f.DifPesoHembrasPct, f => f.SaldoHembras);
        Assert.Equal(-2.0, p!.Value, 10);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Totales
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void TotalesLevante_SumaConteosYPonderaIndicadores()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            new() { SaldoHembras = 9000, SaldoMachos = 900, UniformidadHembras = 90, MortHembrasPct = 0.02 },
            new() { SaldoHembras = 1000, SaldoMachos = 100, UniformidadHembras = 80, MortHembrasPct = 0.10 }
        };
        var t = ResumenSemanalRaPesadasCalculos.TotalesLevante(filas);

        Assert.Equal(2, t.Lotes);
        Assert.Equal(10000, t.SaldoHembras);
        Assert.Equal(1000, t.SaldoMachos);
        Assert.Equal(89.0, t.Ponderados["uniformidadHembras"]!.Value, 10);
        Assert.Equal(0.028, t.Ponderados["mortHembrasPct"]!.Value, 10);
    }

    [Fact]
    public void TotalesProduccion_SumaConteosYPonderaIndicadores()
    {
        var filas = new List<ResumenSemanalProduccionRow>
        {
            new() { SaldoHembras = 20000, SaldoMachos = 1600, ProduccionPct = 80, Htaa = 100 },
            new() { SaldoHembras = 5000,  SaldoMachos = 400,  ProduccionPct = 60, Htaa = 50 }
        };
        var t = ResumenSemanalRaPesadasCalculos.TotalesProduccion(filas);

        Assert.Equal(2, t.Lotes);
        Assert.Equal(25000, t.SaldoHembras);
        Assert.Equal(2000, t.SaldoMachos);
        Assert.Equal(76.0, t.Ponderados["produccionPct"]!.Value, 10);
        Assert.Equal(90.0, t.Ponderados["htaa"]!.Value, 10);
    }

    [Fact]
    public void Totales_SinFilasDaCerosYPonderadosNull()
    {
        var t = ResumenSemanalRaPesadasCalculos.TotalesLevante(new List<ResumenSemanalLevanteRow>());
        Assert.Equal(0, t.Lotes);
        Assert.Equal(0, t.SaldoHembras);
        Assert.All(t.Ponderados.Values, v => Assert.Null(v));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Curva consolidada por edad
    // ─────────────────────────────────────────────────────────────────────────

    private static ResumenSemanalLevanteRow Curva(
        int loteId, int edad, double saldoH, double? unif = null, double saldoM = 0) =>
        new() { LoteId = loteId, EdadSemana = edad, SaldoHembras = saldoH, SaldoMachos = saldoM, UniformidadHembras = unif };

    [Fact]
    public void Curva_AgrupaPorEdad_AunqueLosLotesEstenEnFechasDistintas()
    {
        // Justo la gracia de mirar por EDAD: dos lotes encasetados en meses
        // distintos comparten el punto de la curva cuando llegan a la misma edad.
        var filas = new List<ResumenSemanalLevanteRow>
        {
            Curva(1, 5, 1000), Curva(2, 5, 3000), Curva(1, 6, 990)
        };

        var curva = ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(filas);

        Assert.Equal(new[] { 5, 6 }, curva.Select(p => p.EdadSemana).ToArray());
        Assert.Equal(2, curva[0].Lotes);
        Assert.Equal(4000, curva[0].SaldoHembras);
        Assert.Equal(1, curva[1].Lotes);
    }

    [Fact]
    public void Curva_PonderaPorSaldoDeHembras()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            Curva(1, 5, 9000, unif: 90),
            Curva(2, 5, 1000, unif: 80)
        };

        var curva = ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(filas);

        Assert.Equal(89.0, curva[0].Indicadores["uniformidadHembras"]!.Value, 10);
    }

    [Fact]
    public void Curva_CuentaLOTES_NoFilas()
    {
        // Un mismo lote puede caer dos veces en la misma edad si el año tiene dos
        // semanas calendario que le corresponden: sigue siendo UN lote.
        var filas = new List<ResumenSemanalLevanteRow>
        {
            Curva(1, 5, 1000), Curva(1, 5, 1000), Curva(2, 5, 1000)
        };

        var curva = ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(filas);

        Assert.Equal(2, curva[0].Lotes);
        Assert.Equal(3000, curva[0].SaldoHembras);   // los saldos sí suman todas las filas
    }

    [Fact]
    public void Curva_SaleOrdenadaPorEdad()
    {
        var filas = new List<ResumenSemanalLevanteRow>
        {
            Curva(1, 20, 100), Curva(1, 3, 100), Curva(1, 11, 100)
        };

        var curva = ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(filas);

        Assert.Equal(new[] { 3, 11, 20 }, curva.Select(p => p.EdadSemana).ToArray());
    }

    [Fact]
    public void Curva_SinFilasDaCurvaVacia()
    {
        Assert.Empty(ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadLevante(
            new List<ResumenSemanalLevanteRow>()));
        Assert.Empty(ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadProduccion(
            new List<ResumenSemanalProduccionRow>()));
    }

    [Fact]
    public void CurvaProduccion_PonderaYSumaIgual()
    {
        var filas = new List<ResumenSemanalProduccionRow>
        {
            new() { LotePosturaProduccionId = 1, EdadSemana = 30, SaldoHembras = 20000, ProduccionPct = 80 },
            new() { LotePosturaProduccionId = 2, EdadSemana = 30, SaldoHembras = 5000,  ProduccionPct = 60 }
        };

        var curva = ResumenSemanalRaPesadasCalculos.ConsolidarPorEdadProduccion(filas);

        Assert.Equal(2, curva[0].Lotes);
        Assert.Equal(25000, curva[0].SaldoHembras);
        Assert.Equal(76.0, curva[0].Indicadores["produccionPct"]!.Value, 10);
    }
}
