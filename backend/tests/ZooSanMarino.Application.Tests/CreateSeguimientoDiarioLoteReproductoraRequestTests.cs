using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Mapeo request → DTO del seguimiento diario reproductora engorde. Cubre el fix del consumo de
/// machos: enviado como escalar (sin ítems de inventario) se perdía porque el total de ítems vacío
/// devolvía 0 (HasValue=true) y el fallback de <c>ConsumoMachos</c> nunca disparaba — el usuario
/// registraba consumo H y M y la tabla solo mostraba el de hembras.
/// </summary>
public class CreateSeguimientoDiarioLoteReproductoraRequestTests
{
    private static CreateSeguimientoDiarioLoteReproductoraRequest Base() => new()
    {
        LoteId = 1,
        FechaRegistro = new DateTime(2026, 7, 20),
        TipoAlimento = "Iniciador"
    };

    [Fact]
    public void ToDto_ConsumoMachosEscalarSinItems_SePersiste()
    {
        var req = Base();
        req.ConsumoHembras = 8;
        req.ConsumoMachos = 10;

        var dto = req.ToDto();

        Assert.Equal(8, dto.ConsumoKgHembras);
        Assert.Equal(10, dto.ConsumoKgMachos);
    }

    [Fact]
    public void ToDto_ConsumoMachosPorItems_UsaElTotalDeItems()
    {
        var req = Base();
        req.ItemsMachos = new List<ItemSeguimientoDto>
        {
            new() { TipoItem = "alimento", CatalogItemId = 7, Cantidad = 5, Unidad = "kg" }
        };

        var dto = req.ToDto();

        Assert.Equal(5, dto.ConsumoKgMachos);
    }

    [Fact]
    public void ToDto_SinConsumoMachos_QuedaNull()
    {
        var dto = Base().ToDto();
        Assert.Null(dto.ConsumoKgMachos);
    }

    [Fact]
    public void ToDto_ConsumoMachosEnGramos_ConvierteAKg()
    {
        var req = Base();
        req.ConsumoMachos = 2500;
        req.UnidadConsumoMachos = "g";

        var dto = req.ToDto();

        Assert.Equal(2.5, dto.ConsumoKgMachos);
    }

    [Fact]
    public void ToDto_ConsumoHembrasEscalar_SigueFuncionando()
    {
        var req = Base();
        req.ConsumoHembras = 12.5;

        var dto = req.ToDto();

        Assert.Equal(12.5, dto.ConsumoKgHembras);
    }
}
