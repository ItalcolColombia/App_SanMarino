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

    // ── TryHora (columna «Hora Salida» de la plantilla de venta engorde) ───────────────────

    [Theory]
    [InlineData("06:30", 6, 30)]
    [InlineData("6:30", 6, 30)]
    [InlineData("18:45:00", 18, 45)]
    [InlineData("23:59", 23, 59)]
    [InlineData("00:00", 0, 0)]
    public void TryHora_Texto24h(string input, int h, int m)
    {
        Assert.True(MigracionCalculos.TryHora(input, out var val));
        Assert.Equal(new TimeOnly(h, m), val);
    }

    [Theory]
    [InlineData("06:30 AM", 6, 30)]
    [InlineData("6:30 PM", 18, 30)]
    [InlineData("12:00 AM", 0, 0)]
    [InlineData("12:00 PM", 12, 0)]
    public void TryHora_Texto12h(string input, int h, int m)
    {
        Assert.True(MigracionCalculos.TryHora(input, out var val));
        Assert.Equal(new TimeOnly(h, m), val);
    }

    /// <summary>Excel guarda la hora como fracción del día: 0.5 = mediodía.</summary>
    [Theory]
    [InlineData(0.5, 12, 0)]
    [InlineData(0.25, 6, 0)]
    [InlineData(0.0, 0, 0)]
    [InlineData(0.270833333333333, 6, 30)]  // 6:30 exportado por Excel
    public void TryHora_SerialFraccionario(double serial, int h, int m)
    {
        Assert.True(MigracionCalculos.TryHora(serial, out var val));
        Assert.Equal(new TimeOnly(h, m), val);
    }

    /// <summary>Serial con parte entera (fecha + hora): se ignora la fecha.</summary>
    [Fact]
    public void TryHora_SerialConFecha_TomaSoloLaHora()
    {
        Assert.True(MigracionCalculos.TryHora(45000.5, out var val));
        Assert.Equal(new TimeOnly(12, 0), val);
    }

    /// <summary>Serial que redondea a 24:00 ⇒ medianoche, nunca una hora inválida.</summary>
    [Fact]
    public void TryHora_SerialCasiUnDia_CaeEnMedianoche()
    {
        Assert.True(MigracionCalculos.TryHora(0.99999, out var val));
        Assert.Equal(new TimeOnly(0, 0), val);
    }

    [Fact]
    public void TryHora_DateTimeYTimeSpan()
    {
        Assert.True(MigracionCalculos.TryHora(new DateTime(2026, 7, 26, 14, 15, 0), out var a));
        Assert.Equal(new TimeOnly(14, 15), a);

        Assert.True(MigracionCalculos.TryHora(new TimeSpan(7, 45, 0), out var b));
        Assert.Equal(new TimeOnly(7, 45), b);
    }

    /// <summary>Un serial exportado como texto sin formato de hora igual se interpreta.</summary>
    [Fact]
    public void TryHora_SerialComoTexto()
    {
        Assert.True(MigracionCalculos.TryHora("0.5", out var val));
        Assert.Equal(new TimeOnly(12, 0), val);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mañana")]
    [InlineData("25:00")]
    [InlineData(-0.5)]
    public void TryHora_InvalidoDevuelveFalse(object? input)
    {
        Assert.False(MigracionCalculos.TryHora(input, out _));
    }

    // ── TryBooleanoSiNo (columna «Venta sobre mixtas») ─────────────────────────────────────

    [Theory]
    [InlineData("Sí", true)]
    [InlineData("si", true)]
    [InlineData("SI", true)]
    [InlineData("S", true)]
    [InlineData("x", true)]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData("Verdadero", true)]
    [InlineData("No", false)]
    [InlineData("n", false)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("Falso", false)]
    public void TryBooleanoSiNo_ReconoceLosValoresDePlanilla(string input, bool esperado)
    {
        Assert.True(MigracionCalculos.TryBooleanoSiNo(input, out var val));
        Assert.Equal(esperado, val);
    }

    [Fact]
    public void TryBooleanoSiNo_BoolNativoDeExcel()
    {
        Assert.True(MigracionCalculos.TryBooleanoSiNo(true, out var v));
        Assert.True(v);
        Assert.True(MigracionCalculos.TryBooleanoSiNo(false, out var f));
        Assert.False(f);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("tal vez")]
    [InlineData("2")]
    public void TryBooleanoSiNo_NoReconocidoDevuelveFalse(object? input)
    {
        Assert.False(MigracionCalculos.TryBooleanoSiNo(input, out _));
    }
}
