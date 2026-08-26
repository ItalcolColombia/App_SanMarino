using ZooSanMarino.Application.Calculos;

using static ZooSanMarino.Application.Calculos.ReversionMovimientoInventarioCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la reversión de stock al deshacer un movimiento de inventario
/// (`fase_de_desarrollo/ecuador_cuadre_alimento_y_permisos_plan.md`, F1).
///
/// <para>
/// El defecto original: borrar un ingreso marcaba su fila del histórico como anulada —la tabla diaria
/// dejaba de contarlo— pero NO devolvía el stock. Medido en Sacachún 3A / G0044, ítem 5: la tabla
/// decía 7.720,000 kg y el stock 12.720,000 kg, y esos 5.000,000 kg de diferencia son exactamente el
/// ingreso duplicado que la operación había borrado.
/// </para>
///
/// <para>
/// Lo que se prueba acá es la única parte que puede equivocarse en silencio: el SIGNO. Moverlo para
/// el lado contrario deja el doble del error original y nadie se entera hasta que un humano de
/// operación abre un ticket semanas después.
/// </para>
/// </summary>
public class ReversionMovimientoInventarioCalculosTests
{
    // ─── Entradas: revertirlas DESCUENTA ───────────────────────────────────────

    [Theory]
    [InlineData("Ingreso")]
    [InlineData("TrasladoEntrada")]
    [InlineData("TrasladoInterGranjaEntrada")]
    public void Revertir_una_entrada_descuenta_stock(string tipo)
    {
        Assert.Equal(EfectoReversion.Descontar, EfectoSobreStock(tipo));
        Assert.Equal(-1000m, DeltaStock(tipo, 1000m));
    }

    // ─── Salidas: revertirlas DEVUELVE ─────────────────────────────────────────

    [Theory]
    [InlineData("Consumo")]
    [InlineData("TrasladoSalida")]
    [InlineData("TrasladoInterGranjaSalida")]
    [InlineData("EliminacionStock")]
    public void Revertir_una_salida_devuelve_stock(string tipo)
    {
        Assert.Equal(EfectoReversion.Devolver, EfectoSobreStock(tipo));
        Assert.Equal(1000m, DeltaStock(tipo, 1000m));
    }

    // ─── La trampa del legado ──────────────────────────────────────────────────

    /// <summary>
    /// `TrasladoInterGranjaPendiente` PARECE una salida por el nombre, pero los registros con ese tipo
    /// descuentan el origen AL RECIBIR, no al crearse. Devolverle stock al borrarlo inventaría
    /// alimento que nunca salió — el mismo tipo de kilos fantasma que este trabajo vino a eliminar,
    /// solo que del otro lado.
    /// </summary>
    [Theory]
    [InlineData("TrasladoInterGranjaPendiente")]
    [InlineData("TrasladoInterGranjaRechazado")]
    public void El_transito_legado_no_movio_stock_y_revertirlo_tampoco(string tipo)
    {
        Assert.Equal(EfectoReversion.Ninguno, EfectoSobreStock(tipo));
        Assert.Equal(0m, DeltaStock(tipo, 1000m));
        Assert.False(RequiereStockDisponible(tipo));
    }

    // ─── Lo que no se puede revertir ───────────────────────────────────────────

    /// <summary>
    /// `AjusteStock` guarda `Math.Abs(delta)`: perdió el signo, así que la cantidad sola no dice si el
    /// ajuste subió o bajó el stock. Adivinarlo tiene 50 % de probabilidad de duplicar el error.
    /// </summary>
    [Fact]
    public void Un_ajuste_de_stock_no_se_puede_revertir_porque_perdio_el_signo()
    {
        Assert.Equal(EfectoReversion.NoSoportado, EfectoSobreStock("AjusteStock"));
        Assert.Equal(0m, DeltaStock("AjusteStock", 1000m));
    }

