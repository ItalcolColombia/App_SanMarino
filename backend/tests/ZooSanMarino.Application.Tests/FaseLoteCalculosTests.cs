using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// La fase indicada manda; sin fase indicada se conserva EXACTAMENTE la derivación previa
/// (≥ 26 semanas ⇒ Producción), que es lo que garantiza que ninguna empresa cambie de
/// comportamiento por este agregado.
/// </summary>
public class FaseLoteCalculosTests
{
    // ── Derivación histórica: no puede cambiar ────────────────────────────────
    [Theory]
    [InlineData(0, "Levante")]
    [InlineData(1, "Levante")]
    [InlineData(25, "Levante")]
    [InlineData(26, "Produccion")]
    [InlineData(27, "Produccion")]
    [InlineData(52, "Produccion")]
    public void DerivarPorEdad_conserva_el_corte_en_26_semanas(int semanas, string esperada)
    {
        Assert.Equal(esperada, FaseLoteCalculos.DerivarPorEdad(semanas));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolver_sin_fase_indicada_deriva_igual_que_antes(string? indicada)
    {
        for (var semanas = 0; semanas <= 60; semanas++)
            Assert.Equal(FaseLoteCalculos.DerivarPorEdad(semanas), FaseLoteCalculos.Resolver(indicada, semanas));
    }

    // ── Fase indicada ─────────────────────────────────────────────────────────
    [Fact]
    public void Resolver_respeta_la_fase_indicada_aunque_la_edad_diga_otra_cosa()
    {
        // El caso que motiva el cambio: histórico encasetado hace 49 semanas que se carga como
        // LEVANTE para que los reportes de levante lo vean.
        Assert.Equal("Levante", FaseLoteCalculos.Resolver("Levante", semanasDesdeEncaset: 49));
        Assert.Equal("Produccion", FaseLoteCalculos.Resolver("Produccion", semanasDesdeEncaset: 3));
    }

    [Theory]
    [InlineData("levante", "Levante")]
    [InlineData("LEVANTE", "Levante")]
    [InlineData("  Levante  ", "Levante")]
    [InlineData("produccion", "Produccion")]
    [InlineData("PRODUCCION", "Produccion")]
    public void NormalizarFaseIndicada_acepta_mayusculas_y_espacios(string entrada, string esperada)
    {
        Assert.Equal(esperada, FaseLoteCalculos.NormalizarFaseIndicada(entrada));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NormalizarFaseIndicada_vacia_devuelve_null(string? entrada)
    {
        Assert.Null(FaseLoteCalculos.NormalizarFaseIndicada(entrada));
    }

    [Theory]
    [InlineData("Engorde")]
    [InlineData("Postura")]
    [InlineData("Producción")]   // con tilde: no es el valor que guarda la columna
    [InlineData("x")]
    public void NormalizarFaseIndicada_rechaza_cualquier_otro_valor(string entrada)
    {
        var ex = Assert.Throws<ArgumentException>(() => FaseLoteCalculos.NormalizarFaseIndicada(entrada));
        Assert.Contains("Levante", ex.Message);
        Assert.Contains("Produccion", ex.Message);
    }

    [Fact]
    public void Validas_son_exactamente_los_dos_valores_que_acepta_la_columna()
    {
        Assert.Equal(new[] { "Levante", "Produccion" }, FaseLoteCalculos.Validas);
    }

    // ── EsRegistroLevante: qué ve el reporte de levante ────────────────────────
    // Motivo: el lote base S369 (encaset ago-2025, cargado ago-2026) nació en «Produccion» por
    // edad, sin haber pasado nunca a producción, y los reportes de levante lo escondían pese a
    // tener 168 seguimientos diarios por sublote.

    [Fact]
    public void EsRegistroLevante_un_lote_en_levante_es_levante()
    {
        Assert.True(FaseLoteCalculos.EsRegistroLevante("Levante", lotePadreId: null));
    }

    [Fact]
    public void EsRegistroLevante_un_historico_cargado_como_produccion_sigue_siendo_levante()
    {
        // S369: fase «Produccion» derivada por edad, sin lote hijo de producción.
        Assert.True(FaseLoteCalculos.EsRegistroLevante("Produccion", lotePadreId: null));
    }

    [Fact]
    public void EsRegistroLevante_el_lote_hijo_de_produccion_NO_es_levante()
    {
        // El único registro que legítimamente no es levante: nace en CrearProduccionLoteAsync
        // con fase «Produccion» y el levante como padre.
        Assert.False(FaseLoteCalculos.EsRegistroLevante("Produccion", lotePadreId: 13));
    }

    [Fact]
    public void EsRegistroLevante_un_sublote_de_levante_con_padre_sigue_siendo_levante()
    {
        // Caso K345B: LotePadreId = 13 (el sublote hermano), fase «Levante».
        Assert.True(FaseLoteCalculos.EsRegistroLevante("Levante", lotePadreId: 13));
    }

    [Fact]
    public void EsRegistroLevante_sin_fase_es_levante()
    {
        Assert.True(FaseLoteCalculos.EsRegistroLevante(null, lotePadreId: null));
        Assert.True(FaseLoteCalculos.EsRegistroLevante(null, lotePadreId: 13));
    }

    [Fact]
    public void LoteEsRegistroLevante_la_expresion_y_el_metodo_son_la_misma_regla()
    {
        // Una sola fórmula por número: la expresión que se empuja a la BD no puede divergir del
        // predicado que cubren los tests de arriba.
        var expr = FaseLoteCalculos.LoteEsRegistroLevante.Compile();

        foreach (var fase in new string?[] { null, "", "Levante", "Produccion" })
        foreach (var padre in new int?[] { null, 0, 13 })
        {
            var lote = new ZooSanMarino.Domain.Entities.Lote
            {
                LoteNombre = "X",
                Fase = fase!,   // la columna fase es nullable en la BD, la propiedad no
                LotePadreId = padre
            };
            Assert.Equal(FaseLoteCalculos.EsRegistroLevante(fase, padre), expr(lote));
        }
    }

    // ── Fase VISIBLE: por estado real, no por edad ───────────────────────────

    [Theory]
    [InlineData(false, false, "Levante")]    // recien encasetado
    [InlineData(false, true,  "Levante")]    // produccion a medio cargar, levante todavia abierto
    [InlineData(true,  false, "Levante")]    // levante cerrado pero sin produccion: sigue siendo levante
    [InlineData(true,  true,  "Produccion")] // las DOS condiciones
    public void ResolverFaseVisible_exige_las_dos_condiciones(bool cerrado, bool tieneProd, string esperada) =>
        Assert.Equal(esperada, FaseLoteCalculos.ResolverFaseVisible(cerrado, tieneProd));

    [Fact]
    public void ResolverFaseVisible_no_depende_de_la_edad_del_lote()
    {
        // El caso que motivo el cambio: lote cargado con historia (encaset viejo) que la pantalla
        // mostraba en «Produccion» por tener mas de 26 semanas. Medido: 8 de los 16 lotes de
        // Sanmarino, todos con el levante abierto y cero filas de produccion.
        Assert.Equal(FaseLoteCalculos.Produccion, FaseLoteCalculos.DerivarPorEdad(51));
        Assert.Equal(FaseLoteCalculos.Levante,
            FaseLoteCalculos.ResolverFaseVisible(levanteCerrado: false, tieneProduccion: false));
    }

    [Theory]
    [InlineData("Cerrado", true)]
    [InlineData("cerrado", true)]
    [InlineData("  CERRADO  ", true)]
    [InlineData("Abierto", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void EsCierreCerrado_tolera_mayusculas_espacios_y_nulos(string? crudo, bool esperado) =>
        Assert.Equal(esperado, FaseLoteCalculos.EsCierreCerrado(crudo));

    [Fact]
    public void ResolverFaseVisible_por_texto_es_la_misma_regla_que_por_bool()
    {
        // Una sola formula: la sobrecarga que recibe el texto crudo no puede divergir de la de bool.
        foreach (var crudo in new string?[] { null, "", "Abierto", "Cerrado", "cerrado", " CERRADO " })
        foreach (var prod in new[] { false, true })
            Assert.Equal(
                FaseLoteCalculos.ResolverFaseVisible(FaseLoteCalculos.EsCierreCerrado(crudo), prod),
                FaseLoteCalculos.ResolverFaseVisible(crudo, prod));
    }
}
