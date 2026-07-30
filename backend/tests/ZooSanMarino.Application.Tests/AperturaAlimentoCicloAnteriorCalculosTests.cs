using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Fix v11 (2026-07-29): la apertura de alimento dejaba de ser propia y heredaba el CICLO ANTERIOR
/// del galpón. La ventana previa al encaset (v9) retrocede N días y en Ecuador cae dentro del ciclo
/// que se está vaciando; como las devoluciones por eliminación se excluyen pero los traslados de
/// salida no, la apertura salía NEGATIVA y corría todas las filas de la grilla por igual.
///
/// Caso testigo real: Kilometro 22 / G0036 / lote 98 «2603» (encaset 2026-06-14, primer seguimiento
/// el 16/06). En la ventana [04/06, 15/06] quedaban 4 movimientos del lote 65 «2602» —que cerró el
/// 01/06—: +160, −7.520, −440, −160 = −7.960 kg. La grilla mostraba 3.560 el día 1 en vez de 11.520.
/// </summary>
public class AperturaAlimentoCicloAnteriorCalculosTests
{
    private const int LoteActual   = 98;
    private const int LoteAnterior = 65;
    private const int LoteVecino   = 77;

    private static LoteRegistroHistoricoUnificado Hist(
        string tipoEvento, DateTime fechaOperacion, decimal cantidadKg, int? loteId, long id = 0)
        => new()
        {
            Id = id,
            TipoEvento = tipoEvento,
            OrigenTabla = "origen",
            FechaOperacion = fechaOperacion,
            CantidadKg = cantidadKg,
            CreatedAt = new DateTimeOffset(fechaOperacion, TimeSpan.Zero),
            LoteAveEngordeId = loteId,
        };

    private static SeguimientoDiarioAvesEngorde Seg(long id, DateTime fecha, decimal consumo = 0)
        => new() { Id = id, Fecha = fecha, ConsumoKgHembras = consumo };

    /// <summary>La cola de cierre del ciclo anterior, tal cual está en producción.</summary>
    private static List<LoteRegistroHistoricoUnificado> ColaDelCicloAnterior() => new()
    {
        Hist("INV_INGRESO",          new DateTime(2026, 6,  5),   160m, LoteAnterior, 1),
        Hist("INV_TRASLADO_SALIDA",  new DateTime(2026, 6,  6),  7520m, LoteAnterior, 2),
        Hist("INV_TRASLADO_SALIDA",  new DateTime(2026, 6,  7),   440m, LoteAnterior, 3),
        Hist("INV_TRASLADO_SALIDA",  new DateTime(2026, 6,  7),   160m, LoteAnterior, 4),
    };

    private static readonly DateTime Encaset      = new(2026, 6, 14);
    private static readonly DateTime PrimerSeg    = new(2026, 6, 16);
    private const int DiasPrevios = 10; // ItalcolEcuador → corte 2026-06-04

    // ─────────────────────────────────────────────────────────────────────────
    // Retrocompatibilidad: sin el set, nada cambia (el gate de Panamá)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SinLotesAjenos_ConservaElComportamientoPrevio()
    {
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            ColaDelCicloAnterior(), PrimerSeg, Encaset, DiasPrevios, lotesAjenos: null);