    /// <summary>
    /// Fail-closed: un `movement_type` que nadie enseñó a revertir NO se trata como inocuo. Si mañana
    /// alguien agrega un tipo que mueve stock y se olvida de esta tabla, el sistema tiene que fallar
    /// ruidosamente — no repetir en silencio el descuadre de G0044.
    /// </summary>
    [Theory]
    [InlineData("TipoInventadoManana")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Un_tipo_desconocido_es_NoSoportado_y_no_Ninguno(string? tipo)
    {
        Assert.Equal(EfectoReversion.NoSoportado, EfectoSobreStock(tipo));
    }

    // ─── Normalización ─────────────────────────────────────────────────────────

    [Fact]
    public void El_tipo_se_compara_sin_espacios_alrededor()
    {
        Assert.Equal(EfectoReversion.Descontar, EfectoSobreStock("  Ingreso  "));
    }

    /// <summary>
    /// La comparación es ORDINAL, igual que los `HashSet(StringComparer.Ordinal)` que el service ya
    /// usa para estos mismos tipos. Un `ingreso` en minúscula NO es un `Ingreso`, y tratarlo como tal
    /// abriría una segunda forma de escribir cada tipo.
    /// </summary>
    [Fact]
    public void La_comparacion_es_ordinal_y_no_acepta_otra_capitalizacion()
    {
        Assert.Equal(EfectoReversion.NoSoportado, EfectoSobreStock("ingreso"));
    }

    // ─── Sólo descontar puede fallar por saldo ─────────────────────────────────

    [Fact]
    public void Solo_descontar_exige_stock_disponible()
    {
        Assert.True(RequiereStockDisponible("Ingreso"));
        Assert.False(RequiereStockDisponible("Consumo"));
        Assert.False(RequiereStockDisponible("TrasladoSalida"));
    }

    // ─── El caso real que originó el trabajo ───────────────────────────────────

    /// <summary>
    /// G0044 (Sacachún 3A), ítem 5: el ingreso duplicado de la remisión 63705, de 5.000,000 kg. Al
    /// borrarlo, el stock tiene que bajar 5.000,000 — que es exactamente la diferencia que quedó
    /// colgada entre la tabla diaria (7.720,000) y el stock (12.720,000).
    /// </summary>
    [Fact]
    public void El_ingreso_duplicado_de_G0044_baja_el_stock_de_12720_a_7720()
    {
        const decimal stockAntes = 12_720.000m;
        var delta = DeltaStock("Ingreso", 5_000.000m);

        Assert.Equal(-5_000.000m, delta);
        Assert.Equal(7_720.000m, stockAntes + delta);
    }

    /// <summary>
    /// Un grupo de traslado dentro de la misma granja mueve las dos puntas, y revertirlo tiene que
    /// mover las dos: el origen recupera lo que entregó y el destino pierde lo que recibió. Revertir
    /// una sola punta deja el galpón contrario descuadrado por la misma cantidad.
    /// </summary>
    [Fact]
    public void Revertir_un_traslado_mueve_las_dos_puntas_y_suma_cero()
    {
        var enOrigen = DeltaStock("TrasladoSalida", 500m);
        var enDestino = DeltaStock("TrasladoEntrada", 500m);

        Assert.Equal(500m, enOrigen);
        Assert.Equal(-500m, enDestino);
        Assert.Equal(0m, enOrigen + enDestino);
    }

    /// <summary>
    /// Un traslado entre granjas que quedó EN TRÁNSITO tiene una sola fila (la salida): el destino
    /// todavía no recibió nada. Revertirlo devuelve al origen y no toca ningún destino.
    /// </summary>
    [Fact]
    public void Revertir_un_transito_sin_recibir_solo_devuelve_al_origen()
    {
        Assert.Equal(500m, DeltaStock("TrasladoInterGranjaSalida", 500m));
    }

    /// <summary>El signo lo pone el tipo, no el llamador: una cantidad negativa no lo invierte.</summary>
    [Fact]
    public void Una_cantidad_negativa_no_invierte_el_signo_de_la_reversion()
    {
        Assert.Equal(-1000m, DeltaStock("Ingreso", -1000m));
        Assert.Equal(1000m, DeltaStock("Consumo", -1000m));
    }
}
