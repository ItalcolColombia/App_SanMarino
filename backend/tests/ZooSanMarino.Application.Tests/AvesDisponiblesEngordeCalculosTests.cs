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
}
