using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.RetiroAvesEngordeCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Descuento de aves de un lote pollo engorde por las bajas del seguimiento diario.
/// Cubre el caso Panamá (lote MIXTO: todo el total llega en las columnas "H" de la plantilla) y la
/// regresión del camino por sexo, que debe quedar idéntico al comportamiento previo.
/// </summary>
public class RetiroAvesEngordeCalculosTests
{
    // ── Detección de lote mixto (por DATOS, no por país) ─────────────────────
    [Theory]
    [InlineData(0, 0, 800, true)]    // solo mixtas → mixto
    [InlineData(500, 300, 0, false)] // solo sexos → por sexo
    [InlineData(500, 300, 100, false)] // híbrido → gana el camino por sexo (fail-safe)
    [InlineData(0, 0, 0, false)]     // lote vacío → no es mixto
    [InlineData(0, 300, 100, false)] // machos + mixtas → por sexo
    public void EsLoteMixto_decide_por_los_datos_del_lote(int h, int m, int x, bool esperado) =>
        Assert.Equal(esperado, EsLoteMixto(new MaestroAves(h, m, x)));

    // ── Reparto de las bajas del día ─────────────────────────────────────────
    [Fact]
    public void Repartir_en_lote_con_sexos_conserva_el_comportamiento_actual()
    {
        var r = Repartir(new MaestroAves(500, 300, 0), bajasHembras: 5, bajasMachos: 3);
        Assert.Equal(new RetiroAves(5, 3, 0), r);
    }

    [Fact]
    public void Repartir_en_lote_mixto_suma_los_dos_sexos_en_mixtas()
    {
        var r = Repartir(new MaestroAves(0, 0, 800), bajasHembras: 20, bajasMachos: 10);
        Assert.Equal(new RetiroAves(0, 0, 30), r);
    }

    [Fact]
    public void Repartir_en_lote_mixto_con_todo_el_total_en_hembras_plantilla_mixta()
    {
        var r = Repartir(new MaestroAves(0, 0, 800), bajasHembras: 30, bajasMachos: 0);
        Assert.Equal(new RetiroAves(0, 0, 30), r);
    }

    [Fact]
    public void Repartir_ignora_valores_negativos()
    {
        var r = Repartir(new MaestroAves(500, 300, 0), bajasHembras: -5, bajasMachos: 3);
        Assert.Equal(new RetiroAves(0, 3, 0), r);
    }

    // ── Alta de un seguimiento ───────────────────────────────────────────────
    [Fact]
    public void AplicarDelta_descuenta_mixtas_en_un_lote_mixto()
    {
        var res = AplicarDelta(new MaestroAves(0, 0, 800), 30, 0);

        Assert.Equal(new MaestroAves(0, 0, 770), res.Maestro);
        Assert.Equal(new RetiroAves(0, 0, 30), res.Descontado);
        Assert.True(res.Devuelto.EsVacio);
        Assert.False(res.Insuficiente);
    }

    [Fact]
    public void AplicarDelta_descuenta_por_sexo_en_un_lote_con_sexos()
    {
        var res = AplicarDelta(new MaestroAves(500, 300, 0), 5, 3);

        Assert.Equal(new MaestroAves(495, 297, 0), res.Maestro);
        Assert.Equal(new RetiroAves(5, 3, 0), res.Descontado);
        Assert.False(res.Insuficiente);
    }

    [Fact]
    public void AplicarDelta_no_baja_de_cero_y_marca_insuficiente()
    {
        var res = AplicarDelta(new MaestroAves(0, 0, 10), 30, 0);

        Assert.Equal(new MaestroAves(0, 0, 0), res.Maestro);
        Assert.Equal(new RetiroAves(0, 0, 10), res.Descontado); // solo lo que había
        Assert.True(res.Insuficiente);
    }

    [Fact]
    public void AplicarDelta_sin_bajas_deja_el_maestro_intacto()
    {
        var maestro = new MaestroAves(500, 300, 0);
        var res = AplicarDelta(maestro, 0, 0);

        Assert.Equal(maestro, res.Maestro);
        Assert.True(res.SinEfecto);
        Assert.False(res.Insuficiente);
    }

    // ── Edición: el delta puede ser negativo ─────────────────────────────────
    [Fact]
    public void AplicarDelta_negativo_devuelve_aves_mixtas()
    {
        var res = AplicarDelta(new MaestroAves(0, 0, 770), -10, 0);

        Assert.Equal(new MaestroAves(0, 0, 780), res.Maestro);
        Assert.Equal(new RetiroAves(0, 0, 10), res.Devuelto);
        Assert.True(res.Descontado.EsVacio);
        Assert.False(res.Insuficiente);
    }

    [Fact]
    public void AplicarDelta_en_lote_mixto_netea_los_dos_sexos()
    {
        // La edición sube hembras en 5 y baja machos en 5 ⇒ en un lote mixto no cambia nada.
        var maestro = new MaestroAves(0, 0, 770);
        var res = AplicarDelta(maestro, 5, -5);

        Assert.Equal(maestro, res.Maestro);
        Assert.True(res.SinEfecto);
    }

    [Fact]
    public void AplicarDelta_por_sexo_puede_descontar_y_devolver_a_la_vez()
    {
        var res = AplicarDelta(new MaestroAves(500, 300, 0), 5, -3);

        Assert.Equal(new MaestroAves(495, 303, 0), res.Maestro);
        Assert.Equal(new RetiroAves(5, 0, 0), res.Descontado);
        Assert.Equal(new RetiroAves(0, 3, 0), res.Devuelto);
    }