        Assert.Equal(-7960m, apertura);
    }

    [Fact]
    public void SetVacio_EquivaleANull()
    {
        var hist = ColaDelCicloAnterior();

        var conNull  = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, null);
        var conVacio = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, new HashSet<int>());

        Assert.Equal(conNull, conVacio);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // El fix
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CicloAnterior_QuedaFueraDeLaApertura()
    {
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            ColaDelCicloAnterior(), PrimerSeg, Encaset, DiasPrevios,
            new HashSet<int> { LoteAnterior });

        Assert.Equal(0m, apertura);
    }

    [Fact]
    public void CasoTestigo_ElSaldoDelDiaUnoPasaDe3560A11520()
    {
        // 12.000 kg de preiniciador el mismo día del primer seguimiento, con 480 kg de consumo.
        var hist = ColaDelCicloAnterior();
        hist.Add(Hist("INV_INGRESO", PrimerSeg, 12000m, LoteActual, 5));
        var segs = new List<SeguimientoDiarioAvesEngorde> { Seg(1, PrimerSeg, 480m) };

        var (antes, _) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, DiasPrevios, lotesAjenos: null);
        var (despues, _) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        Assert.Equal(3560m,  antes[1]);   // lo que mostraba la grilla
        Assert.Equal(11520m, despues[1]); // lo que espera la operación: 12.000 − 480
    }

    [Fact]
    public void ElDesvioEsConstante_NoAcumulativo()
    {
        var hist = ColaDelCicloAnterior();
        hist.Add(Hist("INV_INGRESO", PrimerSeg, 12000m, LoteActual, 5));
        var segs = new List<SeguimientoDiarioAvesEngorde>
        {
            Seg(1, PrimerSeg,                    480m),
            Seg(2, PrimerSeg.AddDays(1),         640m),
            Seg(3, PrimerSeg.AddDays(2),         880m),
        };

        var (antes, _) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, DiasPrevios, null);
        var (despues, _) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        foreach (var id in new long[] { 1, 2, 3 })
            Assert.Equal(7960m, despues[id] - antes[id]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lo que NO debe tocarse
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void LoteQueCONVIVE_SiguePesandoEnLaApertura()
    {
        // v10: dos lotes solapados comparten bodega (los 4 galpones de Panamá). No son ajenos.
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_INGRESO", new DateTime(2026, 6, 10), 5000m, LoteVecino, 1),
        };

        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        Assert.Equal(5000m, apertura);
    }

    [Fact]
    public void MovimientoSinAtribucion_NuncaEsAjeno()
    {
        // 8 traslados de entrada de Ecuador (35.770 kg) no tienen lote: no se puede perder alimento.
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_TRASLADO_ENTRADA", new DateTime(2026, 6, 10), 1000m, loteId: null, id: 1),
        };

        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        Assert.Equal(1000m, apertura);
    }

    [Fact]
    public void MovimientoDelPropioLote_SiempreCuenta()
    {
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_INGRESO", new DateTime(2026, 6, 12), 3000m, LoteActual, 1),
        };

        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior, LoteVecino });

        Assert.Equal(3000m, apertura);
    }

    [Fact]
    public void DentroDelRangoPropio_ElFiltroNoAplica()
    {
        // CAROLINA G0059: los 2.800 kg de preiniciador del 16/06 quedaron etiquetados con el lote
        // anterior porque el nuevo todavía no tenía seguimiento — pero son del ciclo nuevo.
        // Por eso `lotesAjenos` solo puede usarse ANTES del primer seguimiento.
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_INGRESO", PrimerSeg, 2800m, LoteAnterior, 1),
        };
        var segs = new List<SeguimientoDiarioAvesEngorde> { Seg(1, PrimerSeg, 0m) };

        var (saldos, _) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        Assert.Equal(2800m, saldos[1]);
    }

    [Fact]
    public void LaVentanaSigueMandando_ElFiltroEsOrtogonal()
    {
        // Un movimiento anterior al corte de la ventana no entra, sea de quien sea.
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_INGRESO", new DateTime(2026, 6, 1), 9999m, LoteActual, 1), // corte = 04/06
        };

        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, DiasPrevios, new HashSet<int> { LoteAnterior });

        Assert.Equal(0m, apertura);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ResolverLotesAjenos — espejo del CTE `lotes_ajenos`
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolverLotesAjenos_CicloSucesivoEsAjeno_CicloSolapadoNo()
    {
        var desde = new DateTime(2026, 6, 16);
        var hasta = new DateTime(2026, 7, 28);

        var ajenos = SaldoAlimentoEngordeCalculos.ResolverLotesAjenos(new[]
        {
            // anterior: 17/04 → 01/06, termina antes de que yo empiece
            (LoteAnterior, (DateTime?)new DateTime(2026, 4, 17), (DateTime?)new DateTime(2026, 6, 1)),
            // conviviente: se solapa conmigo
            (LoteVecino,   (DateTime?)new DateTime(2026, 7,  1), (DateTime?)new DateTime(2026, 8, 10)),
            // posterior: empieza después de que yo termino
            (120,          (DateTime?)new DateTime(2026, 8,  1), (DateTime?)new DateTime(2026, 9, 15)),
        }, desde, hasta);

        Assert.Contains(LoteAnterior, ajenos);
        Assert.Contains(120, ajenos);
        Assert.DoesNotContain(LoteVecino, ajenos);
    }

    [Fact]
    public void ResolverLotesAjenos_LoteSinSeguimientoEsAjeno()
    {
        var ajenos = SaldoAlimentoEngordeCalculos.ResolverLotesAjenos(new[]
        {
            (LoteAnterior, (DateTime?)null, (DateTime?)null),
        }, new DateTime(2026, 6, 16), new DateTime(2026, 7, 28));

        Assert.Contains(LoteAnterior, ajenos);
    }

    [Theory]
    // se tocan justo en un día por cada extremo ⇒ conviven
    [InlineData("2026-07-28", "2026-08-30", false)]
    [InlineData("2026-05-01", "2026-06-16", false)]
    // un día de separación por cada extremo ⇒ ajenos
    [InlineData("2026-07-29", "2026-08-30", true)]
    [InlineData("2026-05-01", "2026-06-15", true)]
    public void ResolverLotesAjenos_LosBordesSonInclusivos(string min, string max, bool esperadoAjeno)
    {
        var ajenos = SaldoAlimentoEngordeCalculos.ResolverLotesAjenos(new[]
        {
            (LoteVecino, (DateTime?)DateTime.Parse(min), (DateTime?)DateTime.Parse(max)),
        }, new DateTime(2026, 6, 16), new DateTime(2026, 7, 28));

        Assert.Equal(esperadoAjeno, ajenos.Contains(LoteVecino));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // v12 — la ventana no retrocede más allá del fin del ciclo anterior
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ResolverFinCicloAnterior_TomaElUltimoCicloQueCerroAntesDeQueYoEmpezara()
    {
        var fin = SaldoAlimentoEngordeCalculos.ResolverFinCicloAnterior(new[]
        {
            (10, (DateTime?)new DateTime(2026, 1, 5), (DateTime?)new DateTime(2026, 2, 20)),
            (27, (DateTime?)new DateTime(2026, 1, 16), (DateTime?)new DateTime(2026, 3, 13)), // el más reciente
            (87, (DateTime?)new DateTime(2026, 6, 5), (DateTime?)new DateTime(2026, 7, 15)),  // posterior a mí
        }, desde: new DateTime(2026, 3, 24));

        Assert.Equal(new DateTime(2026, 3, 13), fin);
    }

    [Fact]
    public void ResolverFinCicloAnterior_PrimerCicloDelGalpon_EsNull()
    {
        Assert.Null(SaldoAlimentoEngordeCalculos.ResolverFinCicloAnterior(
            Array.Empty<(int, DateTime?, DateTime?)>(), new DateTime(2026, 3, 24)));
    }

    [Fact]
    public void ResolverFinCicloAnterior_LoteSinSeguimiento_SeIgnora()
    {
        Assert.Null(SaldoAlimentoEngordeCalculos.ResolverFinCicloAnterior(new[]
        {
            (10, (DateTime?)null, (DateTime?)null),
        }, new DateTime(2026, 3, 24)));
    }

    [Fact]
    public void ResolverFinCicloAnterior_CicloQueTodaviaNoCerro_NoCuenta()
    {
        // Termina el mismo día en que yo empiezo ⇒ convivimos, no es «anterior».
        Assert.Null(SaldoAlimentoEngordeCalculos.ResolverFinCicloAnterior(new[]
        {
            (27, (DateTime?)new DateTime(2026, 1, 16), (DateTime?)new DateTime(2026, 3, 24)),
        }, new DateTime(2026, 3, 24)));
    }

    [Fact]
    public void ResolverCorteApertura_GanaElMasTarde()
    {
        var ventana = new DateTime(2026, 3, 12);           // encaset − 10
        var finAnterior = new DateTime(2026, 3, 13);       // el ciclo previo cerró después

        Assert.Equal(new DateTime(2026, 3, 14),
            SaldoAlimentoEngordeCalculos.ResolverCorteApertura(ventana, finAnterior));
    }

    [Fact]
    public void ResolverCorteApertura_SiElCicloAnteriorCerroHaceRato_MandaLaVentana()
    {
        var ventana = new DateTime(2026, 6, 4);
        var finAnterior = new DateTime(2026, 5, 20);

        Assert.Equal(ventana, SaldoAlimentoEngordeCalculos.ResolverCorteApertura(ventana, finAnterior));
    }

    [Fact]
    public void ResolverCorteApertura_SinCicloAnterior_DevuelveLaVentanaTalCual()
    {
        var ventana = new DateTime(2026, 6, 4);
        Assert.Equal(ventana, SaldoAlimentoEngordeCalculos.ResolverCorteApertura(ventana, null));
        Assert.Null(SaldoAlimentoEngordeCalculos.ResolverCorteApertura(null, null));
    }

    [Fact]
    public void CasoSanGuillermo_LaLimpiezaEtiquetadaConELPROPIOLote_LaCazaElCorte()
    {
        // G0033: dos traslados de salida del 13/03 (960 + 4.200 = 5.160 kg) son el vaciado del ciclo
        // 2601 —que cerró ESE MISMO 13/03— pero quedaron con el id del lote NUEVO, así que para
        // `lotesAjenos` son «propios». Solo el corte por fin de ciclo anterior los saca.
        var encaset  = new DateTime(2026, 3, 22);
        var primerSeg = new DateTime(2026, 3, 24);
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_TRASLADO_SALIDA", new DateTime(2026, 3, 13),  960m, LoteActual, 1),
            Hist("INV_TRASLADO_SALIDA", new DateTime(2026, 3, 13), 4200m, LoteActual, 2),
        };

        var soloAjenos = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, primerSeg, encaset, DiasPrevios, new HashSet<int> { LoteAnterior });
        var conCorte = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, primerSeg, encaset, DiasPrevios, new HashSet<int> { LoteAnterior },
            finCicloAnterior: new DateTime(2026, 3, 13));

        Assert.Equal(-5160m, soloAjenos);  // v11 no los ve
        Assert.Equal(0m,     conCorte);    // v12 sí
    }

    [Fact]
    public void ElCortePorCicloAnterior_NoSeLlevaElPreiniciadorLegitimo()
    {
        // El caso que v9 vino a resolver: el preiniciador llega días antes del encaset, mucho después
        // de que cerró el ciclo previo. Tiene que seguir contando.
        var encaset   = new DateTime(2026, 6, 14);
        var primerSeg = new DateTime(2026, 6, 16);
        var hist = new List<LoteRegistroHistoricoUnificado>
        {
            Hist("INV_INGRESO", new DateTime(2026, 6, 12), 12000m, LoteActual, 1),
        };

        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, primerSeg, encaset, DiasPrevios, null, finCicloAnterior: new DateTime(2026, 6, 1));

        Assert.Equal(12000m, apertura);
    }
}
