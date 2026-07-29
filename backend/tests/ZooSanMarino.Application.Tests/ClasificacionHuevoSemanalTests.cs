using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.ReporteTecnicoSemanal;
using Xunit;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la hoja «CLAS Huevo» del Informe RA Pesadas: conteos de la semana y
/// su % sobre el huevo TOTAL, más la consolidación multi-galpón.
/// </summary>
public class ClasificacionHuevoSemanalTests
{
    private static IndicadorProduccionSemanalBdRow Fila(
        int semana, int total, int limpios = 0, int tratados = 0, int sucios = 0,
        int deformes = 0, int blancos = 0, int dobleYema = 0, int piso = 0,
        int pequenos = 0, int rotos = 0, int desecho = 0, int otro = 0) =>
        new()
        {
            Semana = semana,
            TotalRegistros = 7,
            HuevosTotales = total,
            HuevosLimpios = limpios,
            HuevosTratados = tratados,
            HuevosSucios = sucios,
            HuevosDeformes = deformes,
            HuevosBlancos = blancos,
            HuevosDobleYema = dobleYema,
            HuevosPiso = piso,
            HuevosPequenos = pequenos,
            HuevosRotos = rotos,
            HuevosDesecho = desecho,
            HuevosOtro = otro
        };

    private static ReporteSemanalProduccionSemanaDto Construir(IndicadorProduccionSemanalBdRow fila)
        => ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
               new[] { fila },
               new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>())[0];

    // ─────────────────────────────────────────────────────────────────────────
    // Mapeo Excel ↔ BD
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeformeBlanco_SumaLasDosColumnasDeLaBD()
    {
        // El Excel trae UNA columna «Deforme Blanco»; la BD guarda huevo_deforme
        // y huevo_blanco por separado. Si no se suman, el reporte muestra la mitad.
        var dto = Construir(Fila(30, total: 1000, deformes: 30, blancos: 20));

        Assert.Equal(50, dto.HuevosDeformeBlanco);
        Assert.Equal(5.0, dto.PctDeformeBlanco!.Value, 10);
    }

    [Fact]
    public void PorcentajesSobreElHuevoTotal()
    {
        var dto = Construir(Fila(30, total: 2000,
            limpios: 1000, tratados: 100, sucios: 60, piso: 80,
            pequenos: 40, rotos: 20, desecho: 10, dobleYema: 50, otro: 5));

        Assert.Equal(50.0, dto.PctLimpio!.Value, 10);
        Assert.Equal(5.0, dto.PctTratado!.Value, 10);
        Assert.Equal(3.0, dto.PctSucio!.Value, 10);
        Assert.Equal(4.0, dto.PctPiso!.Value, 10);
        Assert.Equal(2.0, dto.PctPequeno!.Value, 10);
        Assert.Equal(1.0, dto.PctRoto!.Value, 10);
        Assert.Equal(0.5, dto.PctDesecho!.Value, 10);
        Assert.Equal(2.5, dto.PctDobleYema!.Value, 10);
        Assert.Equal(0.25, dto.PctOtro!.Value, 10);
    }

    [Fact]
    public void LosConteosSeCopianTalCual()
    {
        var dto = Construir(Fila(30, total: 1000, limpios: 700, tratados: 50, sucios: 30,
            piso: 25, pequenos: 15, rotos: 10, desecho: 5, dobleYema: 20, otro: 3));

        Assert.Equal(700, dto.HuevosLimpios);
        Assert.Equal(50, dto.HuevosTratados);
        Assert.Equal(30, dto.HuevosSucios);
        Assert.Equal(25, dto.HuevosPiso);
        Assert.Equal(15, dto.HuevosPequenos);
        Assert.Equal(10, dto.HuevosRotos);
        Assert.Equal(5, dto.HuevosDesecho);
        Assert.Equal(20, dto.HuevosDobleYema);
        Assert.Equal(3, dto.HuevosOtro);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Bordes
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SemanaSinHuevos_DaNull_NoCeroNiDivisionPorCero()
    {
        // Semana 25 típica: hay aves y consumo pero todavía no hay postura.
        var dto = Construir(Fila(25, total: 0, limpios: 0));

        Assert.Null(dto.PctLimpio);
        Assert.Null(dto.PctSucio);
        Assert.Null(dto.PctDeformeBlanco);
        Assert.Equal(0, dto.HuevosLimpios);
    }

    [Fact]
    public void ClasificacionSinCargar_DaCeroPorcientoPeroNoNull()
    {
        // Hay huevos pero nadie clasificó: 0 % es la lectura correcta (se midió
        // el total y no se reportó ninguna categoría), distinto de «sin huevos».
        var dto = Construir(Fila(30, total: 1000));

        Assert.Equal(0.0, dto.PctLimpio!.Value, 10);
        Assert.Equal(0.0, dto.PctSucio!.Value, 10);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Consolidado multi-galpón
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Consolidado_SumaConteosYRecalculaPorcentajes()
    {
        // Galpón grande: 9.000 huevos, 90 % limpio. Galpón chico: 1.000, 50 %.
        // El consolidado correcto es 86 %, NO el promedio simple de 70 %.
        var tabA = new ReporteSemanalProduccionTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { LoteNombre = "G1" },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                new[] { Fila(30, total: 9000, limpios: 8100) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>())
        };
        var tabB = new ReporteSemanalProduccionTabDto
        {
            Header = new ReporteSemanalTabHeaderDto { LoteNombre = "G2" },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                new[] { Fila(30, total: 1000, limpios: 500) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>())
        };

        var cons = ReporteTecnicoSemanalCalculos.ConsolidarProduccion(new[] { tabA, tabB });
        var sem = cons.Semanas.Single(s => s.Semana == 30);

        Assert.Equal(8600, sem.HuevosLimpios);
        Assert.Equal(86.0, sem.PctLimpio!.Value, 10);
        Assert.NotEqual(70.0, sem.PctLimpio!.Value, 10);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Venta de aves (columnas VentaH / VentaM)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ventas_SeAgrupanPorSemanaDeVida_YSeparanSexos()
    {
        var encaset = new DateTime(2025, 1, 6);
        var ventas = new[]
        {
            (encaset.AddDays(0),   100, 10),   // semana 1
            (encaset.AddDays(6),    50,  5),   // semana 1 también
            (encaset.AddDays(7),   200, 20)    // semana 2
        };

        var r = ReporteTecnicoSemanalCalculos.AgruparVentasPorSemana(ventas, encaset);

        Assert.Equal((150, 15), r[1]);
        Assert.Equal((200, 20), r[2]);
    }

    [Fact]
    public void Ventas_AnterioresAlEncaset_SeIgnoran()
    {
        // Dato inconsistente: no puede haber una venta antes de que existan aves.
        // Se descarta en vez de caer en una «semana 0» que el reporte no tiene.
        var encaset = new DateTime(2025, 1, 6);
        var r = ReporteTecnicoSemanalCalculos.AgruparVentasPorSemana(
            new[] { (encaset.AddDays(-3), 100, 10) }, encaset);

        Assert.Empty(r);
    }

    [Fact]
    public void Ventas_SinMovimientos_DaSemanasEnCero()
    {
        var dto = Construir(Fila(30, total: 1000));
        Assert.Equal(0, dto.VentaHembras);
        Assert.Equal(0, dto.VentaMachos);
    }

    [Fact]
    public void Ventas_LleganALaSemanaCorrespondiente()
    {
        var ventas = new Dictionary<int, (int Hembras, int Machos)> { [30] = (500, 40) };

        var semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
            new[] { Fila(29, total: 1000), Fila(30, total: 1000) },
            new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>(),
            cargadosPorSemana: null,
            ventasPorSemana: ventas);

        Assert.Equal(0, semanas.Single(s => s.Semana == 29).VentaHembras);
        Assert.Equal(500, semanas.Single(s => s.Semana == 30).VentaHembras);
        Assert.Equal(40, semanas.Single(s => s.Semana == 30).VentaMachos);
    }

    [Fact]
    public void Ventas_EnElConsolidado_Suman()
    {
        // Son conteos de aves, no valores por ave: suman entre galpones.
        ReporteSemanalProduccionTabDto Tab(string nombre, int ventaH) => new()
        {
            Header = new ReporteSemanalTabHeaderDto { LoteNombre = nombre },
            Semanas = ReporteTecnicoSemanalCalculos.ConstruirSemanasProduccion(
                new[] { Fila(30, total: 1000) },
                new Dictionary<int, ReporteTecnicoSemanalCalculos.GuiaSemanaProduccion>(),
                cargadosPorSemana: null,
                ventasPorSemana: new Dictionary<int, (int, int)> { [30] = (ventaH, 0) })
        };

        var cons = ReporteTecnicoSemanalCalculos.ConsolidarProduccion(new[] { Tab("G1", 300), Tab("G2", 200) });

        Assert.Equal(500, cons.Semanas.Single(s => s.Semana == 30).VentaHembras);
    }
}
