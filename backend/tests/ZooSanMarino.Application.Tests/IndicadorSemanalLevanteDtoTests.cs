// tests/ZooSanMarino.Application.Tests/IndicadorSemanalLevanteDtoTests.cs
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de materialización de <c>fn_indicadores_levante_postura</c> (EF <c>SqlQueryRaw</c>): toda
/// columna que la fn puede devolver NULL tiene que ser nullable en el DTO, o el endpoint responde 500.
/// Pasó con la guía propia de Santa Reyes, que arranca en la semana 18: la fn deja la guía mixta en
/// NULL para todo el levante y las pestañas Indicadores y Gráfica quedaban vacías (13-sep-2026).
/// </summary>
public class IndicadorSemanalLevanteDtoTests
{
    [Theory]
    [InlineData(nameof(IndicadorSemanalLevanteDto.ConsumoTabla))]
    [InlineData(nameof(IndicadorSemanalLevanteDto.PesoTabla))]
    [InlineData(nameof(IndicadorSemanalLevanteDto.UnifTabla))]
    [InlineData(nameof(IndicadorSemanalLevanteDto.MortTabla))]
    public void ColumnasDeGuiaMixta_SonNulables(string propiedad)
    {
        var tipo = typeof(IndicadorSemanalLevanteDto).GetProperty(propiedad)!.PropertyType;

        Assert.Equal(typeof(double?), tipo);
    }
}
