using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests de la guía genética REDUCIDA (<c>guia_genetica_santa_reyes</c>), plan
/// <c>fase_de_desarrollo/guia_genetica_tres_modulos_plan.md</c> §4 (F2.1) y §5 (casos 2, 3 y 4).
///
/// <para>
/// Fijan las tres cosas que hacen que la tabla se pueda escribir sin romperse:
/// la <b>clave natural</b> (que es lo que vuelve idempotente al import), el <b>upsert</b> (mismo
/// archivo dos veces ⇒ cero altas) y el <b>vacío ⇒ NULL, nunca 0</b> — que no es cosmética: la raza
/// Criolla tiene 40 semanas legítimamente sin producción, y un 0 ahí diría «puso cero huevos».
/// </para>
/// </summary>
public class GuiaGeneticaSantaReyesCalculosTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Clave natural — §5 caso 2
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El valor exacto que sembró la migración <c>20260820093323_SeedGuiaGeneticaSantaReyes</c>
    /// para su primera fila. Si esto cambia, las 615 filas ya cargadas dejan de matchear y el
    /// próximo import las duplica en vez de actualizarlas.
    /// </summary>
    [Fact]
    public void Codigo_natural_reproduce_el_del_seed()
    {
        Assert.Equal(
            "Babcock Brown202618",
            GuiaGeneticaSantaReyesCalculos.CodigoNatural("Babcock Brown", "2026", 18));
    }

    [Theory]
    [InlineData("Babcock Brown", "2026", 18, "Babcock Brown202618")]
    [InlineData("Hy Line Brown", "2026", 140, "Hy Line Brown2026140")]
    [InlineData("Criolla", "2026", 101, "Criolla2026101")]
    [InlineData("Lohmann LSL", "2025", 18, "Lohmann LSL202518")]
    [InlineData("Azur", "2026", 99, "Azur202699")]
    public void Codigo_natural_concatena_raza_anio_edad(string raza, string anio, int edad, string esperado)
    {
        Assert.Equal(esperado, GuiaGeneticaSantaReyesCalculos.CodigoNatural(raza, anio, edad));
    }

    /// <summary>
    /// 🔴 Equivalencia con <c>ExcelImportService.ComputeCodigo</c> (<c>ExcelImportService.cs:491-497</c>),
    /// que es <c>private static</c> y no se puede invocar desde acá: se replica su cuerpo <b>letra por
    /// letra</b> y se exige que las dos salidas coincidan. Es la única forma de que este test falle
    /// el día que alguien cambie una de las dos fórmulas y no la otra.
    /// </summary>
    [Theory]
    [InlineData("Babcock Brown", "2026", "18")]
    [InlineData(" Babcock Brown ", " 2026 ", " 18 ")]
    [InlineData("Criolla", "2026", "140")]
    [InlineData("Lohmann LSL", "2025", "1")]
    public void Codigo_natural_es_identico_al_del_import_compartido(string raza, string anio, string edadTexto)
    {
        // Copia textual de ExcelImportService.ComputeCodigo (sin la rama del código ya provisto).
        static string ComputeCodigoDelImportCompartido(string raza, string anioGuia, string edad)
            => $"{raza.Trim()}{anioGuia.Trim()}{edad.Trim()}";

        Assert.True(GuiaGeneticaSantaReyesCalculos.TryParsearEdad(edadTexto, out var edad));

        Assert.Equal(
            ComputeCodigoDelImportCompartido(raza, anio, edadTexto),
            GuiaGeneticaSantaReyesCalculos.CodigoNatural(raza, anio, edad));
    }

    /// <summary>La clave se recorta igual que en la compartida: los espacios del Excel no entran.</summary>
    [Fact]
    public void Codigo_natural_recorta_raza_y_anio()
    {
        Assert.Equal(
            "Babcock Brown202618",
            GuiaGeneticaSantaReyesCalculos.CodigoNatural("  Babcock Brown  ", "  2026 ", 18));
    }

    /// <summary>
    /// El código se RECALCULA al cambiar cualquiera de los tres componentes: si no, editar la semana
    /// de una línea la dejaría con el código de la semana vieja y el próximo import la duplicaría.
    /// </summary>
    [Fact]
    public void Codigo_natural_cambia_si_cambia_cualquiera_de_los_tres()
    {
        var original = GuiaGeneticaSantaReyesCalculos.CodigoNatural("Babcock Brown", "2026", 18);

        Assert.NotEqual(original, GuiaGeneticaSantaReyesCalculos.CodigoNatural("Babcock Black", "2026", 18));
        Assert.NotEqual(original, GuiaGeneticaSantaReyesCalculos.CodigoNatural("Babcock Brown", "2027", 18));
        Assert.NotEqual(original, GuiaGeneticaSantaReyesCalculos.CodigoNatural("Babcock Brown", "2026", 19));
    }

    [Theory]
    [InlineData(null, "2026")]
    [InlineData("", "2026")]
    [InlineData("   ", "2026")]
    [InlineData("Babcock Brown", null)]
    [InlineData("Babcock Brown", "")]
    [InlineData("Babcock Brown", "   ")]
    public void Sin_raza_o_sin_anio_no_hay_codigo(string? raza, string? anio)
    {
        Assert.Null(GuiaGeneticaSantaReyesCalculos.CodigoNatural(raza, anio, 18));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Idempotencia del upsert — §5 caso 3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Codigo_nuevo_se_inserta()
    {
        Assert.Equal(
            AccionImportGuiaSantaReyes.Insertar,
            GuiaGeneticaSantaReyesCalculos.DecidirAccion(null, new MetricasGuiaSantaReyes(95.0m, 0.6m, 113.0m)));
    }

    /// <summary>
    /// 🔴 El invariante del plan: reimportar el MISMO archivo no da de alta nada. Con las tres
    /// métricas idénticas la fila ni se toca — reescribirla marcaría 615 filas como modificadas en
    /// cada reimport y ensuciaría <c>updated_at</c> de toda la guía.
    /// </summary>
    [Fact]
    public void Segunda_pasada_del_mismo_archivo_no_inserta_ni_actualiza()
    {
        var guardado = new MetricasGuiaSantaReyes(95.0m, 0.6m, 113.0m);
        var delArchivo = new MetricasGuiaSantaReyes(95.0m, 0.6m, 113.0m);

        var accion = GuiaGeneticaSantaReyesCalculos.DecidirAccion(guardado, delArchivo);

        Assert.Equal(AccionImportGuiaSantaReyes.OmitirSinCambios, accion);
        Assert.NotEqual(AccionImportGuiaSantaReyes.Insertar, accion);
    }

    /// <summary>
    /// La columna es <c>numeric(6,2)</c> y devuelve «95.00» donde el Excel decía «95»: si la
    /// comparación mirara la escala, la 2ª pasada actualizaría las 615 filas «porque cambiaron».
    /// </summary>
    [Fact]
    public void La_escala_del_decimal_no_cuenta_como_cambio()
    {
        var guardado = new MetricasGuiaSantaReyes(95.00m, 0.60m, 113.00m);
        var delArchivo = new MetricasGuiaSantaReyes(95m, 0.6m, 113m);

        Assert.Equal(
            AccionImportGuiaSantaReyes.OmitirSinCambios,
            GuiaGeneticaSantaReyesCalculos.DecidirAccion(guardado, delArchivo));
    }

    [Theory]
    [InlineData(96.0, 0.6, 113.0)]  // cambia produccion
    [InlineData(95.0, 0.7, 113.0)]  // cambia retiro
    [InlineData(95.0, 0.6, 114.0)]  // cambia consumo
    public void Cualquier_metrica_distinta_actualiza(double prod, double retiro, double gramos)
    {
        var guardado = new MetricasGuiaSantaReyes(95.0m, 0.6m, 113.0m);
        var delArchivo = new MetricasGuiaSantaReyes((decimal)prod, (decimal)retiro, (decimal)gramos);

        Assert.Equal(
            AccionImportGuiaSantaReyes.Actualizar,
            GuiaGeneticaSantaReyesCalculos.DecidirAccion(guardado, delArchivo));
    }

    /// <summary>
    /// NULL ⇄ valor es un cambio real en los dos sentidos: cargar la producción de una semana que no
    /// la tenía, y borrarla porque la guía nueva dice que esa semana ya no aplica.
    /// </summary>
    [Fact]
    public void Null_a_valor_y_valor_a_null_son_cambios()
    {
        var conDato = new MetricasGuiaSantaReyes(95.0m, 0.6m, 113.0m);
        var sinDato = new MetricasGuiaSantaReyes(null, 0.6m, 113.0m);

        Assert.Equal(AccionImportGuiaSantaReyes.Actualizar, GuiaGeneticaSantaReyesCalculos.DecidirAccion(sinDato, conDato));
        Assert.Equal(AccionImportGuiaSantaReyes.Actualizar, GuiaGeneticaSantaReyesCalculos.DecidirAccion(conDato, sinDato));
    }

    [Fact]
    public void Dos_nulls_no_son_un_cambio()
    {
        var criollaSemana101 = new MetricasGuiaSantaReyes(null, 8.4m, 108.0m);

        Assert.Equal(
            AccionImportGuiaSantaReyes.OmitirSinCambios,
            GuiaGeneticaSantaReyesCalculos.DecidirAccion(criollaSemana101, criollaSemana101));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Vacío ⇒ NULL, nunca 0 — §5 caso 4
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 🔴 El caso de la raza Criolla: 40 filas (semanas 101–140) con <c>prod_porcentaje</c>
    /// legítimamente nulo. Un 0 ahí no es «casi lo mismo»: cambia el significado de «no hay guía
    /// para esta semana» a «esta ave no puso un solo huevo», y los reportes lo promedian como dato.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Celda_vacia_es_null_no_cero(string? crudo)
    {
        Assert.True(GuiaGeneticaSantaReyesCalculos.TryParsearDecimalOpcional(crudo, out var valor));

        Assert.Null(valor);
        Assert.NotEqual(0m, valor ?? -1m);
    }

    [Fact]
    public void Un_cero_explicito_si_es_cero()
    {
        Assert.True(GuiaGeneticaSantaReyesCalculos.TryParsearDecimalOpcional("0", out var valor));
        Assert.Equal(0m, valor);
    }

    [Theory]
    [InlineData("95", 95)]
    [InlineData("95.9", 95.9)]
    [InlineData("95,9", 95.9)]
    [InlineData(" 95.9 ", 95.9)]
    [InlineData("95.9%", 95.9)]
    [InlineData("1.234,56", 1234.56)]
    [InlineData("1,234.56", 1234.56)]
    [InlineData("-0.5", -0.5)]
    public void Parsea_los_formatos_que_trae_un_excel_de_cliente(string crudo, double esperado)
    {
        Assert.True(GuiaGeneticaSantaReyesCalculos.TryParsearDecimalOpcional(crudo, out var valor));
        Assert.Equal((decimal)esperado, valor);
    }

    [Theory]
    [InlineData("n/a")]
    [InlineData("sin dato")]
    [InlineData("95a")]
    public void Basura_en_una_celda_numerica_no_se_traga_como_null(string crudo)
    {
        Assert.False(GuiaGeneticaSantaReyesCalculos.TryParsearDecimalOpcional(crudo, out var valor));
        Assert.Null(valor);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Edad
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("18", 18)]
    [InlineData("18.0", 18)]   // Excel devuelve así una celda numérica con formato decimal
    [InlineData("18,0", 18)]
    [InlineData(" 140 ", 140)]
    public void Parsea_la_semana(string crudo, int esperado)
    {
        Assert.True(GuiaGeneticaSantaReyesCalculos.TryParsearEdad(crudo, out var edad));
        Assert.Equal(esperado, edad);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sem 18")]
    [InlineData("25P")]        // grafía válida en la tabla COMPARTIDA (edad varchar); acá no
    public void Semana_no_numerica_se_rechaza(string? crudo)
    {
        Assert.False(GuiaGeneticaSantaReyesCalculos.TryParsearEdad(crudo, out _));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Encabezados
    // ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("raza", "raza")]
    [InlineData("RAZA", "raza")]
    [InlineData("Línea genética", "raza")]
    [InlineData("anio_guia", "anio_guia")]
    [InlineData("AÑOGUÍA", "anio_guia")]
    [InlineData("Año Guía", "anio_guia")]
    [InlineData("edad", "edad")]
    [InlineData("Semana", "edad")]
    [InlineData("prod_porcentaje", "prod_porcentaje")]
    [InlineData("%Prod", "prod_porcentaje")]
    [InlineData("retiro_ac_h", "retiro_ac_h")]
    [InlineData("RetiroAcH", "retiro_ac_h")]
    [InlineData("gr_ave_dia_h", "gr_ave_dia_h")]
    [InlineData("GrAveDiaH", "gr_ave_dia_h")]
    public void Reconoce_las_grafias_de_encabezado_del_cliente(string encabezado, string canonico)
    {
        Assert.Equal(canonico, GuiaGeneticaSantaReyesCalculos.MapearEncabezado(encabezado));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("observaciones")]
    public void Una_columna_desconocida_se_ignora_sin_romper(string? encabezado)
    {
        Assert.Null(GuiaGeneticaSantaReyesCalculos.MapearEncabezado(encabezado));
    }

    [Fact]
    public void La_plantilla_lleva_las_seis_columnas_en_orden()
    {
        Assert.Equal(
            new[] { "raza", "anio_guia", "edad", "prod_porcentaje", "retiro_ac_h", "gr_ave_dia_h" },
            GuiaGeneticaSantaReyesCalculos.ColumnasPlantilla);
    }

    /// <summary>Todo encabezado de la plantilla se reconoce a sí mismo (o el import no leería su propia plantilla).</summary>
    [Fact]
    public void La_plantilla_se_lee_a_si_misma()
    {
        foreach (var columna in GuiaGeneticaSantaReyesCalculos.ColumnasPlantilla)
        {
            Assert.Equal(columna, GuiaGeneticaSantaReyesCalculos.MapearEncabezado(columna));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Interpretación de una fila completa
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Una_fila_buena_sale_tipada_y_con_su_codigo()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila(
            "Babcock Brown", "2026", "18", "5.9", "0.0", "95.0");

        Assert.False(r.EsVacia);
        Assert.Null(r.Motivo);
        Assert.NotNull(r.Fila);
        Assert.Equal("Babcock Brown", r.Fila!.Raza);
        Assert.Equal("2026", r.Fila.AnioGuia);
        Assert.Equal(18, r.Fila.Edad);
        Assert.Equal("Babcock Brown202618", r.Fila.Codigo);
        Assert.Equal(5.9m, r.Fila.Metricas.ProdPorcentaje);
        Assert.Equal(0.0m, r.Fila.Metricas.RetiroAcH);
        Assert.Equal(95.0m, r.Fila.Metricas.GrAveDiaH);
    }

    /// <summary>
    /// La fila de la Criolla de la semana 101: producción en blanco. Tiene que entrar, con NULL.
    /// </summary>
    [Fact]
    public void La_fila_de_criolla_sin_produccion_entra_con_null()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila("Criolla", "2026", "101", "", "8.4", "108");

        Assert.NotNull(r.Fila);
        Assert.Null(r.Fila!.Metricas.ProdPorcentaje);
        Assert.Equal(8.4m, r.Fila.Metricas.RetiroAcH);
    }

    /// <summary>
    /// Excel arrastra filas en blanco al final de cualquier hoja editada a mano. No son errores: si
    /// lo fueran, todo import terminaría en «120 errores» y nadie encontraría los 2 reales.
    /// </summary>
    [Fact]
    public void Una_fila_totalmente_vacia_se_saltea_sin_error()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila(null, "", "   ", null, null, null);

        Assert.True(r.EsVacia);
        Assert.Null(r.Fila);
        Assert.Null(r.Motivo);
    }

    [Fact]
    public void Falta_la_raza_y_el_motivo_la_nombra()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila(null, "2026", "18", "5.9", "0.0", "95.0");

        Assert.False(r.EsVacia);
        Assert.Null(r.Fila);
        Assert.Contains("raza", r.Motivo);
    }

    [Fact]
    public void Faltan_los_tres_campos_clave_y_los_nombra_a_todos()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila(null, null, null, "5.9", null, null);

        Assert.Null(r.Fila);
        Assert.Contains("raza", r.Motivo);
        Assert.Contains("anio_guia", r.Motivo);
        Assert.Contains("edad", r.Motivo);
    }

    [Fact]
    public void Semana_cero_o_negativa_se_rechaza()
    {
        Assert.Null(GuiaGeneticaSantaReyesCalculos.InterpretarFila("Criolla", "2026", "0", null, null, null).Fila);
        Assert.Null(GuiaGeneticaSantaReyesCalculos.InterpretarFila("Criolla", "2026", "-3", null, null, null).Fila);
    }

    [Fact]
    public void Una_metrica_con_basura_rechaza_la_fila_nombrando_la_columna()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila("Criolla", "2026", "18", "n/a", "0.0", "95.0");

        Assert.Null(r.Fila);
        Assert.Contains("prod_porcentaje", r.Motivo);
    }

    /// <summary>
    /// Una fila con SÓLO una métrica cargada no es «vacía» — hay que decirle al usuario que le
    /// faltan raza/año/semana, no tragársela en silencio.
    /// </summary>
    [Fact]
    public void Una_fila_con_solo_una_metrica_no_cuenta_como_vacia()
    {
        var r = GuiaGeneticaSantaReyesCalculos.InterpretarFila(null, null, null, "95", null, null);

        Assert.False(r.EsVacia);
        Assert.NotNull(r.Motivo);
    }
}
