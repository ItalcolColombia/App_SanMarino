using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Convergencia LEVANTE a Feature-13 (Fase 3). Invariante DURA: el saldo físico de aves.
/// El reparto de % selección cambia A PROPÓSITO (el traslado deja de contarse como selección).
/// </summary>
public class SaldoLevanteCalculosTests
{
    // ── 1. Neutralidad: lote SIN traslados/ventas ⇒ idéntico al baseline ────────
    [Fact]
    public void SaldoFisico_SinTraslado_IgualAlBaselineMortSel()
    {
        // Baseline (comportamiento previo para un lote sin traslado ni error):
        //   saldo = base − mortCaja − mort − sel
        const int baseAves = 10_000, mortCaja = 50, mort = 300, sel = 120;
        var baseline = baseAves - mortCaja - mort - sel;

        var saldo = SaldoLevanteCalculos.SaldoFisico(
            baseAves, mortCaja, mort, sel, errorSexaje: 0, trasladoIngreso: 0, trasladoSalida: 0);

        Assert.Equal(baseline, saldo); // sin traslado ni err ⇒ nada cambia
    }

    [Fact]
    public void PorcSeleccion_SinTraslado_Identico()
    {
        // 120 selección genuina / 10000 = 1.2 % (igual que antes; no hay traslado que lo infle)
        Assert.Equal(1.2m, SaldoLevanteCalculos.PorcSeleccion(120, 10_000));
    }

    // ── 2. Traslado: corrige el SIGNO (el hack ±Sel lo sumaba) ───────────────────
    [Fact]
    public void SaldoFisico_TrasladoSalida_RestaDelSaldo_NoSuma()
    {
        const int baseAves = 10_000, mort = 300, sel = 0; // sel genuina 0: fue un traslado
        const int trasladoSalida = 800;

        // Feature-13 (correcto): el traslado RESTA.
        var nuevo = SaldoLevanteCalculos.SaldoFisico(
            baseAves, 0, mort, sel, 0, trasladoIngreso: 0, trasladoSalida: trasladoSalida);
        Assert.Equal(10_000 - 300 - 800, nuevo); // 8900

        // Hack legacy ±Sel: codificaba la salida como sel = −800 ⇒ saldo = base − (−800) = +800 (INVERTIDO).
        var hackInvertido = SaldoLevanteCalculos.SaldoFisico(
            baseAves, 0, mort, seleccion: -trasladoSalida, errorSexaje: 0, trasladoIngreso: 0, trasladoSalida: 0);
        Assert.Equal(10_000 - 300 + 800, hackInvertido); // 10500 (lo que hacía mal)

        // La convergencia baja el saldo en 2 × traslado respecto del hack (fija el signo).
        Assert.Equal(1_600, hackInvertido - nuevo);
    }

    [Fact]
    public void SaldoFisico_TrasladoIngreso_SumaAlDestino()
    {
        var destino = SaldoLevanteCalculos.SaldoFisico(
            baseAves: 5_000, mortCaja: 0, mortalidad: 0, seleccion: 0, errorSexaje: 0,
            trasladoIngreso: 800, trasladoSalida: 0);
        Assert.Equal(5_800, destino);
    }

    // ── 3. % selección BAJA para lotes con traslado (deja de inflarse) ──────────
    [Fact]
    public void PorcSeleccion_ConTraslado_UsaSoloSeleccionGenuina()
    {
        // Antes (hack): sel reportada = genuina(120) + traslado(800) = 920 ⇒ 9.2 %
        var viejo = SaldoLevanteCalculos.PorcSeleccion(120 + 800, 10_000);
        // Ahora: solo genuina 120 ⇒ 1.2 % (el traslado va por su columna)
        var nuevo = SaldoLevanteCalculos.PorcSeleccion(120, 10_000);

        Assert.Equal(9.2m, viejo);
        Assert.Equal(1.2m, nuevo);
        Assert.True(nuevo < viejo); // baja a propósito
    }

    // ── 4. Venta: resta UNA sola vez (vía sacrificadas), sin doble conteo ───────
    [Fact]
    public void AvesActualesEcuador_Venta_RestaUnaVez()
    {
        // ini 10000, mort 300, sel genuina 120, venta (sacrificadas) 500, sin traslado.
        var aves = SaldoLevanteCalculos.AvesActualesEcuador(
            iniciales: 10_000, mortalidad: 300, seleccion: 120, sacrificadas: 500, trasladoNeto: 0);
        Assert.Equal(10_000 - 300 - 120 - 500, aves); // 9080, una sola resta de la venta
    }

    [Fact]
    public void AvesActualesEcuador_TrasladoNeto_Resta()
    {
        var neto = SaldoLevanteCalculos.TrasladoNeto(trasladoSalida: 800, trasladoIngreso: 200);
        Assert.Equal(600, neto);

        var aves = SaldoLevanteCalculos.AvesActualesEcuador(
            iniciales: 10_000, mortalidad: 300, seleccion: 120, sacrificadas: 0, trasladoNeto: neto);
        Assert.Equal(10_000 - 300 - 120 - 600, aves); // 8980
    }

    // ── 5. Cross-reader: mismo saldo entre fn/GetMortalidadResumen e IndicadorEcuador ──
    [Fact]
    public void CrossReader_MismaFormula_MismoSaldo()
    {
        // Fixture donde las tres fuentes coinciden: mortCaja = 0, base = iniciales, err = 0, sin venta.
        const int baseAves = 12_000, mort = 400, selGenuina = 150, trasSalida = 900, trasIngreso = 0;

        var saldoFisico = SaldoLevanteCalculos.SaldoFisico(
            baseAves, mortCaja: 0, mortalidad: mort, seleccion: selGenuina,
            errorSexaje: 0, trasladoIngreso: trasIngreso, trasladoSalida: trasSalida);

        var avesEcuador = SaldoLevanteCalculos.AvesActualesEcuador(
            iniciales: baseAves, mortalidad: mort, seleccion: selGenuina, sacrificadas: 0,
            trasladoNeto: SaldoLevanteCalculos.TrasladoNeto(trasSalida, trasIngreso));

        Assert.Equal(saldoFisico, avesEcuador); // 12000 − 400 − 150 − 900 = 10550
        Assert.Equal(10_550, saldoFisico);
    }

    // ── 6. Nunca negativo ───────────────────────────────────────────────────────
    [Fact]
    public void SaldoFisico_NuncaNegativo()
    {
        var saldo = SaldoLevanteCalculos.SaldoFisico(
            baseAves: 100, mortCaja: 0, mortalidad: 0, seleccion: 0, errorSexaje: 0,
            trasladoIngreso: 0, trasladoSalida: 5_000);
        Assert.Equal(0, saldo);
    }
}