    // ── Borrado: revierte exactamente lo descontado ──────────────────────────
    [Fact]
    public void Alta_y_borrado_dejan_el_maestro_como_estaba()
    {
        var original = new MaestroAves(0, 0, 800);

        var alta = AplicarDelta(original, 30, 0);
        var borrado = AplicarDelta(alta.Maestro, -30, 0);

        Assert.Equal(original, borrado.Maestro);
    }

    [Fact]
    public void Alta_y_borrado_por_sexo_dejan_el_maestro_como_estaba()
    {
        var original = new MaestroAves(500, 300, 0);

        var alta = AplicarDelta(original, 5, 3);
        var borrado = AplicarDelta(alta.Maestro, -5, -3);

        Assert.Equal(original, borrado.Maestro);
    }

    // ── Bajas del día ────────────────────────────────────────────────────────
    [Fact]
    public void BajasDelDia_suma_mortalidad_seleccion_y_error_de_sexaje()
    {
        var (h, m) = BajasDelDia(
            mortalidadHembras: 30, selHembras: 4, errorSexajeHembras: 1,
            mortalidadMachos: 2, selMachos: 1, errorSexajeMachos: 0);

        Assert.Equal(35, h);
        Assert.Equal(3, m);
    }

    [Fact]
    public void BajasDelDia_trata_los_negativos_como_cero()
    {
        var (h, m) = BajasDelDia(-5, 4, 1, 2, -1, 0);

        Assert.Equal(5, h);
        Assert.Equal(2, m);
    }

    // ── Baseline: lo que REALMENTE se aplicó al maestro ──────────────────────
    // La fila viva del histórico es la única prueba de que un día descontó. Sin ella el baseline es
    // 0, aunque el registro tenga bajas cargadas.

    [Fact]
    public void BaselineAplicado_sin_fila_es_cero() =>
        Assert.Equal((0, 0), BaselineAplicado(null));

    [Fact]
    public void BaselineAplicado_de_una_fila_por_sexo() =>
        Assert.Equal((5, 12), BaselineAplicado(new RetiroAves(5, 12, 0)));

    [Fact]
    public void BaselineAplicado_de_una_fila_mixta_va_al_bucket_machos() =>
        // AplicarDelta netea los dos sexos en un lote mixto, así que (0,0,30) y (0,30) dan el mismo delta.
        Assert.Equal((0, 30), BaselineAplicado(new RetiroAves(0, 0, 30)));

    // ── Regresión: cohorte anterior al aplicador (< 2026-07-27) ──────────────

    [Fact]
    public void Borrar_un_seguimiento_sin_fila_de_historico_no_devuelve_aves()
    {
        // Ese día nunca descontó, así que borrarlo no puede acreditar nada. Con el baseline viejo
        // (las columnas del registro) el maestro se inflaba SIN dejar rastro: es el descuadre de 17
        // aves del lote 107 (Kilometro 61 · G0037), cuyo día 2026-07-24 valía 5 H + 12 M.
        var maestro = new MaestroAves(10_860, 13_374, 0);

        var (viejasH, viejasM) = BaselineAplicado(null);
        var res = AplicarDelta(maestro, 0 - viejasH, 0 - viejasM);

        Assert.Equal(maestro, res.Maestro);
        Assert.True(res.SinEfecto);

        // Contraste explícito con el comportamiento anterior, que sí acreditaba.
        var conBaselineViejo = AplicarDelta(maestro, 0 - 5, 0 - 12);
        Assert.Equal(new MaestroAves(10_865, 13_386, 0), conBaselineViejo.Maestro);
    }

    [Fact]
    public void Editar_un_seguimiento_sin_fila_descuenta_el_total_nuevo_y_no_el_delta()
    {
        // Editar un día viejo debe descontar el total NUEVO: nada de ese día se había descontado.
        // El baseline viejo movía solo el delta y a la vez creaba una fila con el total nuevo, así
        // que la conservación quedaba corrida por el valor ANTERIOR del registro.
        var maestro = new MaestroAves(1_000, 800, 0);

        var (viejasH, viejasM) = BaselineAplicado(null);
        var res = AplicarDelta(maestro, 4 - viejasH, 3 - viejasM);

        Assert.Equal(new MaestroAves(996, 797, 0), res.Maestro);
    }

    // ── Camino normal (con fila viva): idéntico al comportamiento previo ─────

    [Fact]
    public void Alta_y_borrado_con_fila_viva_siguen_siendo_simetricos()
    {
        var original = new MaestroAves(500, 300, 0);

        var alta = AplicarDelta(original, 5, 3);
        var fila = Repartir(original, 5, 3); // lo que quedó registrado en el histórico

        var (viejasH, viejasM) = BaselineAplicado(fila);
        var borrado = AplicarDelta(alta.Maestro, 0 - viejasH, 0 - viejasM);

        Assert.Equal(original, borrado.Maestro);
    }

    [Fact]
    public void Alta_y_borrado_con_fila_viva_siguen_siendo_simetricos_en_lote_mixto()
    {
        var original = new MaestroAves(0, 0, 800);

        var alta = AplicarDelta(original, 30, 0);
        var fila = Repartir(original, 30, 0); // (0, 0, 30)

        var (viejasH, viejasM) = BaselineAplicado(fila);
        var borrado = AplicarDelta(alta.Maestro, 0 - viejasH, 0 - viejasM);

        Assert.Equal(original, borrado.Maestro);
    }

    [Fact]
    public void Edicion_con_fila_viva_mueve_solo_el_delta()
    {
        var maestro = new MaestroAves(495, 297, 0);
        var fila = new RetiroAves(5, 3, 0); // lo aplicado por el alta

        var (viejasH, viejasM) = BaselineAplicado(fila);
        var res = AplicarDelta(maestro, 8 - viejasH, 1 - viejasM);

        Assert.Equal(new MaestroAves(492, 299, 0), res.Maestro);
    }
}
