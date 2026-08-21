using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.AjusteEncasetamientoCalculos;
using MaestroAves = ZooSanMarino.Application.Calculos.RetiroAvesEngordeCalculos.MaestroAves;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Ajuste de encasetamiento: corregir las aves con que arrancó un lote que ya tiene seguimiento.
/// <para>
/// Los dos invariantes que estas pruebas fijan y que el service NO puede romper:
/// (1) el saldo vivo se corre por el DELTA, nunca se pisa — las bajas ya descontadas sobreviven;
/// (2) bajar el inicial por debajo de lo consumido se rechaza ANTES de escribir, nombrando el día.
/// </para>
/// </summary>
public class AjusteEncasetamientoCalculosTests
{
    private static readonly DateTime D1 = new(2026, 7, 1);
    private static readonly DateTime D2 = new(2026, 7, 2);
    private static readonly DateTime D3 = new(2026, 7, 3);

    // ── 1 · Delta cero ⇒ el service no escribe nada ──────────────────────────

    [Fact]
    public void Delta_cero_cuando_el_inicial_no_cambia()
    {
        var vigente = new MaestroAves(10_000, 2_000, 0);
        var delta = Calcular(vigente, new MaestroAves(10_000, 2_000, 0));

        Assert.True(delta.EsCero);
        Assert.True(SinCambio(delta));
    }

    [Fact]
    public void Delta_cero_deja_el_maestro_exactamente_igual()
    {
        var maestro = new MaestroAves(1_840, 320, 0);
        Assert.Equal(maestro, AplicarDelta(maestro, Delta.Cero));
    }

    // ── 2 · Sumar aves conserva las bajas ya aplicadas ───────────────────────

    [Fact]
    public void Sumar_aves_sube_el_saldo_por_el_delta_y_conserva_las_bajas()
    {
        // Lote que arrancó con 10.000 H y lleva 3.000 bajas descontadas ⇒ maestro en 7.000.
        var inicialVigente = new MaestroAves(10_000, 0, 0);
        var maestro = new MaestroAves(7_000, 0, 0);

        var delta = Calcular(inicialVigente, new MaestroAves(10_500, 0, 0));
        var nuevoMaestro = AplicarDelta(maestro, delta);

        Assert.Equal(500, delta.Hembras);
        Assert.Equal(7_500, nuevoMaestro.Hembras);
        // Lo que importa: las 3.000 bajas siguen descontadas (10.500 − 7.500 = 3.000).
        Assert.Equal(3_000, 10_500 - nuevoMaestro.Hembras);
    }

    [Fact]
    public void Sumar_aves_reparte_por_sexo_de_forma_independiente()
    {
        var delta = Calcular(new MaestroAves(10_000, 2_000, 0), new MaestroAves(10_500, 1_900, 0));

        Assert.Equal(500, delta.Hembras);
        Assert.Equal(-100, delta.Machos);
        Assert.Equal(400, delta.Total);
    }

    // ── 3 y 4 · Gate al restar ───────────────────────────────────────────────

    private static MovimientoDia[] SerieDe(params (DateTime Fecha, int Perdidas, int Ventas)[] dias) =>
        dias.Select(d => new MovimientoDia(d.Fecha, d.Perdidas, d.Ventas)).ToArray();

    [Fact]
    public void Restar_por_encima_de_lo_consumido_es_compatible()
    {
        // Consumo total 1.500; el nuevo inicial (2.000) todavía alcanza.
        var serie = SerieDe((D1, 500, 0), (D2, 500, 0), (D3, 0, 500));

        var d = Diagnosticar(inicialPropuesto: 2_000, mortalidadCaja: 0, serie);

        Assert.True(d.Compatible);
        Assert.Null(d.PrimerDiaNegativo);
        Assert.Equal(500, d.SaldoFinal);
        Assert.Equal(1_500, d.ConsumoTotal);
    }

    [Fact]
    public void Restar_por_debajo_de_lo_consumido_se_rechaza_y_nombra_el_primer_dia()
    {
        var serie = SerieDe((D1, 500, 0), (D2, 500, 0), (D3, 0, 500));

        var d = Diagnosticar(inicialPropuesto: 1_200, mortalidadCaja: 0, serie);

        // 1.200 − 500 − 500 todavía da 200: el día que rompe es el de la venta, no el primero.
        Assert.False(d.Compatible);
        Assert.Equal(D3, d.PrimerDiaNegativo);
        Assert.Equal(300, d.FaltanAves);
        Assert.Equal(-300, d.SaldoFinal);
    }

