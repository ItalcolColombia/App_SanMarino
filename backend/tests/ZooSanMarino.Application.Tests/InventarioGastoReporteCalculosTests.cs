using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del filtro del reporte de Gastos de inventario. Existe porque el Excel venía trayendo
/// consumos ELIMINADOS mezclados con los reales (46 filas anuladas sobre 467 en el dump de prod):
/// la regla "eliminado no va al reporte" ahora tiene un solo dueño y estos tests son su gate.
/// </summary>
public class InventarioGastoReporteCalculosTests
{
    // ── EsGastoEliminado / EsGastoActivo ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Eliminado")]
    [InlineData("eliminado")]
    [InlineData("ELIMINADO")]
    [InlineData("  Eliminado  ")]
    public void EsGastoEliminado_reconoce_el_estado_sin_importar_caso_ni_espacios(string estado)
    {
        Assert.True(InventarioGastoReporteCalculos.EsGastoEliminado(estado));
        Assert.False(InventarioGastoReporteCalculos.EsGastoActivo(estado));
    }

    [Theory]
    [InlineData("Activo")]
    [InlineData("activo")]
    [InlineData("  Activo ")]
    public void EsGastoActivo_reconoce_el_estado_vigente(string estado)
    {
        Assert.True(InventarioGastoReporteCalculos.EsGastoActivo(estado));
        Assert.False(InventarioGastoReporteCalculos.EsGastoEliminado(estado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Estado_nulo_o_vacio_NO_es_eliminado(string? estado)
    {
        // Fail-safe: ante un estado ausente el gasto se considera vigente (default de la entidad).
        // Lo contrario borraría consumos reales del reporte por un dato sucio.
        Assert.False(InventarioGastoReporteCalculos.EsGastoEliminado(estado));
        Assert.True(InventarioGastoReporteCalculos.EsGastoActivo(estado));
    }

    [Fact]
    public void Un_estado_desconocido_no_se_confunde_con_eliminado()
    {
        Assert.False(InventarioGastoReporteCalculos.EsGastoEliminado("Anulado"));
        Assert.False(InventarioGastoReporteCalculos.EsGastoEliminado("Eliminada"));
        Assert.False(InventarioGastoReporteCalculos.EsGastoEliminado("Elimin"));
    }

    [Fact]
    public void EsGastoActivo_es_el_complemento_exacto_de_EsGastoEliminado()
    {
        foreach (var estado in new string?[] { null, "", " ", "Activo", "Eliminado", "eliminado", "Otro" })
            Assert.NotEqual(
                InventarioGastoReporteCalculos.EsGastoEliminado(estado),
                InventarioGastoReporteCalculos.EsGastoActivo(estado));
    }

    // ── ClaveOrdenConcepto ──────────────────────────────────────────────────────────────────

    [Fact]
    public void ClaveOrdenConcepto_agrupa_las_variantes_de_capitalizacion_del_catalogo()
    {
        // 'Otros insumos' y 'Otros Insumos' conviven hoy en item_inventario_ecuador: sin normalizar
        // la clave quedarían separados por todo el resto de conceptos en la hoja de existencias.
        Assert.Equal(
            InventarioGastoReporteCalculos.ClaveOrdenConcepto("Otros insumos"),
            InventarioGastoReporteCalculos.ClaveOrdenConcepto("Otros Insumos"));
        Assert.Equal(
            InventarioGastoReporteCalculos.ClaveOrdenConcepto(" Vacuna "),
            InventarioGastoReporteCalculos.ClaveOrdenConcepto("vacuna"));
    }

    [Fact]
    public void ClaveOrdenConcepto_no_mezcla_conceptos_distintos()
    {
        Assert.NotEqual(
            InventarioGastoReporteCalculos.ClaveOrdenConcepto("Desinfectante"),
            InventarioGastoReporteCalculos.ClaveOrdenConcepto("Medicamento"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClaveOrdenConcepto_manda_los_sin_concepto_al_final(string? concepto)
    {
        var sinConcepto = InventarioGastoReporteCalculos.ClaveOrdenConcepto(concepto);
        foreach (var real in new[] { "Desinfectante", "Empaques", "Gas", "Medicamento", "Vacuna", "Pollinaza" })
            Assert.True(string.CompareOrdinal(InventarioGastoReporteCalculos.ClaveOrdenConcepto(real), sinConcepto) < 0);
    }

    // ── EtiquetaConcepto ────────────────────────────────────────────────────────────────────

    [Fact]
    public void EtiquetaConcepto_devuelve_el_concepto_del_catalogo_tal_cual()
    {
        // El reporte NO reescribe el concepto: la normalización es solo clave de orden.
        Assert.Equal("Otros Insumos", InventarioGastoReporteCalculos.EtiquetaConcepto("Otros Insumos"));
        Assert.Equal("Otros insumos", InventarioGastoReporteCalculos.EtiquetaConcepto("Otros insumos"));
        Assert.Equal("Desinfectante", InventarioGastoReporteCalculos.EtiquetaConcepto("  Desinfectante  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EtiquetaConcepto_usa_la_etiqueta_por_defecto_cuando_falta(string? concepto)
    {
        Assert.Equal(
            InventarioGastoReporteCalculos.ConceptoSinAsignar,
            InventarioGastoReporteCalculos.EtiquetaConcepto(concepto));
    }
}
