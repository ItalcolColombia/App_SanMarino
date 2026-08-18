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
///   <item>MARCA <c>para_proximo_ciclo</c>: ⛔ RETIRADA en v16a (18-ago-2026). Los casos de la marca
///   que quedan abajo son el contrato de su <b>inercia</b>: el booleano se guarda pero no lo interpreta
///   nadie, ni la fn ni este cálculo. Por qué, en la cabecera de la sección (B).</item>
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

    // --- (B) v16a: la marca «para el próximo ciclo» es INERTE -----------------------------------
    //
    // Hasta v15 la marca movía kg de una pantalla a otra: entraba a la apertura saltando los cortes
    // de v11/v12 y salía de la fila diaria del ciclo que la contenía. Medido sobre el dump local, esa
    // semántica rompe la conservación: marcar los 2.371 movimientos de alimento reales deja 24 filas
    // de la tabla diaria sin ninguna pantalla, mueve 1.733 saldos (peor caso 193.701,7 kg), lleva las
    // filas en negativo de 97 a 1.160 y el cuadre de 8 a 58 galpones descuadrados.
    //
    // La Fase A del plan `v16_engorde_atribucion_persistida_plan.md` la apaga: el booleano se guarda,
    // pero NADIE lo interpreta. Estos casos son el contrato de esa inercia — cada uno compara el mismo
    // historial con la marca y sin ella y exige el MISMO número. La atribución vuelve en la Fase B,
    // como hecho persistido con dueño único, no como predicado recalculado en cada lectura.

    [Fact]
    public void Marca_Apertura_DaExactamenteLoMismoQueSinMarca()
    {
        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(sinMarca, conMarca);
        Assert.Equal(7000m, conMarca);
    }

    [Fact]
    public void Marca_Documentos_DanExactamenteLosMismosQueSinMarca()
    {
        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(), PrimerSeg, Encaset, diasAlimentoPrevio: 10);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeDocumentosAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(sinMarca, conMarca);
        // FAC-0014 (14-ago) queda fuera de la ventana de 10 días con o sin marca: v16a no la rescata.
        Assert.Equal("FAC-0015, FAC-0026", conMarca);
    }

    [Fact]
    public void Marca_MovimientoFueraDeLaVentana_SigueFuera()
    {
        // v15 usaba la marca para rescatar los 3.000 del 14-ago (un día fuera de la ventana) y los
        // subía a 10.000. v16a no: el corte por fecha vuelve a ser la única regla.
        var apertura = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            HistorialDelPlan(marcarEl14: true), PrimerSeg, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(7000m, apertura);
    }

    [Fact]
    public void Marca_MovimientoDeCicloAjeno_SigueSiendoAjeno_ElCorteV11Manda()
    {
        // Galpón encadenado: el trigger etiquetó la llegada con el lote VIEJO (99), que v11 descarta.
        // En v15 la marca ganaba a la heurística; en v16a no la mira nadie.
        var ajenos = new HashSet<int> { 99 };
        List<LoteRegistroHistoricoUnificado> sin =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, loteAveEngordeId: 99)];
        List<LoteRegistroHistoricoUnificado> con =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, numeroDocumento: "FAC-X",
                  loteAveEngordeId: 99, paraProximoCiclo: true)];

        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            sin, PrimerSeg, Encaset, diasAlimentoPrevio: 10, lotesAjenos: ajenos);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            con, PrimerSeg, Encaset, diasAlimentoPrevio: 10, lotesAjenos: ajenos);

        Assert.Equal(sinMarca, conMarca);
        Assert.Equal(0m, conMarca);
    }

    [Fact]
    public void Marca_GalponEncadenado_ElCorteDelCicloAnteriorTambienManda()
    {
        var finCicloAnterior = new DateTime(2026, 8, 22);
        List<LoteRegistroHistoricoUnificado> sin =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m)];
        List<LoteRegistroHistoricoUnificado> con =
            [Hist("INV_INGRESO", new DateTime(2026, 8, 20), 4000m, numeroDocumento: "FAC-X", paraProximoCiclo: true)];

        var sinMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            sin, PrimerSeg, Encaset, diasAlimentoPrevio: 10, finCicloAnterior: finCicloAnterior);
        var conMarca = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            con, PrimerSeg, Encaset, diasAlimentoPrevio: 10, finCicloAnterior: finCicloAnterior);

        Assert.Equal(sinMarca, conMarca);
        Assert.Equal(0m, conMarca);
    }

    // --- (B) Efecto sobre el saldo por seguimiento (la fila diaria completa) ---------------------

    [Fact]
    public void SaldoPorSeguimiento_CasoDelPlan_AperturaMasDiasDaElMismo6800()
    {
        // Reproduce §8 [2] del plan de v15 y la simulación en BD: 5.000 + 2.000 - 200 = 6.800.
        var (porSeg, final) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            HistorialDelPlan(), [Seg(1, PrimerSeg, 200m)], Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(6800m, porSeg[1]);
        Assert.Equal(6800m, final);
    }

    [Fact]
    public void SaldoPorSeguimiento_MarcadoDentroDelRango_NoSaleDelSaldoDeEsteCiclo()
    {
        // Éste es el caso que rompía la conservación: v15 restaba los 4.000 del saldo de este ciclo
        // (8.700 -> 4.700) confiando en que la apertura del ciclo destino los tomaría. Cuando el
        // destino convive con el cedente, o todavía no existe, nadie los toma y los kg desaparecen de
        // toda pantalla. v16a los deja donde están: marcado o no, el número es el mismo.
        List<LoteRegistroHistoricoUnificado> sin =
        [
            Hist("INV_INGRESO", new DateTime(2026, 8, 15), 5000m, numeroDocumento: "FAC-0015"),
            Hist("INV_INGRESO", new DateTime(2026, 9, 5), 4000m, numeroDocumento: "FAC-0905"),
        ];
        List<LoteRegistroHistoricoUnificado> con =
        [
            Hist("INV_INGRESO", new DateTime(2026, 8, 15), 5000m, numeroDocumento: "FAC-0015"),
            Hist("INV_INGRESO", new DateTime(2026, 9, 5), 4000m, numeroDocumento: "FAC-0905", paraProximoCiclo: true),
        ];
        List<SeguimientoDiarioAvesEngorde> segs = [Seg(1, PrimerSeg, 200m), Seg(2, new DateTime(2026, 9, 10), 100m)];

        var sinMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            sin, segs, Encaset, diasAlimentoPrevio: 10);
        var conMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            con, segs, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(sinMarca.SaldoFinal, conMarca.SaldoFinal);
        Assert.Equal(8700m, conMarca.SaldoFinal);   // 5.000 + 4.000 - 200 - 100
    }

    [Fact]
    public void SaldoPorSeguimiento_MarcarTodoElHistorial_NoMueveNiUnKilo()
    {
        // Generalización del caso anterior: es el equivalente en C# del A/B que el gate corre en BD
        // (2.371 movimientos marcados => EXCEPT ALL 0 y 0 sobre 6.429 filas).
        List<SeguimientoDiarioAvesEngorde> segs = [Seg(1, PrimerSeg, 200m), Seg(2, new DateTime(2026, 8, 28), 150m)];
        var todoMarcado = HistorialDelPlan()
            .Select(h => Hist(h.TipoEvento, h.FechaOperacion, h.CantidadKg,
                              numeroDocumento: h.NumeroDocumento, paraProximoCiclo: true))
            .ToList();

        var sinMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            HistorialDelPlan(), segs, Encaset, diasAlimentoPrevio: 10);
        var conMarca = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            todoMarcado, segs, Encaset, diasAlimentoPrevio: 10);

        Assert.Equal(sinMarca.SaldoFinal, conMarca.SaldoFinal);
        Assert.Equal(sinMarca.SaldoPorSegId[1], conMarca.SaldoPorSegId[1]);
        Assert.Equal(sinMarca.SaldoPorSegId[2], conMarca.SaldoPorSegId[2]);
    }
}
