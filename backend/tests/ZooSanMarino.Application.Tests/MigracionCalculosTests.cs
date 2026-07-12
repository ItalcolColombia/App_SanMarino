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
}
