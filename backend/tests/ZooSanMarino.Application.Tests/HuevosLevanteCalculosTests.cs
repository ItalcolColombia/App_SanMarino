using System.Text.Json;
using Xunit;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Tests del cálculo puro de huevos en levante (semana 14+) y de su arrastre a producción.
/// Cubren: fórmula de semana (equivalencia con la canónica), gate fail-closed, aritmética de la
/// clasificadora, suma/delta (idempotencia), peso ponderado y round-trip de la marca en metadata.
/// </summary>
public class HuevosLevanteCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ── Semana de vida ────────────────────────────────────────────────────────────────────────
    // Mismo contrato que MovimientoAvesCalculosTests: el día del encaset es la SEMANA 1.

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    [InlineData(90, 13)]   // último día de la semana 13
    [InlineData(91, 14)]   // primer día de la semana 14 ← el borde del requerimiento
    [InlineData(97, 14)]   // último día de la semana 14
    [InlineData(98, 15)]
    [InlineData(175, 26)]  // semana 26 = inicio de producción
    public void SemanaVida_cuenta_desde_el_encaset_como_semana_1(int dias, int semanaEsperada)
    {
        Assert.Equal(semanaEsperada, HuevosLevanteCalculos.SemanaVida(Encaset.AddDays(dias), Encaset));
    }

    [Fact]
    public void SemanaVida_es_identica_a_la_formula_canonica_de_MovimientoAvesCalculos()
    {
        for (var dias = 0; dias <= 400; dias++)
        {
            var fecha = Encaset.AddDays(dias);
            Assert.Equal(
                MovimientoAvesCalculos.SemanaDesdeEncaset(fecha, Encaset),
                HuevosLevanteCalculos.SemanaVida(fecha, Encaset));
        }
    }

    [Fact]
    public void SemanaVida_ignora_la_hora_del_dia()
    {
        var fecha = Encaset.AddDays(91);
        Assert.Equal(14, HuevosLevanteCalculos.SemanaVida(fecha.AddHours(23).AddMinutes(59), Encaset));
        Assert.Equal(14, HuevosLevanteCalculos.SemanaVida(fecha, Encaset));
    }

    // ── Gate de captura ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void PermiteHuevos_false_en_semana_13_y_true_en_semana_14()
    {
        Assert.False(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(90)));
        Assert.True(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(91)));
    }

    [Fact]
    public void PermiteHuevos_true_en_cualquier_semana_posterior_a_la_14()
    {
        Assert.True(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(175)));
        Assert.True(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(400)));
    }

    [Fact]
    public void PermiteHuevos_es_fail_closed_sin_fecha_de_encaset()
    {
        Assert.False(HuevosLevanteCalculos.PermiteHuevos(null, Encaset.AddDays(200)));
    }

    [Fact]
    public void PermiteHuevos_false_si_la_fecha_de_registro_es_anterior_al_encaset()
    {
        Assert.False(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(-1)));
        Assert.False(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(-100)));
    }

    [Fact]
    public void SemanaMinima_es_14()
    {
        Assert.Equal(14, HuevosLevanteCalculos.SemanaMinimaHuevosLevante);
    }

    // ── Aritmética de la clasificadora ────────────────────────────────────────────────────────

    [Fact]
    public void Totales_e_incubables_replican_la_aritmetica_del_modal_de_produccion()
    {
        var c = new HuevosClasificacion(
            Limpio: 800, Tratado: 100,
            Sucio: 10, Deforme: 20, Blanco: 30, DobleYema: 40,
            Piso: 50, Pequeno: 60, Roto: 70, Desecho: 80, Otro: 90);

        Assert.Equal(900, c.Incubables);                       // 800 + 100
        Assert.Equal(900 + 450, c.Totales);                    // incubables + las 9 no incubables
    }

    [Fact]
    public void Clasificacion_en_cero_da_totales_cero()
    {
        Assert.Equal(0, HuevosClasificacion.Cero.Incubables);
        Assert.Equal(0, HuevosClasificacion.Cero.Totales);
        Assert.True(HuevosClasificacion.Cero.EsCero);
        Assert.False(HuevosClasificacion.Cero.AlgunoPositivo);
    }

    [Fact]
    public void AlgunoPositivo_detecta_una_sola_categoria_cargada()
    {
        Assert.True(new HuevosClasificacion(Otro: 1).AlgunoPositivo);
        Assert.True(new HuevosClasificacion(Limpio: 1).AlgunoPositivo);
        Assert.False(new HuevosClasificacion().AlgunoPositivo);
    }

    [Fact]
    public void Solo_no_incubables_no_suma_a_incubables()
    {
        var c = new HuevosClasificacion(Sucio: 5, Roto: 3);
        Assert.Equal(0, c.Incubables);
        Assert.Equal(8, c.Totales);
    }

    // ── Suma ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Sumar_acumula_categoria_por_categoria()
    {
        var a = new HuevosClasificacion(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        var b = new HuevosClasificacion(10, 20, 30, 40, 50, 60, 70, 80, 90, 100, 110);

        var s = HuevosLevanteCalculos.Sumar(a, b);

        Assert.Equal(new HuevosClasificacion(11, 22, 33, 44, 55, 66, 77, 88, 99, 110, 121), s);
        Assert.Equal(a.Totales + b.Totales, s.Totales);
        Assert.Equal(a.Incubables + b.Incubables, s.Incubables);
    }

    [Fact]
    public void Sumar_con_cero_es_neutro()
    {
        var a = new HuevosClasificacion(1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);
        Assert.Equal(a, HuevosLevanteCalculos.Sumar(a, HuevosClasificacion.Cero));
        Assert.Equal(a, HuevosLevanteCalculos.Sumar(HuevosClasificacion.Cero, a));
    }

    // ── Delta (idempotencia del arrastre) ─────────────────────────────────────────────────────

    [Fact]
    public void Delta_de_valores_iguales_es_cero_arrastrar_dos_veces_no_duplica()
    {
        var acumulado = new HuevosClasificacion(800, 100, 10, 20, 30, 40, 50, 60, 70, 80, 90);
        Assert.True(HuevosLevanteCalculos.Delta(acumulado, acumulado).EsCero);
    }

    [Fact]
    public void Delta_arrastra_solo_lo_que_falta_cuando_se_agregaron_huevos()
    {
        var aplicado = new HuevosClasificacion(Limpio: 100, Tratado: 50);
        var nuevo = new HuevosClasificacion(Limpio: 180, Tratado: 50, Sucio: 5);

        var d = HuevosLevanteCalculos.Delta(nuevo, aplicado);

        Assert.Equal(new HuevosClasificacion(Limpio: 80, Sucio: 5), d);
        Assert.Equal(85, d.Totales);
    }

    [Fact]
    public void Delta_es_negativo_si_se_borraron_huevos_en_levante_no_se_clampea()
    {
        var aplicado = new HuevosClasificacion(Limpio: 200);
        var nuevo = new HuevosClasificacion(Limpio: 120);

        var d = HuevosLevanteCalculos.Delta(nuevo, aplicado);

        Assert.Equal(-80, d.Limpio);
        Assert.Equal(-80, d.Totales);
        Assert.False(d.EsCero);
    }

    [Fact]
    public void Aplicado_mas_delta_reconstruye_el_acumulado_real()
    {
        var aplicado = new HuevosClasificacion(100, 50, 1, 2, 3, 4, 5, 6, 7, 8, 9);
        var real = new HuevosClasificacion(400, 60, 1, 2, 3, 9, 5, 6, 7, 8, 9);

        var reconstruido = HuevosLevanteCalculos.Sumar(aplicado, HuevosLevanteCalculos.Delta(real, aplicado));

        Assert.Equal(real, reconstruido);
    }

    // ── Peso ponderado ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void PesoHuevoPonderado_pondera_por_cantidad_de_huevos()
    {
        var peso = HuevosLevanteCalculos.PesoHuevoPonderado(new (double?, int)[] { (60.0, 100), (70.0, 300) });

        // (60*100 + 70*300) / 400 = 27000/400 = 67.5  (el promedio simple daría 65)
        Assert.NotNull(peso);
        Assert.Equal(67.5, peso!.Value, 6);
    }

    [Fact]
    public void PesoHuevoPonderado_null_si_no_hay_dias()
    {
        Assert.Null(HuevosLevanteCalculos.PesoHuevoPonderado(Array.Empty<(double?, int)>()));
    }

    [Fact]
    public void PesoHuevoPonderado_null_si_todos_los_dias_tienen_cero_huevos_sin_division_por_cero()
    {
        Assert.Null(HuevosLevanteCalculos.PesoHuevoPonderado(new (double?, int)[] { (60.0, 0), (70.0, 0) }));
    }

    [Fact]
    public void PesoHuevoPonderado_ignora_dias_sin_peso_informado()
    {
        var peso = HuevosLevanteCalculos.PesoHuevoPonderado(new (double?, int)[] { (null, 1000), (65.0, 200) });

        Assert.NotNull(peso);
        Assert.Equal(65.0, peso!.Value, 6);
    }

    [Fact]
    public void PesoHuevoPonderado_ignora_dias_con_totales_negativos()
    {
        var peso = HuevosLevanteCalculos.PesoHuevoPonderado(new (double?, int)[] { (60.0, -50), (70.0, 100) });

        Assert.NotNull(peso);
        Assert.Equal(70.0, peso!.Value, 6);
    }

    // ── Marca de arrastre en metadata ─────────────────────────────────────────────────────────

    [Fact]
    public void TieneMarcaArrastre_false_para_metadata_null_o_sin_la_clave()
    {
        Assert.False(HuevosLevanteCalculos.TieneMarcaArrastre(null));
        Assert.False(HuevosLevanteCalculos.TieneMarcaArrastre(JsonDocument.Parse("""{"itemsHembras":[]}""")));
        Assert.False(HuevosLevanteCalculos.TieneMarcaArrastre(JsonDocument.Parse("""[1,2,3]""")));
    }

    [Fact]
    public void EscribirMarcaArrastre_y_LeerArrastreAplicado_hacen_round_trip()
    {
        var aplicado = new HuevosClasificacion(800, 100, 10, 20, 30, 40, 50, 60, 70, 80, 90);

        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, aplicado, lotePosturaLevanteId: 12,
            fechaArrastre: new DateTime(2026, 2, 24, 12, 0, 0, DateTimeKind.Utc));

        Assert.True(HuevosLevanteCalculos.TieneMarcaArrastre(md));
        Assert.Equal(aplicado, HuevosLevanteCalculos.LeerArrastreAplicado(md));
    }

    [Fact]
    public void EscribirMarcaArrastre_conserva_verbatim_las_demas_claves_del_metadata()
    {
        var original = JsonDocument.Parse("""
        {
          "itemsHembras": [{ "tipoItem": "alimento", "catalogItemId": 7, "cantidad": 12.5, "unidad": "kg" }],
          "consumoOriginalHembras": 12.5,
          "unidadConsumoOriginalHembras": "kg",
          "huevoItems": [{ "catalogItemId": 678, "cantidad": 1200 }]
        }
        """);

        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(original, new HuevosClasificacion(Limpio: 5),
            lotePosturaLevanteId: 1, fechaArrastre: Encaset);

        var root = md.RootElement;
        Assert.Equal(12.5, root.GetProperty("consumoOriginalHembras").GetDouble());
        Assert.Equal("kg", root.GetProperty("unidadConsumoOriginalHembras").GetString());
        Assert.Equal(7, root.GetProperty("itemsHembras")[0].GetProperty("catalogItemId").GetInt32());
        Assert.Equal(678, root.GetProperty("huevoItems")[0].GetProperty("catalogItemId").GetInt32());
        Assert.True(HuevosLevanteCalculos.TieneMarcaArrastre(md));
    }

    [Fact]
    public void EscribirMarcaArrastre_reemplaza_la_marca_previa_sin_duplicarla()
    {
        var primera = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Limpio: 100),
            lotePosturaLevanteId: 3, fechaArrastre: Encaset);

        var segunda = HuevosLevanteCalculos.EscribirMarcaArrastre(primera, new HuevosClasificacion(Limpio: 250),
            lotePosturaLevanteId: 3, fechaArrastre: Encaset);

        Assert.Equal(new HuevosClasificacion(Limpio: 250), HuevosLevanteCalculos.LeerArrastreAplicado(segunda));

        var marcas = 0;
        foreach (var p in segunda.RootElement.EnumerateObject())
            if (p.Name == HuevosLevanteCalculos.MetadataKeyArrastre) marcas++;
        Assert.Equal(1, marcas);
    }

    [Fact]
    public void Marca_guarda_lote_fecha_version_y_los_derivados_para_auditar()
    {
        var aplicado = new HuevosClasificacion(Limpio: 800, Tratado: 100, Sucio: 50);

        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, aplicado, lotePosturaLevanteId: 42,
            fechaArrastre: new DateTime(2026, 2, 24, 17, 30, 0, DateTimeKind.Utc));

        var marca = md.RootElement.GetProperty(HuevosLevanteCalculos.MetadataKeyArrastre);
        Assert.Equal(42, marca.GetProperty("lotePosturaLevanteId").GetInt32());
        Assert.Equal("2026-02-24", marca.GetProperty("fecha").GetString());
        Assert.Equal(1, marca.GetProperty("version").GetInt32());
        Assert.Equal(900, marca.GetProperty("aplicado").GetProperty("huevoInc").GetInt32());
        Assert.Equal(950, marca.GetProperty("aplicado").GetProperty("huevoTot").GetInt32());
    }

    [Fact]
    public void LeerArrastreAplicado_devuelve_cero_si_la_marca_esta_mal_formada()
    {
        var clave = HuevosLevanteCalculos.MetadataKeyArrastre;

        Assert.Equal(HuevosClasificacion.Cero,
            HuevosLevanteCalculos.LeerArrastreAplicado(JsonDocument.Parse("{\"" + clave + "\":{}}")));

        Assert.Equal(HuevosClasificacion.Cero,
            HuevosLevanteCalculos.LeerArrastreAplicado(JsonDocument.Parse("{\"" + clave + "\":{\"aplicado\":\"nope\"}}")));

        Assert.Equal(HuevosClasificacion.Cero, HuevosLevanteCalculos.LeerArrastreAplicado(null));
    }

    // ── CopiarMarcaArrastre (merge del seguimiento del mismo día) ─────────────────────────────

    [Fact]
    public void CopiarMarcaArrastre_traslada_la_marca_al_metadata_reconstruido_del_request()
    {
        var aplicado = new HuevosClasificacion(Limpio: 900, Tratado: 100, Sucio: 40);
        var origen = HuevosLevanteCalculos.EscribirMarcaArrastre(null, aplicado, 7, Encaset);

        // El metadata que arma el request del usuario NO tiene la marca…
        var delRequest = JsonDocument.Parse("""{"itemsHembras":[{"catalogItemId":5,"cantidad":10.0}]}""");
        Assert.False(HuevosLevanteCalculos.TieneMarcaArrastre(delRequest));

        var final = HuevosLevanteCalculos.CopiarMarcaArrastre(delRequest, origen);

        // …y después de copiar, conserva lo del request Y la marca (⇒ el arrastre sigue idempotente).
        Assert.True(HuevosLevanteCalculos.TieneMarcaArrastre(final));
        Assert.Equal(aplicado, HuevosLevanteCalculos.LeerArrastreAplicado(final));
        Assert.Equal(5, final!.RootElement.GetProperty("itemsHembras")[0].GetProperty("catalogItemId").GetInt32());
    }

    [Fact]
    public void CopiarMarcaArrastre_no_toca_el_destino_si_el_origen_no_tiene_marca()
    {
        var destino = JsonDocument.Parse("""{"consumoOriginalHembras":9.5}""");

        var final = HuevosLevanteCalculos.CopiarMarcaArrastre(destino, null);

        Assert.Same(destino, final);
        Assert.False(HuevosLevanteCalculos.TieneMarcaArrastre(final));
    }

    [Fact]
    public void CopiarMarcaArrastre_sobre_destino_null_deja_solo_la_marca()
    {
        var origen = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Roto: 3), 1, Encaset);

        var final = HuevosLevanteCalculos.CopiarMarcaArrastre(null, origen);

        Assert.NotNull(final);
        Assert.Equal(new HuevosClasificacion(Roto: 3), HuevosLevanteCalculos.LeerArrastreAplicado(final));
    }

    // ── Ventana de merge (la regla «un registro por día» se conserva) ─────────────────────────

    [Fact]
    public void Ventana_de_merge_nace_abierta_con_el_primer_arrastre()
    {
        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Limpio: 10), 1, Encaset);
        Assert.True(HuevosLevanteCalculos.PermiteMergeSeguimiento(md));
    }

    [Fact]
    public void Ventana_de_merge_se_cierra_al_registrar_el_seguimiento_del_dia()
    {
        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Limpio: 10), 1, Encaset);

        var cerrada = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(md);

        Assert.False(HuevosLevanteCalculos.PermiteMergeSeguimiento(cerrada));
        // …pero la marca y el acumulado siguen ahí (el arrastre sigue siendo idempotente).
        Assert.True(HuevosLevanteCalculos.TieneMarcaArrastre(cerrada));
        Assert.Equal(new HuevosClasificacion(Limpio: 10), HuevosLevanteCalculos.LeerArrastreAplicado(cerrada));
    }

    [Fact]
    public void Re_arrastrar_no_reabre_la_ventana_de_merge_ya_cerrada()
    {
        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Limpio: 10), 1, Encaset);
        var cerrada = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(md);

        // Se editaron huevos en levante y se vuelve a arrastrar sobre la MISMA fila.
        var reArrastrada = HuevosLevanteCalculos.EscribirMarcaArrastre(
            cerrada, new HuevosClasificacion(Limpio: 30), 1, Encaset);

        Assert.False(HuevosLevanteCalculos.PermiteMergeSeguimiento(reArrastrada));
        Assert.Equal(new HuevosClasificacion(Limpio: 30), HuevosLevanteCalculos.LeerArrastreAplicado(reArrastrada));
    }

    [Fact]
    public void Sin_marca_no_hay_merge_el_400_por_duplicado_se_conserva()
    {
        Assert.False(HuevosLevanteCalculos.PermiteMergeSeguimiento(null));
        Assert.False(HuevosLevanteCalculos.PermiteMergeSeguimiento(JsonDocument.Parse("""{"itemsHembras":[]}""")));
    }

    [Fact]
    public void MarcarSeguimientoRegistrado_sin_marca_no_altera_el_metadata()
    {
        var sinMarca = JsonDocument.Parse("""{"consumoOriginalHembras":3.5}""");
        Assert.Same(sinMarca, HuevosLevanteCalculos.MarcarSeguimientoRegistrado(sinMarca));
        Assert.Null(HuevosLevanteCalculos.MarcarSeguimientoRegistrado(null));
    }

    [Fact]
    public void CopiarMarcaArrastre_traslada_tambien_el_estado_de_la_ventana()
    {
        var abierta = HuevosLevanteCalculos.EscribirMarcaArrastre(null, new HuevosClasificacion(Limpio: 5), 1, Encaset);
        var cerrada = HuevosLevanteCalculos.MarcarSeguimientoRegistrado(abierta);

        var copiada = HuevosLevanteCalculos.CopiarMarcaArrastre(
            JsonDocument.Parse("""{"itemsHembras":[]}"""), cerrada);

        Assert.False(HuevosLevanteCalculos.PermiteMergeSeguimiento(copiada));
    }

    [Fact]
    public void Delta_sobre_una_marca_recien_escrita_es_cero_idempotencia_end_to_end()
    {
        var acumuladoLevante = new HuevosClasificacion(1200, 300, 15, 25, 35, 45, 55, 65, 75, 85, 95);

        var md = HuevosLevanteCalculos.EscribirMarcaArrastre(null, acumuladoLevante, 9, Encaset);
        var yaAplicado = HuevosLevanteCalculos.LeerArrastreAplicado(md);

        Assert.True(HuevosLevanteCalculos.Delta(acumuladoLevante, yaAplicado).EsCero);
    }
}