    [Fact]
    public void El_primer_dia_negativo_es_el_dia_exacto_en_que_se_agota()
    {
        // 1.000 iniciales: el día 1 deja 400, el día 2 se pasa por 100.
        var serie = SerieDe((D1, 600, 0), (D2, 500, 0), (D3, 100, 0));

        var d = Diagnosticar(inicialPropuesto: 1_000, mortalidadCaja: 0, serie);

        Assert.False(d.Compatible);
        Assert.Equal(D2, d.PrimerDiaNegativo);
        Assert.Equal(100, d.FaltanAves);
        Assert.Equal(-200, d.SaldoFinal);
        Assert.Equal(1_200, d.ConsumoTotal);
    }

    [Fact]
    public void El_diagnostico_ordena_la_serie_y_no_depende_del_orden_de_entrada()
    {
        var enOrden = Diagnosticar(1_000, 0, SerieDe((D1, 600, 0), (D2, 500, 0)));
        var desordenada = Diagnosticar(1_000, 0, SerieDe((D2, 500, 0), (D1, 600, 0)));

        Assert.Equal(enOrden, desordenada);
        Assert.Equal(D2, desordenada.PrimerDiaNegativo);
    }

    [Fact]
    public void Las_ventas_cuentan_igual_que_las_bajas()
    {
        var soloBajas = Diagnosticar(1_000, 0, SerieDe((D1, 1_100, 0)));
        var soloVentas = Diagnosticar(1_000, 0, SerieDe((D1, 0, 1_100)));

        Assert.Equal(soloBajas, soloVentas);
        Assert.False(soloVentas.Compatible);
        Assert.Equal(100, soloVentas.FaltanAves);
    }

    [Fact]
    public void La_mortalidad_en_caja_baja_la_base_igual_que_en_la_fn()
    {
        // Espejo de aves_iniciales (v8): inicial − mort_caja, con piso 0.
        var d = Diagnosticar(inicialPropuesto: 1_000, mortalidadCaja: 200, SerieDe((D1, 800, 0)));

        Assert.True(d.Compatible);
        Assert.Equal(0, d.SaldoFinal);

        var apenasMas = Diagnosticar(1_000, 200, SerieDe((D1, 801, 0)));
        Assert.False(apenasMas.Compatible);
        Assert.Equal(1, apenasMas.FaltanAves);
    }

    [Fact]
    public void Un_lote_sin_serie_siempre_es_compatible()
    {
        var d = Diagnosticar(inicialPropuesto: 5_000, mortalidadCaja: 0, Array.Empty<MovimientoDia>());

        Assert.True(d.Compatible);
        Assert.Equal(5_000, d.SaldoFinal);
        Assert.Equal(0, d.ConsumoTotal);
    }

    [Fact]
    public void El_mensaje_de_rechazo_dice_el_dia_las_aves_que_faltan_y_el_minimo()
    {
        var d = Diagnosticar(1_000, 0, SerieDe((D1, 600, 0), (D2, 500, 0), (D3, 100, 0)));
        var msg = MensajeIncompatible(d, inicialPropuesto: 1_000);

        // Fecha dd/MM/yyyy invariante y números SIN separador de miles: el mensaje no puede depender
        // de la cultura del servidor (un "N0" en ECS sale "1,200", que se lee como 1 coma 2).
        Assert.Contains("02/07/2026", msg);   // el día que rompe
        Assert.Contains("100", msg);          // las aves que faltan
        Assert.Contains("1200", msg);         // el mínimo con el que puede quedar
    }

    // ── 5 · Lote mixto (Panamá) ──────────────────────────────────────────────

    [Fact]
    public void En_lote_mixto_el_delta_vive_en_mixtas_aunque_se_digite_en_hembras()
    {
        var inicialVigente = new MaestroAves(0, 0, 20_000);

        var delta = Calcular(inicialVigente, new MaestroAves(20_500, 0, 0));

        Assert.Equal(0, delta.Hembras);
        Assert.Equal(0, delta.Machos);
        Assert.Equal(500, delta.Mixtas);
    }

