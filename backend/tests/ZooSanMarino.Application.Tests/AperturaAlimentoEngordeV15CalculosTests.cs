using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato ejecutable de la v15 de <c>fn_seguimiento_diario_engorde</c> (plan
/// <c>fase_de_desarrollo/ingreso_alimento_fecha_real_ingreso_inicial_ciclo_plan.md</c>, D1 y D2).
/// <para>
/// El alimento llega a la granja 2-7 días ANTES del encasetamiento. Hasta ahora había que fechar el
/// ingreso el primer día de consumo para que la tabla diaria «cuadrara», y así se perdía la fecha
/// real que necesita contabilidad. La v15 resuelve las dos mitades del problema:
/// </para>
/// <list type="number">
///   <item>APERTURA VISIBLE: los kg y los documentos que la fn ya absorbía en la apertura desde v9
///   dejan de ser un escalar interno (columnas <c>apertura_alimento_kg</c> / <c>apertura_documentos</c>).</item>
///   <item>MARCA <c>para_proximo_ciclo</c>: atribución explícita para los galpones encadenados, donde
///   la llegada real cae dentro del ciclo anterior y la fecha sola no puede decidir.</item>
/// </list>
/// <para>
/// Regla del repo «una sola fórmula por número»: estos casos son el contrato que la fn SQL debe
/// cumplir. Los mismos escenarios se corrieron contra la BD local con dump tipo prod (transacción
/// con ROLLBACK) y dan los mismos números.
/// </para>
/// </summary>
public class AperturaAlimentoEngordeV15CalculosTests
{
    private static LoteRegistroHistoricoUnificado Hist(
        string tipoEvento,
        DateTime fechaOperacion,
        decimal? cantidadKg,
        string? numeroDocumento = null,
        string? referencia = null,
        int? loteAveEngordeId = null,
        bool paraProximoCiclo = false,
        bool anulado = false)
        => new()
        {
            TipoEvento = tipoEvento,
            OrigenTabla = "origen",
            FechaOperacion = fechaOperacion,
            CantidadKg = cantidadKg,
            CreatedAt = new DateTimeOffset(fechaOperacion, TimeSpan.Zero),
            NumeroDocumento = numeroDocumento,
            Referencia = referencia,
            LoteAveEngordeId = loteAveEngordeId,
            ParaProximoCiclo = paraProximoCiclo,
            Anulado = anulado,
        };

    private static SeguimientoDiarioAvesEngorde Seg(long id, DateTime fecha, decimal consumo)
        => new() { Id = id, Fecha = fecha, ConsumoKgHembras = consumo };

    // Escenario canónico del plan §8: encaset 2026-08-25 (ventana default 10 ⇒ corte 2026-08-15),
    // llegadas reales el 14 (3.000, un día FUERA de la ventana), el 15 (5.000, el caso del usuario)
    // y el 26 (2.000, entre encaset y primer registro). Primer seguimiento el 27 con consumo 200.
    private static readonly DateTime Encaset = new(2026, 8, 25);
    private static readonly DateTime PrimerSeg = new(2026, 8, 27);

    private static List<LoteRegistroHistoricoUnificado> HistorialDelPlan(bool marcarEl14 = false) =>
    [
        Hist("INV_INGRESO", new DateTime(2026, 8, 14), 3000m, numeroDocumento: "FAC-0014", paraProximoCiclo: marcarEl14),
        Hist("INV_INGRESO", new DateTime(2026, 8, 15), 5000m, numeroDocumento: "FAC-0015"),
        Hist("INV_INGRESO", new DateTime(2026, 8, 26), 2000m, numeroDocumento: "FAC-0026"),
    ];

    // ─── (A) Apertura visible: los kg y los documentos del «Ingreso inicial del ciclo» ───────────

    [Fact]
    public void Apertura_CasoLlegaEl15EncasetaEl25_EsLaQueYaAlimentabaElSaldo()
    {
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        // 5.000 (15-ago, justo dentro de la ventana) + 2.000 (26-ago, antes del primer seguimiento).
        // Los 3.000 del 14-ago quedan fuera por un solo día: el default 10 es filo de navaja.
        Assert.Equal(7000m, apertura);
    }

    [Fact]
    public void Apertura_Documentos_MuestranDeDondeSaleElSaldoDelDiaUno()
    {
        var docs = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        // Mismos movimientos que los 7.000 kg de arriba: kg y documentos hablan del mismo alimento.
        Assert.Equal("FAC-0015, FAC-0026", docs);
    }

    [Fact]
    public void Apertura_Documentos_SinMovimientosConDocumento_DevuelveNullNoCadenaVacia()
    {
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 1000m, numeroDocumento: "   ")];

        var docs = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        Assert.Null(docs);
    }

    [Fact]
    public void Apertura_Documentos_DeduplicaYOrdena()
    {
        List<LoteRegistroHistoricoUnificado> hist =
        [
            Hist("INV_INGRESO", new DateTime(2026, 8, 20), 100m, numeroDocumento: "LLEG-02"),
            Hist("INV_INGRESO", new DateTime(2026, 8, 19), 100m, numeroDocumento: "LLEG-01"),
            Hist("INV_INGRESO", new DateTime(2026, 8, 19), 100m, numeroDocumento: "LLEG-01"),
        ];

        var docs = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        // Caso real DAYLAND G0464 (Panamá): cuatro llegadas con dos documentos.
        Assert.Equal("LLEG-01, LLEG-02", docs);
    }

    [Fact]
    public void Apertura_Documentos_MovimientoDeCeroKgIgualAportaSuDocumento()
    {
        // La fn arma `apertura_docs` sobre `apert_mov`, que filtra por tipo_evento y no por kg.
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 0m, numeroDocumento: "FAC-CERO")];

        Assert.Equal("FAC-CERO", SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10));
    }

    [Fact]
    public void Apertura_Documentos_NumeroDocumentoVacioNoCaeALaReferencia()
    {
        // Espejo de COALESCE(numero_documento, referencia, ''): '' no es NULL, así que gana el vacío.
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 100m, numeroDocumento: "", referencia: "REF-X")];

        Assert.Null(SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10));
    }

    [Fact]
    public void Apertura_Documentos_SinNumeroDeDocumentoUsaLaReferencia()
    {
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 100m, referencia: "LLEG-01")];

        Assert.Equal("LLEG-01", SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10));
    }

    // ─── (B) Marca «para el próximo ciclo»: sin marca NADA cambia ────────────────────────────────

    [Fact]
    public void SinMarca_AperturaIdenticaAV14_AunqueSePasenLosCiclosDelGalpon()
    {
        var ciclos = new[] { (LoteId: 7, SegMin: (DateTime?)new DateTime(2026, 6, 1), SegMax: (DateTime?)new DateTime(2026, 7, 20)) };

        var sinCiclos = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10);
        var conCiclos = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10, ciclosDelGalpon: ciclos);

        Assert.Equal(sinCiclos, conCiclos);
        Assert.Equal(7000m, conCiclos);
    }

    [Fact]
    public void ConMarca_PeroSinCiclosDelGalpon_ElDefaultConservaElComportamientoV14()
    {
        // El parámetro nuevo es opt-in: quien no lo pasa sigue viendo exactamente la v14.
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(7000m, apertura);
    }

    [Fact]
    public void ConMarca_MovimientoFueraDeLaVentana_EntraALaApertura()
    {
        // Los 3.000 del 14-ago están un día fuera de la ventana de 10: sin marca se pierden del
        // reporte aunque sigan en el stock («el sistema se comió alimento»). La marca los rescata.
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10,
            ciclosDelGalpon: []);

        Assert.Equal(10000m, apertura);
    }

    [Fact]
    public void ConMarca_ElDocumentoDelMovimientoRescatadoTambienSeMuestra()
    {
        var docs = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10,
            ciclosDelGalpon: []);

        Assert.Equal("FAC-0014, FAC-0015, FAC-0026", docs);
    }

    [Fact]
    public void ConMarca_MovimientoDeCicloAjeno_EntraIgual_LaMarcaGanaAlCorteV11()
    {
        // Galpón encadenado: el trigger etiquetó la llegada con el lote VIEJO (99), que v11 descarta.
        // La marca la pone una persona, así que sustituye a la heurística.
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, numeroDocumento: "FAC-X",
                  loteAveEngordeId: 99, paraProximoCiclo: true)];
        var ajenos = new HashSet<int> { 99 };

        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, loteAveEngordeId: 99)],
            PrimerSeg, Encaset, diasAlimentoPrevio: 10, lotesAjenos: ajenos, ciclosDelGalpon: []);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10, lotesAjenos: ajenos, ciclosDelGalpon: []);

        Assert.Equal(0m, sinMarca);
        Assert.Equal(4000m, conMarca);
    }

    [Fact]
    public void ConMarca_GalponEncadenado_ElCorteDelCicloAnteriorTampocoLaFrena()
    {
        // 28 de 75 ciclos encadenados de Ecuador tienen menos de 10 días de gap: la ventana se recorta
        // a `fin_ciclo_anterior + 1` y la llegada real queda del lado del ciclo viejo.
        var finCicloAnterior = new DateTime(2026, 8, 22);
        List<LoteRegistroHistoricoUnificado> hist =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, numeroDocumento: "FAC-X", paraProximoCiclo: true)];

        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m)],
            PrimerSeg, Encaset, diasAlimentoPrevio: 10, finCicloAnterior: finCicloAnterior, ciclosDelGalpon: []);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, PrimerSeg, Encaset, diasAlimentoPrevio: 10, finCicloAnterior: finCicloAnterior,
            ciclosDelGalpon: []);

        Assert.Equal(0m, sinMarca);
        Assert.Equal(4000m, conMarca);
    }

    // ─── (B) La guarda: una marca vieja no se cuela dos ciclos después ───────────────────────────

    [Fact]
    public void EntraPorMarca_SinCicloIntermedio_LoAbsorbeEsteCiclo()
    {
        var ciclos = new[] { (LoteId: 7, SegMin: (DateTime?)new DateTime(2026, 6, 1), SegMax: (DateTime?)new DateTime(2026, 7, 20)) };

        Assert.True(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: true, fechaMovimiento: new DateTime(2026, 8, 14), miPrimerSeguimiento: PrimerSeg, ciclos));
    }

    [Fact]
    public void EntraPorMarca_OtroCicloArrancoEnElMedio_LaMarcaEraDeAquel()
    {
        // El lote 8 empezó su seguimiento el 18-ago, entre la llegada (14-ago) y mi día 1 (27-ago).
        var ciclos = new[] { (LoteId: 8, SegMin: (DateTime?)new DateTime(2026, 8, 18), SegMax: (DateTime?)new DateTime(2026, 8, 25)) };

        Assert.False(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: true, fechaMovimiento: new DateTime(2026, 8, 14), miPrimerSeguimiento: PrimerSeg, ciclos));
    }

    [Fact]
    public void EntraPorMarca_LoteSinSeguimientoEnElGalpon_NoBloquea()
    {
        var ciclos = new[] { (LoteId: 8, SegMin: (DateTime?)null, SegMax: (DateTime?)null) };

        Assert.True(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: true, fechaMovimiento: new DateTime(2026, 8, 14), miPrimerSeguimiento: PrimerSeg, ciclos));
    }

    [Fact]
    public void EntraPorMarca_OtroCicloQueArrancoElMISMODiaDelMovimiento_NoBloquea()
    {
        // La comparación es estricta por los dos lados: el ciclo que ya estaba corriendo cuando llegó
        // el alimento no es «el próximo».
        var ciclos = new[] { (LoteId: 8, SegMin: (DateTime?)new DateTime(2026, 8, 14), SegMax: (DateTime?)new DateTime(2026, 8, 25)) };

        Assert.True(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: true, fechaMovimiento: new DateTime(2026, 8, 14), miPrimerSeguimiento: PrimerSeg, ciclos));
    }

    [Fact]
    public void EntraPorMarca_MovimientoPosteriorAMiDiaUno_NoEsApertura()
    {
        Assert.False(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: true, fechaMovimiento: new DateTime(2026, 9, 5), miPrimerSeguimiento: PrimerSeg, []));
    }

    [Fact]
    public void EntraPorMarca_SinMarca_SiempreFalse()
    {
        Assert.False(SaldoAlimentoEngordeCalculos.EntraPorMarcaProximoCiclo(
            marcado: false, fechaMovimiento: new DateTime(2026, 8, 14), miPrimerSeguimiento: PrimerSeg, []));
    }

    // ─── (B) El marcado sale de la fila diaria del ciclo que lo contiene ─────────────────────────

    [Fact]
    public void ExcluidoDeFilaDiaria_ConSeguimiento_ElMarcadoNoEsKgDeEsteCiclo()
        => Assert.True(SaldoAlimentoEngordeCalculos.ExcluidoDeFilaDiariaPorMarca(true, PrimerSeg));

    [Fact]
    public void ExcluidoDeFilaDiaria_LoteSinSeguimientoTodavia_ElMarcadoSIGUEVISIBLE()
        // Excepción deliberada: sin primer seguimiento no hay apertura donde absorberlo, y los kg no
        // pueden desaparecer de la pantalla — ese parpadeo es lo que empuja a re-fechar al día 1.
        => Assert.False(SaldoAlimentoEngordeCalculos.ExcluidoDeFilaDiariaPorMarca(true, null));

    [Fact]
    public void ExcluidoDeFilaDiaria_SinMarca_NuncaSeExcluye()
        => Assert.False(SaldoAlimentoEngordeCalculos.ExcluidoDeFilaDiariaPorMarca(false, PrimerSeg));

    // ─── (B) Efecto sobre el saldo por seguimiento (la fila diaria completa) ─────────────────────

    [Fact]
    public void SaldoPorSeguimiento_CasoDelPlan_AperturaMasDiasDaElMismo6800()
    {
        // Reproduce §8 [2] del plan y la simulación en BD: 5.000 + 2.000 − 200 = 6.800.
        var (porSeg, final) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            HistorialDelPlan(), [Seg(1, PrimerSeg, 200m)], Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(6800m, porSeg[1]);
        Assert.Equal(6800m, final);
    }

    [Fact]
    public void SaldoPorSeguimiento_MarcadoDentroDelRango_SaleDelSaldoDeEsteCiclo()
    {
        List<LoteRegistroHistoricoUnificado> hist =
        [
            Hist("INV_INGRESO", new DateTime(2026, 8, 15), 5000m, numeroDocumento: "FAC-0015"),
            Hist("INV_INGRESO", new DateTime(2026, 9, 5), 4000m, numeroDocumento: "FAC-0905", paraProximoCiclo: true),
        ];
        List<SeguimientoDiarioAvesEngorde> segs = [Seg(1, PrimerSeg, 200m), Seg(2, new DateTime(2026, 9, 10), 100m)];

        var conMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, diasAlimentoPrevio: 10, ciclosDelGalpon: []);
        var ignorandoLaMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            hist, segs, Encaset, diasAlimentoPrevio: 10);

        // Sin leer la marca los 4.000 del 05-sep siguen sumando (camino v14).
        Assert.Equal(8700m, ignorandoLaMarca.SaldoFinal);
        // Leyéndola, esos kg son del ciclo siguiente: 5.000 − 200 − 100.
        Assert.Equal(4700m, conMarca.SaldoFinal);
    }

    [Fact]
    public void SaldoPorSeguimiento_SinNingunaMarca_ConYSinCiclosDelGalponEsElMismoNumero()
    {
        List<SeguimientoDiarioAvesEngorde> segs = [Seg(1, PrimerSeg, 200m)];

        var a = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            HistorialDelPlan(), segs, Encaset, diasAlimentoPrevio: 10);
        var b = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            HistorialDelPlan(), segs, Encaset, diasAlimentoPrevio: 10, ciclosDelGalpon: []);

        Assert.Equal(a.SaldoFinal, b.SaldoFinal);
    }
}
