using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Coerción/normalización pura del módulo de Migraciones: interpretación robusta de celdas de Excel
/// (números con coma o punto, fechas seriales/strings, nombres con acentos para matching de catálogos).
/// </summary>
public class MigracionCalculosTests
{
    [Theory]
    [InlineData("Antioquia", "antioquia")]
    [InlineData("  BOGOTÁ  D.C. ", "bogota d.c.")]
    [InlineData("Núcleo Á É Í", "nucleo a e i")]
    [InlineData("San   Antonio", "san antonio")]
    [InlineData(null, "")]
    public void NormalizarClave_QuitaAcentosMinusculasYEspacios(string? input, string esperado)
        => Assert.Equal(esperado, MigracionCalculos.NormalizarClave(input));

    [Fact]
    public void NormalizarClave_MismaClave_ParaVariantesDeAcentoYCaso()
        => Assert.Equal(MigracionCalculos.NormalizarClave("BOLÍVAR"), MigracionCalculos.NormalizarClave("bolivar"));

    [Theory]
    [InlineData("  hola ", "hola")]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData(null, null)]
    public void TextoLimpio_RecortaYVacioEsNull(string? input, string? esperado)
        => Assert.Equal(esperado, MigracionCalculos.TextoLimpio(input));

    [Fact]
    public void EsVacia_DetectaNullYEspacios()
    {
        Assert.True(MigracionCalculos.EsVacia(null));
        Assert.True(MigracionCalculos.EsVacia("   "));
        Assert.False(MigracionCalculos.EsVacia("x"));
        Assert.False(MigracionCalculos.EsVacia(0));
    }

    [Theory]
    [InlineData(12, true, 12)]
    [InlineData(12.0, true, 12)]
    [InlineData("12", true, 12)]
    [InlineData("1.234", true, 1234)]   // miles con punto
    [InlineData("1,234", true, 1234)]   // miles con coma
    [InlineData("abc", false, 0)]
    [InlineData(null, false, 0)]
    public void TryEntero_CoercionaVariasFormas(object? input, bool ok, int esperado)
    {
        Assert.Equal(ok, MigracionCalculos.TryEntero(input, out var v));
        if (ok) Assert.Equal(esperado, v);
    }

    [Fact]
    public void TryEntero_DoubleNoEnteroFalla()
        => Assert.False(MigracionCalculos.TryEntero(12.5, out _));

    [Theory]
    [InlineData("12,5", 12.5)]     // coma decimal
    [InlineData("12.5", 12.5)]     // punto decimal
    [InlineData("1.234,56", 1234.56)] // punto miles + coma decimal
    [InlineData(12.5, 12.5)]
    public void TryDecimal_AceptaComaYPunto(object input, double esperado)
    {
        Assert.True(MigracionCalculos.TryDecimal(input, out var v));
        Assert.Equal((decimal)esperado, v);
    }

    [Fact]
    public void TryFecha_DateTimeSerialYString()
    {
        Assert.True(MigracionCalculos.TryFecha(new DateTime(2026, 3, 15), out var d1));
        Assert.Equal(new DateTime(2026, 3, 15), d1);

        var serial = new DateTime(2026, 3, 15).ToOADate();
        Assert.True(MigracionCalculos.TryFecha(serial, out var d2));
        Assert.Equal(new DateTime(2026, 3, 15), d2);

        Assert.True(MigracionCalculos.TryFecha("2026-03-15", out var d3));
        Assert.Equal(new DateTime(2026, 3, 15), d3);

        Assert.True(MigracionCalculos.TryFecha("15/03/2026", out var d4));
        Assert.Equal(new DateTime(2026, 3, 15), d4);
    }

    [Fact]
    public void TryFecha_InvalidaFalla()
        => Assert.False(MigracionCalculos.TryFecha("no-es-fecha", out _));

    [Theory]
    [InlineData("A", "A")]
    [InlineData("activa", "A")]
    [InlineData(null, "A")]
    [InlineData("I", "I")]
    [InlineData("Inactivo", "I")]
    [InlineData("0", "I")]
    public void NormalizarEstado_MapeaAI(string? input, string esperado)
        => Assert.Equal(esperado, MigracionCalculos.NormalizarEstado(input));

    [Theory]
    [InlineData(null, "kg")]
    [InlineData("", "kg")]
    [InlineData("kg", "kg")]
    [InlineData("KG", "kg")]
    [InlineData("Kilos", "kg")]
    [InlineData("qq", "qq")]
    [InlineData("QQ", "qq")]
    [InlineData("Quintales", "qq")]
    [InlineData("libras", null)]
    [InlineData("gr", null)]
    public void NormalizarUnidadConsumo_KgDefaultQqYRechazaOtras(string? input, string? esperado)
        => Assert.Equal(esperado, MigracionCalculos.NormalizarUnidadConsumo(input));

    [Fact]
    public void ConsumoAKilos_QqConvierteConFactor4536YRedondeo3Decimales()
    {
        // Mismo factor y redondeo que el front (QQ_TO_KG = 45.36, 3 decimales).
        Assert.Equal(45.36m, MigracionCalculos.ConsumoAKilos(1m, "qq"));
        Assert.Equal(90.72m, MigracionCalculos.ConsumoAKilos(2m, "qq"));
        Assert.Equal(11.34m, MigracionCalculos.ConsumoAKilos(0.25m, "qq"));
        Assert.Equal(4.99m, MigracionCalculos.ConsumoAKilos(0.11m, "qq")); // 4.9896 → 4.99 (redondeo a 3)
    }

    [Fact]
    public void ConsumoAKilos_KgPasaIntactoYNullSeConserva()
    {
        Assert.Equal(12.5m, MigracionCalculos.ConsumoAKilos(12.5m, "kg"));
        Assert.Null(MigracionCalculos.ConsumoAKilos(null, "qq"));
        Assert.Null(MigracionCalculos.ConsumoAKilos(null, "kg"));
    }
}
