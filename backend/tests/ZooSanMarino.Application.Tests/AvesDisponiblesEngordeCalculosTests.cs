using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.AvesDisponiblesEngordeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Bajas de seguimiento pendientes de descontar del maestro de aves de engorde.
/// Fija la retrocompatibilidad (lote sin filas BAJA_SEGUIMIENTO ⇒ fórmula previa intacta) y el fix
/// del doble descuento que dejaba «Aves disponibles» por debajo del saldo de la tabla diaria.
/// </summary>
public class AvesDisponiblesEngordeCalculosTests
{
    // ── Retrocompatibilidad: sin bajas aplicadas, el pendiente es el total ───
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1830, 0)]
    [InlineData(1646, 184)]
    [InlineData(423, 77)]
    public void Sin_bajas_aplicadas_el_pendiente_es_el_total_registrado(int regH, int regM)
    {
        var (h, m) = BajasPendientesDeAplicar(regH, regM, 0, 0, 0);

        Assert.Equal(regH, h);
        Assert.Equal(regM, m);
    }

    // ── Fix del doble descuento: todo aplicado ⇒ no se resta nada ────────────
    [Theory]
    [InlineData(1830, 0)]     // lote 142 Panamá: la plantilla mixta manda el total en "H"
    [InlineData(1646, 184)]   // lote con bajas repartidas por sexo
    [InlineData(423, 0)]      // lote 179
    public void Con_todas_las_bajas_aplicadas_no_queda_pendiente(int regH, int regM)
    {
        var (h, m) = BajasPendientesDeAplicar(regH, regM, regH, regM, 0);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    // ── Caso real: las bajas del cruce (7 días) nunca se aplicaron ───────────
    [Theory]
    [InlineData(1814, 1361, 453)]  // lote 165: 1.814 registradas, 1.361 aplicadas
    [InlineData(1371, 673, 698)]   // lote 171
    [InlineData(1788, 1346, 442)]  // lote 169
    [InlineData(481, 0, 481)]      // lote 151: solo tiene los 7 días de cruce, ninguno aplicado
    public void Aplicacion_parcial_deja_pendiente_solo_la_diferencia(int reg, int aplicadas, int esperado)
    {
        var (h, m) = BajasPendientesDeAplicar(reg, 0, aplicadas, 0, 0);

        Assert.Equal(esperado, h);
        Assert.Equal(0, m);
    }

    // ── Clamp: el maestro con más descontado que lo registrado no infla ──────
    [Fact]
    public void Mas_aplicadas_que_registradas_no_genera_pendiente_negativo()
    {
        var (h, m) = BajasPendientesDeAplicar(100, 50, 300, 200, 0);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    [Fact]
    public void Valores_negativos_de_entrada_se_tratan_como_cero()
    {
        var (h, m) = BajasPendientesDeAplicar(-5, -9, -3, -2, -7);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    // ── Lote MIXTO: el descuento fue a un único bucket `mixtas` ──────────────
    [Fact]
    public void Lote_mixto_con_todo_aplicado_en_mixtas_no_deja_pendiente()
    {
        // Plantilla mixta: el total del día llega en "H" y se descontó de `mixtas`.
        var (h, m) = BajasPendientesDeAplicar(800, 0, 0, 0, 800);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    [Fact]
    public void Lote_mixto_consume_primero_hembras_y_luego_machos()
    {
        // Archivo por sexo cargado sobre un lote mixto: Repartir sumó h+m en `mixtas`.
        var (h, m) = BajasPendientesDeAplicar(500, 300, 0, 0, 800);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    [Fact]
    public void Lote_mixto_con_aplicacion_parcial_agota_hembras_antes_que_machos()
    {
        var (h, m) = BajasPendientesDeAplicar(500, 300, 0, 0, 600);

        Assert.Equal(0, h);    // 500 consumidas de hembras
        Assert.Equal(200, m);  // quedan 100 de los 600 → machos 300 − 100
    }

    [Fact]
    public void Lote_mixto_con_menos_aplicado_que_hembras_no_toca_machos()
    {
        var (h, m) = BajasPendientesDeAplicar(500, 300, 0, 0, 200);

        Assert.Equal(300, h);
        Assert.Equal(300, m);
    }

    // ── Combinación: parte por sexo y parte en mixtas ────────────────────────
    [Fact]
    public void Aplicadas_por_sexo_y_en_mixtas_se_descuentan_ambas()
    {
        var (h, m) = BajasPendientesDeAplicar(500, 300, 200, 100, 150);

        Assert.Equal(150, h);  // 500 − 200 = 300, menos 150 de mixtas
        Assert.Equal(200, m);  // 300 − 100 = 200, ya no queda excedente mixto
    }

    // ════════════════════════════════════════════════════════════════════════
    //  DisponiblesPorSexo — fórmula ÚNICA del seguimiento diario y de la venta
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fórmula previa del camino de VENTA (antes del fix): restaba las bajas registradas completas,
    /// incluidas las que el maestro ya tenía descontadas. Se conserva para fijar por test dónde
    /// difiere del cálculo unificado y dónde debe seguir dando exactamente lo mismo.
    /// </summary>
    private static (int H, int M) VentaFormulaPrevia(
        int maestroH, int maestroM, int mortCajaH, int mortCajaM,
        bool sieteDias, int asigH, int asigM, int mortCajaReproH, int mortCajaReproM,
        int regH, int regM, int reservH, int reservM)
    {
        var rawH = sieteDias
            ? Math.Max(0, maestroH - mortCajaH - mortCajaReproH - regH)
            : Math.Max(0, maestroH - mortCajaH - asigH - regH);
        var rawM = sieteDias
            ? Math.Max(0, maestroM - mortCajaM - mortCajaReproM - regM)
            : Math.Max(0, maestroM - mortCajaM - asigM - regM);

        return (Math.Max(0, rawH - reservH), Math.Max(0, rawM - reservM));
    }

    private static (int H, int M) SinReproductoras(
        int maestroH, int maestroM, int regH, int regM,
        int aplH, int aplM, int aplX = 0, int reservH = 0, int reservM = 0) =>
        DisponiblesPorSexo(
            maestroHembras: maestroH, maestroMachos: maestroM,
            mortCajaHembras: 0, mortCajaMachos: 0,
            sieteDiasCompletos: false,
            asignadasHembras: 0, asignadasMachos: 0,
            mortCajaReproHembras: 0, mortCajaReproMachos: 0,
            registradasHembras: regH, registradasMachos: regM,
            aplicadasHembras: aplH, aplicadasMachos: aplM, aplicadasMixtas: aplX,
            reservadasHembras: reservH, reservadasMachos: reservM);

    // ── Ticket 05ago26: la venta mostraba de menos y bloqueaba despachos ─────

    /// <summary>
    /// CAROLINA · GALPON 4 · lote 2603 (id 97), el caso del ticket. El maestro (762 machos) ya tiene
    /// descontadas 7 de las 729 bajas registradas, así que solo se restan las 722 pendientes.
    /// 40 es lo que muestra fn_seguimiento_diario_engorde(97) en su última fila (edad 49 d).
    /// </summary>
    [Fact]
    public void Ticket_carolina_g4_lote_2603_da_40_como_la_grilla()
    {
        var (h, m) = SinReproductoras(maestroH: 0, maestroM: 762, regH: 0, regM: 729, aplH: 0, aplM: 7);

        Assert.Equal(0, h);
        Assert.Equal(40, m);

        // La fórmula previa de la venta restaba las 7 dos veces → 33, y la operación no podía despachar.
        var previa = VentaFormulaPrevia(0, 762, 0, 0, false, 0, 0, 0, 0, 0, 729, 0, 0);
        Assert.Equal(33, previa.M);
    }

    /// <summary>
    /// Sacachun 3A · Galpon-2 · lote 2602 (id 91), el segundo caso del ticket: la venta llegaba a
    /// CERO disponibles —no dejaba vender ni un ave— sobre 194 vivas.
    /// </summary>
    [Fact]
    public void Ticket_sacachun_3a_g2_lote_2602_da_194_y_no_cero()
    {
        var (h, m) = SinReproductoras(
            maestroH: 814, maestroM: 1022, regH: 848, regM: 1108, aplH: 51, aplM: 263);

        Assert.Equal(17, h);            // 814 − (848 − 51)
        Assert.Equal(177, m);           // 1022 − (1108 − 263)
        Assert.Equal(194, h + m);

        var previa = VentaFormulaPrevia(814, 1022, 0, 0, false, 0, 0, 0, 0, 848, 1108, 0, 0);
        Assert.Equal(0, previa.H + previa.M);
    }

    // ── Retrocompatibilidad: lote sin filas BAJA_SEGUIMIENTO ────────────────

    /// <summary>
    /// CAROLINA · GALPON 4 · lote 2601 (id 37), la corrida anterior del mismo galpón: es anterior al
    /// descuento automático, no tiene filas BAJA_SEGUIMIENTO ⇒ pendiente = total ⇒ las dos pantallas
    /// siguen dando exactamente lo de hoy. (Sus 7 aves y las 7 bajas aplicadas del lote 2603 son
    /// números sin relación: el ticket los leyó como una suma entre lotes que nunca ocurre.)
    /// </summary>
    [Fact]
    public void Lote_sin_bajas_aplicadas_conserva_el_resultado_previo()
    {
        var (h, m) = SinReproductoras(maestroH: 0, maestroM: 566, regH: 0, regM: 559, aplH: 0, aplM: 0);

        Assert.Equal(0, h);
        Assert.Equal(7, m);

        var previa = VentaFormulaPrevia(0, 566, 0, 0, false, 0, 0, 0, 0, 0, 559, 0, 0);
        Assert.Equal(previa, (h, m));
    }

    [Theory]
    [InlineData(13000, 12000, 900, 700, 0, 0)]       // sin aplicar: idénticas
    [InlineData(13000, 12000, 900, 700, 0, 0, 250)]  // con reservas Pendiente
    [InlineData(500, 400, 900, 700, 0, 0)]           // bajas > maestro ⇒ ambas en 0
    public void Sin_bajas_aplicadas_venta_y_seguimiento_coinciden_con_la_formula_previa(
        int maestroH, int maestroM, int regH, int regM, int aplH, int aplM, int reserv = 0)
    {
        var actual = SinReproductoras(maestroH, maestroM, regH, regM, aplH, aplM, 0, reserv, reserv);
        var previa = VentaFormulaPrevia(maestroH, maestroM, 0, 0, false, 0, 0, 0, 0, regH, regM, reserv, reserv);

        Assert.Equal(previa, actual);
    }

    // ── R2: seguimiento y venta devuelven el mismo número ───────────────────

    [Theory]
    [InlineData(0, 762, 0, 729, 0, 7, 0, 0, 0)]
    [InlineData(814, 1022, 848, 1108, 51, 263, 0, 0, 0)]
    [InlineData(5000, 4000, 300, 250, 120, 90, 0, 40, 25)]
    [InlineData(5000, 4000, 300, 250, 0, 0, 200, 0, 0)]
    public void Ambas_pantallas_comparten_el_mismo_calculo(
        int maestroH, int maestroM, int regH, int regM,
        int aplH, int aplM, int aplX, int reservH, int reservM)
    {
        // El widget del seguimiento no resta reservas propias distintas: ambas pantallas alimentan el
        // mismo método con los mismos insumos, así que el número no puede divergir por construcción.
        var seguimiento = SinReproductoras(maestroH, maestroM, regH, regM, aplH, aplM, aplX, reservH, reservM);
        var venta = SinReproductoras(maestroH, maestroM, regH, regM, aplH, aplM, aplX, reservH, reservM);

        Assert.Equal(seguimiento, venta);
    }

    // ── R4/R5: reservas Pendiente y clamp a cero ────────────────────────────

    [Fact]
    public void Las_reservas_pendientes_se_restan_de_lo_disponible()
    {
        var (h, m) = SinReproductoras(
            maestroH: 1000, maestroM: 800, regH: 100, regM: 50,
            aplH: 0, aplM: 0, aplX: 0, reservH: 300, reservM: 250);

        Assert.Equal(600, h);   // 1000 − 100 − 300
        Assert.Equal(500, m);   // 800 − 50 − 250
    }

    [Fact]
    public void Nunca_devuelve_negativos()
    {
        var (h, m) = SinReproductoras(
            maestroH: 100, maestroM: 80, regH: 500, regM: 400,
            aplH: 0, aplM: 0, aplX: 0, reservH: 50, reservM: 50);

        Assert.Equal(0, h);
        Assert.Equal(0, m);
    }

    // ── R7: rama de los 7 días de reproductora ──────────────────────────────

    [Fact]
    public void Con_los_siete_dias_completos_resta_mort_caja_repro_y_no_las_asignadas()
    {
        var (h, m) = DisponiblesPorSexo(
            maestroHembras: 10_000, maestroMachos: 9_000,
            mortCajaHembras: 20, mortCajaMachos: 15,
            sieteDiasCompletos: true,
            asignadasHembras: 4_000, asignadasMachos: 3_500,   // ignoradas: las aves regresaron
            mortCajaReproHembras: 30, mortCajaReproMachos: 25,
            registradasHembras: 100, registradasMachos: 80,
            aplicadasHembras: 0, aplicadasMachos: 0, aplicadasMixtas: 0,
            reservadasHembras: 0, reservadasMachos: 0);

        Assert.Equal(9_850, h);   // 10.000 − 20 − 30 − 100
        Assert.Equal(8_880, m);   // 9.000 − 15 − 25 − 80
    }

    [Fact]
    public void Sin_los_siete_dias_completos_resta_las_asignadas_a_reproductora()
    {
        var (h, m) = DisponiblesPorSexo(
            maestroHembras: 10_000, maestroMachos: 9_000,
            mortCajaHembras: 20, mortCajaMachos: 15,
            sieteDiasCompletos: false,
            asignadasHembras: 4_000, asignadasMachos: 3_500,
            mortCajaReproHembras: 30, mortCajaReproMachos: 25,   // ignoradas: las aves no regresaron
            registradasHembras: 100, registradasMachos: 80,
            aplicadasHembras: 0, aplicadasMachos: 0, aplicadasMixtas: 0,
            reservadasHembras: 0, reservadasMachos: 0);

        Assert.Equal(5_880, h);   // 10.000 − 20 − 4.000 − 100
        Assert.Equal(5_405, m);   // 9.000 − 15 − 3.500 − 80
    }

    // ── R8: bajas aplicadas en un solo bucket mixto (plantilla Panamá) ──────

    [Fact]
    public void Bajas_aplicadas_como_mixtas_consumen_primero_hembras_y_luego_machos()
    {
        var (h, m) = SinReproductoras(
            maestroH: 1_000, maestroM: 900, regH: 500, regM: 300,
            aplH: 0, aplM: 0, aplX: 600);

        Assert.Equal(1_000, h);   // las 500 pendientes de hembras quedan cubiertas por las mixtas
        Assert.Equal(700, m);     // sobran 100 de las 600 → 300 − 100 = 200 pendientes → 900 − 200
    }
}
