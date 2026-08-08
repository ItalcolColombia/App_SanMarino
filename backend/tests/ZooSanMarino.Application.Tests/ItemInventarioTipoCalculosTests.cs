using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.ItemInventarioTipoCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Criterio único de tipo de ítem de inventario. Reemplaza al que leía
/// <c>catalogo_items.metadata-&gt;&gt;'type_item'</c> (modelo viejo, NULL en el 80 % del catálogo) y
/// comparaba distinguiendo mayúsculas — combinación que le costaba al Reporte Contable 257
/// movimientos de alimento.
/// </summary>
public class ItemInventarioTipoCalculosTests
{
    [Theory]
    [InlineData("alimento")]
    [InlineData("Alimento")]   // así están cargadas filas reales del catálogo (1 por empresa)
    [InlineData("ALIMENTO")]
    [InlineData("  alimento  ")]
    [InlineData("\tAlimento\n")]
    public void EsTipoAlimento_ToleraCapitalizacionYEspacios(string tipo)
    {
        Assert.True(EsTipoAlimento(tipo));
    }

    [Theory]
    [InlineData("vacuna")]
    [InlineData("medicamento")]
    [InlineData("insumo")]
    [InlineData("desinfectante")]
    [InlineData("empaque")]
    [InlineData("huevo")]
    [InlineData("combustible")]
    [InlineData("materia_prima")]
    [InlineData("mantenimiento")]
    [InlineData("alimentos")]     // no es el tipo canónico: no debe colar por prefijo
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EsTipoAlimento_RechazaTodoLoDemas(string? tipo)
    {
        Assert.False(EsTipoAlimento(tipo));
    }

    [Theory]
    // El tipo del MOVIMIENTO manda: preserva la historia si el ítem del catálogo cambia de tipo.
    [InlineData("alimento", "vacuna",   "alimento")]
    [InlineData("vacuna",   "alimento", "vacuna")]
    // Sin tipo en el movimiento, respalda el catálogo (patrón `m.ItemType ?? m.CatalogItem.ItemType`).
    [InlineData(null,       "alimento", "alimento")]
    [InlineData("",         "alimento", "alimento")]
    [InlineData("   ",      "alimento", "alimento")]
    [InlineData(null,       null,       null)]
    public void TipoEfectivo_ElMovimientoMandaYElCatalogoRespalda(
        string? enMovimiento, string? enCatalogo, string? esperado)
    {
        Assert.Equal(esperado, TipoEfectivo(enMovimiento, enCatalogo));
    }

    [Theory]
    // El caso que producía el bug: el catálogo tiene el tipo correcto en la columna y el movimiento
    // también, pero el criterio viejo miraba el jsonb vacío y no reconocía ninguno de los dos.
    [InlineData("alimento", null,       true)]
    [InlineData(null,       "alimento", true)]
    [InlineData("Alimento", "Alimento", true)]
    [InlineData("vacuna",   "alimento", false)]  // el ítem es de alimento pero ESTE movimiento no
    [InlineData(null,       "vacuna",   false)]
    [InlineData(null,       null,       false)]
    public void EsMovimientoDeAlimento_CombinaAmbasReglas(
        string? enMovimiento, string? enCatalogo, bool esperado)
    {
        Assert.Equal(esperado, EsMovimientoDeAlimento(enMovimiento, enCatalogo));
    }

    [Fact]
    public void ElTipoCanonicoEsElQueEscribeElServicePorDefecto()
    {
        // CatalogItemService.CreateAsync usa este literal como default de ItemType.
        Assert.Equal("alimento", TipoAlimento);
        Assert.True(EsTipoAlimento(TipoAlimento));
    }
}
