using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Recepción de tránsito: resolución de ubicaciones de destino.
/// El camino SIN distribución debe seguir comportándose (y fallando) exactamente como antes.
/// </summary>
public class InventarioGestionRecepcionDistribucionCalculosTests
{
    private static (IReadOnlyList<InventarioGestionRecepcionDistribucionCalculos.Destino> Destinos, string? Error) Resolver(
        IReadOnlyList<InventarioGestionRecepcionDestinoDto>? distribucion = null,
        string? toNucleoId = null,
        string? toGalponId = null,
        bool usaUbicacion = true,
        decimal cantidadTransito = 1000m) =>
        InventarioGestionRecepcionDistribucionCalculos.Resolver(distribucion, toNucleoId, toGalponId, usaUbicacion, cantidadTransito);

    // ─── Camino clásico (sin distribución) ────────────────────────────────────

    [Fact]
    public void SinDistribucion_ConNucleoYGalpon_DevuelveUnDestinoConLaCantidadCompleta()
    {
        var (destinos, error) = Resolver(toNucleoId: " N1 ", toGalponId: " G1 ", usaUbicacion: true, cantidadTransito: 1000m);

        Assert.Null(error);
        var destino = Assert.Single(destinos);
        Assert.Equal("N1", destino.NucleoId);
        Assert.Equal("G1", destino.GalponId);
        Assert.Equal(1000m, destino.Quantity);
    }

    [Theory]
    [InlineData("N1", null)]
    [InlineData(null, "G1")]
    [InlineData(null, null)]
    [InlineData("N1", "   ")]
    public void SinDistribucion_AlimentoPorGalponSinUbicacionCompleta_MensajeHistorico(string? nucleoId, string? galponId)
    {
        var (destinos, error) = Resolver(toNucleoId: nucleoId, toGalponId: galponId, usaUbicacion: true);

        Assert.Empty(destinos);
        Assert.Equal("Para alimento debe indicar Núcleo y Galpón de recepción en la granja destino.", error);
    }

    [Fact]
    public void SinDistribucion_NivelGranja_DevuelveUnDestinoSinUbicacion()
    {
        var (destinos, error) = Resolver(usaUbicacion: false, cantidadTransito: 250.5m);

        Assert.Null(error);
        var destino = Assert.Single(destinos);
        Assert.Null(destino.NucleoId);
        Assert.Null(destino.GalponId);
        Assert.Equal(250.5m, destino.Quantity);
    }

    [Fact]
    public void SinDistribucion_NivelGranjaConUbicacion_MensajeHistorico()
    {
        var (destinos, error) = Resolver(toNucleoId: "N1", toGalponId: "G1", usaUbicacion: false);

        Assert.Empty(destinos);
        Assert.Equal("La recepción es solo a nivel granja (sin Núcleo/Galpón).", error);
    }

    [Fact]
    public void DistribucionSoloConFilasVacias_CaeAlCaminoClasico()
    {
        var vacias = new[]
        {
            new InventarioGestionRecepcionDestinoDto(null, null, 0m),
            new InventarioGestionRecepcionDestinoDto("  ", "  ", 0m)
        };

        var (destinos, error) = Resolver(vacias, toNucleoId: "N1", toGalponId: "G1", usaUbicacion: true, cantidadTransito: 800m);

        Assert.Null(error);
        var destino = Assert.Single(destinos);
        Assert.Equal("G1", destino.GalponId);
        Assert.Equal(800m, destino.Quantity);
    }

    // ─── Camino distribuido ───────────────────────────────────────────────────

    [Fact]
    public void Distribucion_SumaExacta_DevuelveUnDestinoPorGalpon()
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto(" N1 ", " G1 ", 400m),
            new InventarioGestionRecepcionDestinoDto("N1", "G2", 350m),
            new InventarioGestionRecepcionDestinoDto("N2", "G3", 250m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(3, destinos.Count);
        Assert.Equal(new[] { "G1", "G2", "G3" }, destinos.Select(d => d.GalponId));
        Assert.Equal(new[] { "N1", "N1", "N2" }, destinos.Select(d => d.NucleoId));
        Assert.Equal(1000m, destinos.Sum(d => d.Quantity));
    }

    [Fact]
    public void Distribucion_IgnoraFilasVaciasIntercaladas()
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", 600m),
            new InventarioGestionRecepcionDestinoDto(null, null, 0m),
            new InventarioGestionRecepcionDestinoDto("N1", "G2", 400m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(2, destinos.Count);
    }

    [Theory]
    [InlineData(900)]
    [InlineData(1100)]
    public void Distribucion_SumaDistinta_Error(int total)
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", total - 100m),
            new InventarioGestionRecepcionDestinoDto("N1", "G2", 100m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal($"La suma de la distribución ({total:0.###}) debe ser igual a la cantidad en tránsito (1000).", error);
    }

    [Fact]
    public void Distribucion_DiferenciaDentroDeLaTolerancia_EsValida()
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", 500m),
            new InventarioGestionRecepcionDestinoDto("N1", "G2", 500.00005m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(2, destinos.Count);
    }

    [Fact]
    public void Distribucion_GalponRepetido_Error()
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", 500m),
            new InventarioGestionRecepcionDestinoDto("N1", " G1 ", 500m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("No repita el mismo galpón en la distribución (galpón G1).", error);
    }

