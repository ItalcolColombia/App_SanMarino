using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de las OPCIONES de los desplegables del histórico de inventario (concepto, tipo de ítem,
/// unidad). Existe porque las listas se armaban con <c>Distinct()</c> sobre el texto crudo mientras
/// los filtros del API comparan normalizado: el catálogo de Ecuador tiene <c>Otros insumos</c> y
/// <c>Otros Insumos</c> conviviendo (42 y 40 ítems en las empresas 3 y 5) y los movimientos traen
/// <c>und</c> / <c>UND</c>, así que el usuario veía dos opciones que devuelven las mismas filas.
/// </summary>
public class EtiquetasFiltroInventarioCalculosTests
{
    private static List<string> Unicas(params (string? Valor, int Usos)[] valores) =>
        EtiquetasFiltroInventarioCalculos.EtiquetasUnicas(valores);

    // ── Normalizar ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Otros insumos", "otros insumos")]
    [InlineData("Otros Insumos", "otros insumos")]
    [InlineData("  ALIMENTO  ", "alimento")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void Normalizar_usa_la_misma_clave_que_los_WHERE_del_API(string? valor, string esperado) =>
        Assert.Equal(esperado, EtiquetasFiltroInventarioCalculos.Normalizar(valor));

    // ── Sin variantes: la lista sale igual que antes del cambio ─────────────────────────────

    [Fact]
    public void Sin_variantes_devuelve_todo_ordenado_case_insensitive()
    {
        var lista = Unicas(("Vacuna", 21), ("Alimento", 8), ("Medicamento", 31), ("Gas", 2));

        Assert.Equal(new[] { "Alimento", "Gas", "Medicamento", "Vacuna" }, lista);
    }

    [Fact]
    public void Lista_vacia_devuelve_lista_vacia() =>
        Assert.Empty(EtiquetasFiltroInventarioCalculos.EtiquetasUnicas([]));

    // ── El caso del ticket ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Caso_del_ticket_Otros_insumos_queda_en_UNA_opcion_la_mayoritaria()
    {
        var lista = Unicas(("Otros insumos", 36), ("Otros Insumos", 6), ("Desinfectante", 36));

        Assert.Equal(new[] { "Desinfectante", "Otros insumos" }, lista);
    }

    [Fact]
    public void Caso_unidades_und_UND_queda_en_UNA_opcion()
    {
        var lista = Unicas(("und", 120), ("UND", 3), ("kg", 900));

        Assert.Equal(new[] { "kg", "und" }, lista);
    }

    [Fact]
    public void Gana_la_variante_mas_usada_aunque_llegue_ultima()
    {
        Assert.Equal(new[] { "Otros insumos" }, Unicas(("Otros Insumos", 6), ("Otros insumos", 36)));
        Assert.Equal(new[] { "Otros Insumos" }, Unicas(("Otros insumos", 6), ("Otros Insumos", 36)));
    }

    [Fact]
    public void Tres_variantes_del_mismo_valor_colapsan_a_una()
    {
        var lista = Unicas(("alimento", 5), ("Alimento", 17), ("ALIMENTO", 1));

        Assert.Equal(new[] { "Alimento" }, lista);
    }

    // ── Determinismo ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Empate_de_frecuencia_gana_la_primera_en_orden_ordinal()
    {
        // Ordinal: las mayúsculas van antes que las minúsculas ('I' = 73 < 'i' = 105).
        Assert.Equal(new[] { "Otros Insumos" }, Unicas(("Otros insumos", 6), ("Otros Insumos", 6)));
        Assert.Equal(new[] { "Otros Insumos" }, Unicas(("Otros Insumos", 6), ("Otros insumos", 6)));
    }

    // ── Higiene de valores ──────────────────────────────────────────────────────────────────

    [Fact]
    public void Descarta_nulos_vacios_y_solo_espacios()
    {
        var lista = Unicas((null, 9), ("", 9), ("   ", 9), ("Gas", 2));

        Assert.Equal(new[] { "Gas" }, lista);
    }

    [Fact]
    public void Los_espacios_alrededor_agrupan_con_la_variante_limpia()
    {
        var lista = Unicas(("  Otros insumos ", 2), ("Otros insumos", 36));

        Assert.Equal(new[] { "Otros insumos" }, lista);
    }

    [Fact]
    public void La_etiqueta_devuelta_viene_trimeada()
    {
        Assert.Equal(new[] { "Otros insumos" }, Unicas(("  Otros insumos  ", 36)));
    }
}
