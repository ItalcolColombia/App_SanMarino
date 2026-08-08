using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Especificación ejecutable del corte temporal de la grilla diaria de engorde (v14): hasta dónde
/// llega `fn_seguimiento_diario_engorde` cuando el galpón encadena ciclos.
/// <para>
/// Caso que lo motivó (ticket de operación Ecuador, 07-ago-2026): granja Kilometro 86, lote 2601 de
/// Galpon-1. Su último seguimiento es el 2026-04-20, nunca se liquidó (quedó `Abierto`) y su saldo de
/// alimento nunca llega a 0 porque el galpón siguió recibiendo alimento para los ciclos 2602 y 2603.
/// La grilla llegaba hasta el 2026-08-03 con el saldo inflado de 1.600 kg a 206.450 kg: los ingresos
/// de julio existen y son correctos, pero son del lote 2603.
/// </para>
/// </summary>
public class CorteCicloEngordeCalculosTests
{
    private static DateTime D(int y, int m, int d) => new(y, m, d);

    // Topología real del galpón G0039 de Kilometro 86 (los tres ciclos, uno detrás del otro).
    private static readonly (int LoteId, DateTime? SegMin, DateTime? SegMax)[] CiclosG0039 =
    [
        (72,  D(2026, 4, 24), D(2026, 6,  6)),   // 2602
        (104, D(2026, 6, 26), D(2026, 8,  6))    // 2603
    ];

    // ─── ResolverInicioCicloSiguiente ────────────────────────────────────────

    [Fact]
    public void CasoDelTicket_ElCicloSiguienteEsElMasTempranoPosteriorAMiUltimoSeguimiento()
    {
        var inicio = SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente(CiclosG0039, D(2026, 4, 20));

        Assert.Equal(D(2026, 4, 24), inicio);   // el 2602, no el 2603
    }

    [Fact]
    public void UltimoCicloDelGalpon_NoTieneSucesor()
    {
        // El propio 2603: nadie arranca después de él.
        var inicio = SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente(
            [(2, D(2026, 2, 13), D(2026, 4, 20)), (72, D(2026, 4, 24), D(2026, 6, 6))],
            D(2026, 8, 6));

        Assert.Null(inicio);
    }

    [Fact]
    public void LoteQueCONVIVE_NoEsUnCicloSiguiente()
    {
        // Dos lotes solapados en el mismo galpón (caso v10, inventario compartido): el otro arranca
        // ANTES de que yo termine, así que no corta nada.
        var inicio = SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente(
            [(169, D(2026, 6, 10), D(2026, 7, 20))],
            D(2026, 7, 15));

        Assert.Null(inicio);
    }

    [Fact]
    public void LoteSinSeguimiento_NoSeCorta()
    {
        var inicio = SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente(CiclosG0039, null);

        Assert.Null(inicio);
    }

    [Fact]
    public void CicloVecinoSinSeguimientoCargado_SeIgnora()
    {
        // Un lote creado pero todavía sin ninguna fila diaria no puede reclamar el galpón.
        var inicio = SaldoAlimentoEngordeCalculos.ResolverInicioCicloSiguiente(
            [(200, null, null), (72, D(2026, 4, 24), D(2026, 6, 6))],
            D(2026, 4, 20));

        Assert.Equal(D(2026, 4, 24), inicio);
    }

    // ─── ResolverFechaMaxGrilla ──────────────────────────────────────────────

    [Fact]
    public void CasoDelTicket_LaGrillaCortaElDiaAnteriorAlCicloSiguiente()
    {
        // Abierto, sin cierre por saldo 0: antes de v14 esto era null (sin tope).
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(
            cierrePorSaldoCero: null,
            loteCerrado: false,
            ultimoSeguimiento: D(2026, 4, 20),
            inicioCicloSiguiente: D(2026, 4, 24));

        Assert.Equal(D(2026, 4, 23), hasta);
    }

    [Fact]
    public void SinCicloSiguiente_ElCorteEsExactamenteElDeAntesDeV14()
    {
        Assert.Null(SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(null, false, D(2026, 4, 20), null));
        Assert.Equal(D(2026, 4, 20),
            SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(null, true, D(2026, 4, 20), null));
        Assert.Equal(D(2026, 4, 22),
            SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(D(2026, 4, 22), false, D(2026, 4, 20), null));
    }

    [Fact]
    public void CierrePorSaldoCeroMasTemprano_Gana()
    {
        // El lote se vació de alimento antes de que arrancara el ciclo siguiente: manda el cierre real.
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(
            cierrePorSaldoCero: D(2026, 4, 21),
            loteCerrado: false,
            ultimoSeguimiento: D(2026, 4, 20),
            inicioCicloSiguiente: D(2026, 4, 24));

        Assert.Equal(D(2026, 4, 21), hasta);
    }

    [Fact]
    public void CierrePorSaldoCeroYaDentroDelCicloSiguiente_LoCortaElCicloSiguiente()
    {
        // El saldo tocó 0 recién en mayo, pero el galpón ya era del ciclo siguiente desde el 24/04.
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(
            cierrePorSaldoCero: D(2026, 5, 1),
            loteCerrado: false,
            ultimoSeguimiento: D(2026, 4, 20),
            inicioCicloSiguiente: D(2026, 4, 24));

        Assert.Equal(D(2026, 4, 23), hasta);
    }

    [Fact]
    public void LoteCerrado_ConservaSuUltimoSeguimientoAunqueHayaCicloSiguiente()
    {
        // Es el caso de los galpones 3 y 4 del mismo lote 2601, que sí se cerraron en abril.
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(
            cierrePorSaldoCero: null,
            loteCerrado: true,
            ultimoSeguimiento: D(2026, 4, 20),
            inicioCicloSiguiente: D(2026, 5, 1));

        Assert.Equal(D(2026, 4, 20), hasta);
    }

    [Fact]
    public void CicloSiguienteQueArrancaAlDiaSiguiente_DejaLaUltimaFilaDeSeguimiento()
    {
        // Borde: relevo sin un solo día de hueco. La grilla llega justo hasta mi último seguimiento.
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(
            cierrePorSaldoCero: null,
            loteCerrado: false,
            ultimoSeguimiento: D(2026, 4, 20),
            inicioCicloSiguiente: D(2026, 4, 21));

        Assert.Equal(D(2026, 4, 20), hasta);
    }

    [Fact]
    public void LoteGenuinamenteActivo_SigueSinTope()
    {
        var hasta = SaldoAlimentoEngordeCalculos.ResolverFechaMaxGrilla(null, false, D(2026, 8, 6), null);

        Assert.Null(hasta);
    }
}