    [Fact]
    public void Distribucion_MismoGalponEnNucleosDistintos_EsValida()
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", 500m),
            new InventarioGestionRecepcionDestinoDto("N2", "G1", 500m)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(2, destinos.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-50)]
    public void Distribucion_CantidadNoPositiva_Error(int cantidad)
    {
        var reparto = new[]
        {
            new InventarioGestionRecepcionDestinoDto("N1", "G1", 1000m),
            new InventarioGestionRecepcionDestinoDto("N1", "G2", cantidad)
        };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("Las cantidades de la distribución deben ser mayores a cero.", error);
    }

    [Theory]
    [InlineData("N1", null)]
    [InlineData(null, "G1")]
    [InlineData("N1", "  ")]
    public void Distribucion_FilaIncompleta_Error(string? nucleoId, string? galponId)
    {
        var reparto = new[] { new InventarioGestionRecepcionDestinoDto(nucleoId, galponId, 1000m) };

        var (destinos, error) = Resolver(reparto, cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("Cada destino de la distribución debe indicar Núcleo y Galpón.", error);
    }

    [Fact]
    public void Distribucion_EnGranjaANivelGranja_Error()
    {
        var reparto = new[] { new InventarioGestionRecepcionDestinoDto("N1", "G1", 1000m) };

        var (destinos, error) = Resolver(reparto, usaUbicacion: false, cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("La distribución por galpón solo aplica a alimento manejado por galpón. Esta recepción es a nivel granja.", error);
    }
    // ─── Granja destino que ubica por SILO (Fase B) ───────────────────────────

    private static (IReadOnlyList<InventarioGestionRecepcionDistribucionCalculos.Destino> Destinos, string? Error) ResolverPorSilo(
        IReadOnlyList<InventarioGestionRecepcionDestinoDto>? distribucion = null,
        int? toSiloId = null,
        decimal cantidadTransito = 1000m) =>
        InventarioGestionRecepcionDistribucionCalculos.Resolver(
            distribucion, null, null, usaUbicacion: false, cantidadTransito, porSilo: true, toSiloId: toSiloId);

    [Fact]
    public void PorSilo_SinDistribucion_RecibeTodoEnElSiloIndicado()
    {
        var (destinos, error) = ResolverPorSilo(toSiloId: 4, cantidadTransito: 1000m);

        Assert.Null(error);
        var destino = Assert.Single(destinos);
        Assert.Equal(4, destino.SiloId);
        Assert.Equal(1000m, destino.Quantity);
        // El galpón no participa: si viajara, la fila de stock quedaría con galpón Y silo, que es
        // justo lo que la clave natural no puede representar.
        Assert.Null(destino.NucleoId);
        Assert.Null(destino.GalponId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    public void PorSilo_SinSilo_SeRechaza(int? siloId)
    {
        var (destinos, error) = ResolverPorSilo(toSiloId: siloId);

        Assert.Empty(destinos);
        Assert.Equal("Debe indicar el silo o la bodega de recepción en la granja destino.", error);
    }

    [Fact]
    public void PorSilo_Distribuida_UnaFilaPorSilo()
    {
        var (destinos, error) = ResolverPorSilo(
            distribucion: new[]
            {
                new InventarioGestionRecepcionDestinoDto(null, null, 600m, SiloId: 4),
                new InventarioGestionRecepcionDestinoDto(null, null, 400m, SiloId: 20)
            },
            cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(2, destinos.Count);
        Assert.Equal([4, 20], destinos.Select(d => d.SiloId).ToArray());
        Assert.Equal(1000m, destinos.Sum(d => d.Quantity));
    }

    [Fact]
    public void PorSilo_Distribuida_SiloRepetido_SeRechaza()
    {
        var (destinos, error) = ResolverPorSilo(
            distribucion: new[]
            {
                new InventarioGestionRecepcionDestinoDto(null, null, 600m, SiloId: 4),
                new InventarioGestionRecepcionDestinoDto(null, null, 400m, SiloId: 4)
            },
            cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("No repita el mismo silo en la distribución (silo 4).", error);
    }

    [Fact]
    public void PorSilo_Distribuida_SumaDistinta_SeRechaza()
    {
        var (destinos, error) = ResolverPorSilo(
            distribucion: new[] { new InventarioGestionRecepcionDestinoDto(null, null, 600m, SiloId: 4) },
            cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Contains("debe ser igual a la cantidad en tránsito", error);
    }

    [Fact]
    public void PorSilo_Distribuida_FilaSinSilo_SeRechaza()
    {
        var (destinos, error) = ResolverPorSilo(
            distribucion: new[] { new InventarioGestionRecepcionDestinoDto("N1", "G1", 1000m) },
            cantidadTransito: 1000m);

        Assert.Empty(destinos);
        Assert.Equal("Cada destino de la distribución debe indicar el silo o la bodega.", error);
    }

    [Fact]
    public void SinPorSilo_ElSiloDeLasFilasSeIgnora()
    {
        // Red de seguridad para las empresas con el flag apagado: aunque un cliente mandara siloId,
        // el reparto sigue siendo por galpón y ninguna fila se lleva un silo a la BD.
        var (destinos, error) = Resolver(
            distribucion: new[]
            {
                new InventarioGestionRecepcionDestinoDto("N1", "G1", 600m, SiloId: 4),
                new InventarioGestionRecepcionDestinoDto("N1", "G2", 400m, SiloId: 20)
            },
            usaUbicacion: true,
            cantidadTransito: 1000m);

        Assert.Null(error);
        Assert.Equal(2, destinos.Count);
        Assert.All(destinos, d => Assert.Null(d.SiloId));
    }
}
