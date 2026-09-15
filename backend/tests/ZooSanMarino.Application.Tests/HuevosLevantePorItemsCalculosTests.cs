using System.Text.Json;
using Xunit;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Huevos en LEVANTE por los tipos declarados del lote (capacitación Santa Reyes, 14-sep-2026):
/// semana mínima configurable por empresa, modo de captura, suma/delta por ítem y marca de arrastre
/// con <c>aplicadoItems</c>. Contrato de no regresión: sin semana configurada y sin ítems, todo es
/// idéntico al comportamiento previo.
/// </summary>
public class HuevosLevantePorItemsCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static HuevoItemSeguimientoDto Item(int id, int cantidad, string? tipoHuevo = "Primera") =>
        new(id, Codigo: id.ToString(), Nombre: $"HUEVO {id}", TipoHuevo: tipoHuevo, Cantidad: cantidad, Um: "UND");

    // ── Semana mínima ─────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(-30)]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(118)]
    [InlineData(119)]
    [InlineData(400)]
    public void PermiteHuevos_sin_semana_minima_es_identico_a_la_version_de_siempre(int dias)
    {
        var fecha = Encaset.AddDays(dias);
        Assert.Equal(
            HuevosLevanteCalculos.PermiteHuevos(Encaset, fecha),
            HuevosLevanteCalculos.PermiteHuevos(Encaset, fecha, null));
    }

    [Theory]
    [InlineData(0, false)]    // semana 1
    [InlineData(118, false)]  // último día de la semana 17
    [InlineData(119, true)]   // primer día de la semana 18 ← el valor de Santa Reyes
    [InlineData(300, true)]
    public void PermiteHuevos_con_semana_18_habilita_desde_el_primer_dia_de_la_semana_18(int dias, bool esperado)
    {
        Assert.Equal(esperado, HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(dias), 18));
    }

    [Fact]
    public void PermiteHuevos_con_semana_minima_sigue_rechazando_la_fecha_anterior_al_encaset()
    {
        Assert.False(HuevosLevanteCalculos.PermiteHuevos(Encaset, Encaset.AddDays(-1), 18));
    }

    [Fact]
    public void PermiteHuevos_con_semana_minima_y_sin_encaset_permite()
    {
        // Sin encaset no hay semana evaluable: bloquear dejaría un 400 sin remedio.
        Assert.True(HuevosLevanteCalculos.PermiteHuevos(null, Encaset.AddDays(10), 18));
    }

    [Fact]
    public void MensajeFechaAnteriorAlEncaset_conserva_el_texto_historico()
    {
        Assert.Equal(
            "Los huevos no pueden registrarse con una fecha anterior al encasetamiento del lote.",
            HuevosLevanteCalculos.MensajeFechaAnteriorAlEncaset);
    }

    [Fact]
    public void MensajeAntesDeSemana_dice_la_semana_minima_y_la_del_registro()
    {
        var msg = HuevosLevanteCalculos.MensajeAntesDeSemana(18, 17);
        Assert.Contains("desde la semana 18", msg);
        Assert.Contains("de la semana 17", msg);
    }

    // ── Modo de captura ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(false, false, ModoHuevosLevante.Ninguno)]
    [InlineData(false, true, ModoHuevosLevante.Ninguno)]
    [InlineData(true, false, ModoHuevosLevante.Clasificadora)]   // Sanmarino / Demo
    [InlineData(true, true, ModoHuevosLevante.PorItems)]         // Santa Reyes
    public void ResolverModo_combina_captura_y_clasificacion_por_items(bool captura, bool porItems, ModoHuevosLevante esperado)
    {
        Assert.Equal(esperado, HuevosLevanteCalculos.ResolverModo(captura, porItems));
    }

    // ── SumarPorItem ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void SumarPorItem_null_y_null_da_vacio()
    {
        Assert.Empty(HuevoItemsCalculos.SumarPorItem(null, null));
    }

    [Fact]
    public void SumarPorItem_suma_los_repetidos_y_agrega_los_nuevos_en_orden_de_aparicion()
    {
        var a = new[] { Item(678, 100), Item(679, 20, "Pnc") };
        var b = new[] { Item(679, 5, "Pnc"), Item(700, 7) };

        var r = HuevoItemsCalculos.SumarPorItem(a, b);

        Assert.Equal(new[] { Item(678, 100), Item(679, 25, "Pnc"), Item(700, 7) }, r);
    }

    [Fact]
    public void SumarPorItem_completa_las_etiquetas_vacias_con_las_de_la_otra_lista()
    {
        var sinEtiquetas = new HuevoItemSeguimientoDto(678, Cantidad: 10);

        var r = HuevoItemsCalculos.SumarPorItem(new[] { sinEtiquetas }, new[] { Item(678, 5) });

        Assert.Equal(Item(678, 15), Assert.Single(r));
    }

    [Fact]
    public void SumarPorItem_descarta_los_ids_invalidos()
    {
        var r = HuevoItemsCalculos.SumarPorItem(new[] { new HuevoItemSeguimientoDto(0, Cantidad: 5), Item(678, 1) }, null);
        Assert.Equal(678, Assert.Single(r).CatalogItemId);
    }

    // ── DeltaPorItem ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void DeltaPorItem_sin_aplicado_devuelve_todo()
    {
        var nuevo = new[] { Item(678, 100), Item(679, 20, "Pnc") };
        Assert.Equal(nuevo, HuevoItemsCalculos.DeltaPorItem(nuevo, null));
    }

    [Fact]
    public void DeltaPorItem_igual_a_lo_aplicado_es_vacio_idempotente()
    {
        var nuevo = new[] { Item(678, 100), Item(679, 20, "Pnc") };
        Assert.Empty(HuevoItemsCalculos.DeltaPorItem(nuevo, nuevo));
    }

    [Fact]
    public void DeltaPorItem_no_clampea_los_negativos()
    {
        var r = HuevoItemsCalculos.DeltaPorItem(
            new[] { Item(678, 80) },
            new[] { Item(678, 100), Item(679, 10, "Pnc") });

        Assert.Equal(new[] { Item(678, -20), Item(679, -10, "Pnc") }, r);
    }

    // ── Marca de arrastre ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void EscribirMarcaArrastre_sin_items_no_agrega_aplicadoItems_ni_cambia_huevoTot()
    {
        var doc = HuevosLevanteCalculos.EscribirMarcaArrastre(
            null, new HuevosClasificacion(Limpio: 10, Sucio: 2), 5, Encaset);

        var marca = doc.RootElement.GetProperty(HuevosLevanteCalculos.MetadataKeyArrastre);
        Assert.False(marca.TryGetProperty("aplicadoItems", out _));
        Assert.Equal(12, marca.GetProperty("aplicado").GetProperty("huevoTot").GetInt32());
        Assert.Empty(HuevosLevanteCalculos.LeerArrastreAplicadoItems(doc));
    }

    [Fact]
    public void EscribirMarcaArrastre_con_items_los_guarda_y_los_suma_a_huevoTot()
    {
        var items = new[] { Item(678, 100), Item(679, 20, "Pnc") };

        var doc = HuevosLevanteCalculos.EscribirMarcaArrastre(null, HuevosClasificacion.Cero, 5, Encaset, items);

        var aplicado = doc.RootElement.GetProperty(HuevosLevanteCalculos.MetadataKeyArrastre).GetProperty("aplicado");
        Assert.Equal(120, aplicado.GetProperty("huevoTot").GetInt32());
        Assert.Equal(0, aplicado.GetProperty("huevoInc").GetInt32());
        Assert.Equal(items, HuevosLevanteCalculos.LeerArrastreAplicadoItems(doc));
        Assert.True(HuevosLevanteCalculos.LeerArrastreAplicado(doc).EsCero);
    }

    [Fact]
    public void EscribirMarcaArrastre_conserva_el_desglose_huevoItems_de_la_fila()
    {
        var items = new[] { Item(678, 50) };
        var metadata = HuevoItemsCalculos.EscribirEnMetadata(null, items);

        var doc = HuevosLevanteCalculos.EscribirMarcaArrastre(metadata, HuevosClasificacion.Cero, 5, Encaset, items);

        Assert.Equal(items, HuevoItemsCalculos.LeerDeMetadata(doc.RootElement));
    }

    [Fact]
    public void TotalArrastrado_suma_clasificadora_e_items()
    {
        var doc = HuevosLevanteCalculos.EscribirMarcaArrastre(
            null, new HuevosClasificacion(Limpio: 7), 5, Encaset, new[] { Item(678, 30) });

        Assert.Equal(37, HuevosLevanteCalculos.TotalArrastrado(doc));
    }

    [Fact]
    public void TotalArrastrado_sin_items_es_el_total_de_la_clasificadora_de_siempre()
    {
        var doc = HuevosLevanteCalculos.EscribirMarcaArrastre(
            null, new HuevosClasificacion(Limpio: 7, Roto: 3), 5, Encaset);

        Assert.Equal(HuevosLevanteCalculos.LeerArrastreAplicado(doc).Totales, HuevosLevanteCalculos.TotalArrastrado(doc));
        Assert.Equal(0, HuevosLevanteCalculos.TotalArrastrado(null));
    }
}