    [Fact]
    public void En_lote_mixto_el_maestro_se_mueve_solo_en_mixtas()
    {
        var inicialVigente = new MaestroAves(0, 0, 20_000);
        var maestro = new MaestroAves(0, 0, 17_000);

        var nuevo = AplicarDelta(maestro, Calcular(inicialVigente, new MaestroAves(0, 0, 20_500)));

        Assert.Equal(new MaestroAves(0, 0, 17_500), nuevo);
    }

    [Fact]
    public void Un_lote_por_sexo_no_se_convierte_en_mixto()
    {
        var normalizado = Normalizar(new MaestroAves(10_000, 2_000, 0), new MaestroAves(10_500, 1_900, 0));
        Assert.Equal(new MaestroAves(10_500, 1_900, 0), normalizado);
    }

    // ── 6 · Reversibilidad y clamp ───────────────────────────────────────────

    [Fact]
    public void Aplicar_un_delta_y_su_opuesto_devuelve_el_maestro_original()
    {
        var maestro = new MaestroAves(7_000, 1_500, 0);
        var suma = new Delta(500, 200, 0);
        var resta = new Delta(-500, -200, 0);

        Assert.Equal(maestro, AplicarDelta(AplicarDelta(maestro, suma), resta));
    }

    [Fact]
    public void Un_delta_negativo_mayor_que_el_saldo_clampea_a_cero_sin_dejar_negativos()
    {
        var maestro = new MaestroAves(300, 0, 0);

        var nuevo = AplicarDelta(maestro, new Delta(-1_000, 0, 0));

        Assert.Equal(0, nuevo.Hembras);
        Assert.True(nuevo.Total >= 0);
    }

    [Fact]
    public void El_gate_rechaza_el_caso_que_obligaria_al_clamp()
    {
        // Mismo escenario del test anterior visto desde el gate: 10.000 iniciales, 9.700 consumidas,
        // y alguien intenta dejar el lote en 9.000.
        var serie = SerieDe((D1, 9_700, 0));

        var d = Diagnosticar(inicialPropuesto: 9_000, mortalidadCaja: 0, serie);

        Assert.False(d.Compatible);
        Assert.Equal(700, d.FaltanAves);
    }

    // ── 7 · Regresión: la serie tiene que ser la del CICLO, no la de una etapa ───

    [Fact]
    public void Medir_solo_una_etapa_del_ciclo_deja_pasar_un_ajuste_que_hunde_la_otra()
    {
        // Caso real que el smoke destapó (lote 13 / K345A): levante consumió 739 aves y producción
        // 2.492 más. Con la serie COMPLETA, bajar la base a 1.232 se rechaza; con la de levante sola
        // pasaba el filtro y hundía lote_postura_produccion.aves_h_inicial de 7.597 a 0 por clamp.
        // El cálculo es el mismo en los dos casos: lo que cambia es qué serie recibe.
        var soloLevante = SerieDe((D1, 739, 0));
        var cicloCompleto = SerieDe((D1, 739, 0), (D2, 2_492, 0));

        Assert.True(Diagnosticar(1_232, 0, soloLevante).Compatible);

        var real = Diagnosticar(1_232, 0, cicloCompleto);
        Assert.False(real.Compatible);
        Assert.Equal(D2, real.PrimerDiaNegativo);
        Assert.Equal(3_231, real.ConsumoTotal);
    }

    // ── 8 · Valores defensivos ───────────────────────────────────────────────

    [Fact]
    public void Los_negativos_digitados_se_tratan_como_cero()
    {
        var normalizado = Normalizar(new MaestroAves(10_000, 0, 0), new MaestroAves(-5, -5, -5));
        Assert.Equal(new MaestroAves(0, 0, 0), normalizado);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-500)]
    public void Una_mortalidad_en_caja_negativa_no_infla_la_base(int mortCaja)
    {
        var d = Diagnosticar(inicialPropuesto: 1_000, mortalidadCaja: mortCaja, SerieDe((D1, 1_000, 0)));

        Assert.True(d.Compatible);
        Assert.Equal(0, d.SaldoFinal);
    }

    [Fact]
    public void Un_dia_con_perdidas_negativas_no_devuelve_aves()
    {
        var d = Diagnosticar(1_000, 0, SerieDe((D1, -100, 0), (D2, 1_000, 0)));

        Assert.True(d.Compatible);
        Assert.Equal(0, d.SaldoFinal);
        Assert.Equal(1_000, d.ConsumoTotal);
    }
}
