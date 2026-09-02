using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Resumen Primera / Pnc / Otros de un registro diario.
///
/// <para>Espejo de <c>resumir-huevo-items-por-tipo.funcion.ts</c>: cada caso de acá replica una
/// rama de esa función (tipo conocido, desconocido, cantidad ≤ 0, lista vacía/nula).</para>
/// </summary>
public class HuevoItemsResumenCalculosTests
{
    private static HuevoItemSeguimientoDto Item(string? tipo, int cantidad, int id = 1) =>
        new(CatalogItemId: id, TipoHuevo: tipo, Cantidad: cantidad);

    [Fact]
    public void Suma_por_categoria_conocida()
    {
        var r = HuevoItemsResumenCalculos.Resumir(new[]
        {
            Item("Primera", 100, 1),
            Item("Primera", 50, 2),
            Item("Pnc", 30, 3)
        });

        Assert.Equal(150, r.Primera);
        Assert.Equal(30, r.Pnc);
        Assert.Equal(0, r.Otros);
        Assert.Equal(180, r.Total);
    }

    [Theory]
    [InlineData("primera")]
    [InlineData("PRIMERA")]
    [InlineData("  Primera  ")]
    public void El_tipo_se_compara_sin_espacios_ni_mayusculas(string tipo)
    {
        Assert.Equal(10, HuevoItemsResumenCalculos.Resumir(new[] { Item(tipo, 10) }).Primera);
    }

    /// <summary>Un tipo que no es Primera ni Pnc NO se suma a ninguno de los dos, pero sí al total.</summary>
    [Theory]
    [InlineData("Sin categoría")]
    [InlineData("Doble yema")]
    [InlineData("")]
    [InlineData(null)]
    public void Tipo_desconocido_va_a_Otros_y_cuenta_en_el_total(string? tipo)
    {
        var r = HuevoItemsResumenCalculos.Resumir(new[] { Item(tipo, 25) });

        Assert.Equal(0, r.Primera);
        Assert.Equal(0, r.Pnc);
        Assert.Equal(25, r.Otros);
        Assert.Equal(25, r.Total);
        Assert.True(r.TieneOtros);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Cantidad_no_positiva_se_descarta(int cantidad)
    {
        var r = HuevoItemsResumenCalculos.Resumir(new[] { Item("Primera", cantidad) });

        Assert.Equal(0, r.Primera);
        Assert.Equal(0, r.Total);
    }

    [Fact]
    public void Lista_vacia_o_nula_da_todo_en_cero()
    {
        Assert.Equal(0, HuevoItemsResumenCalculos.Resumir(Array.Empty<HuevoItemSeguimientoDto>()).Total);
        Assert.Equal(0, HuevoItemsResumenCalculos.Resumir(null).Total);
    }

    [Fact]
    public void Sin_items_de_tipo_desconocido_no_hay_columna_Otros()
    {
        var r = HuevoItemsResumenCalculos.Resumir(new[] { Item("Primera", 10), Item("Pnc", 5) });

        Assert.False(r.TieneOtros);
    }

    // ── Suma de varios registros (semana / galpón / consolidado) ────────────────────────────────

    [Fact]
    public void Sumar_acumula_las_tres_categorias()
    {
        var r = HuevoItemsResumenCalculos.Sumar(new[]
        {
            new ResumenHuevoPorTipo(100, 20, 3),
            new ResumenHuevoPorTipo(50, 10, 0),
            new ResumenHuevoPorTipo(25, 5, 2)
        });

        Assert.Equal(175, r.Primera);
        Assert.Equal(35, r.Pnc);
        Assert.Equal(5, r.Otros);
        Assert.Equal(215, r.Total);
    }

    [Fact]
    public void Sumar_de_lista_vacia_o_nula_da_cero()
    {
        Assert.Equal(0, HuevoItemsResumenCalculos.Sumar(Array.Empty<ResumenHuevoPorTipo>()).Total);
        Assert.Equal(0, HuevoItemsResumenCalculos.Sumar(null).Total);
    }
}
