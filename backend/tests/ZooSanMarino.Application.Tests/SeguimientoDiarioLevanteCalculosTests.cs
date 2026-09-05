// tests/ZooSanMarino.Application.Tests/SeguimientoDiarioLevanteCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SeguimientoDiarioLevanteCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de <c>fn_seguimiento_diario_levante</c> (la fn SQL es la dueña; esta clase es el
/// test — regla «una sola fórmula por número»). El caso testigo (lote 152, Santa Reyes) está
/// validado contra Postgres real en una transacción revertida: 2 registros el mismo día
/// (999.991 + 5.0 kg de consumo, peso null + 0.220) dieron consumo=1004.991 y peso=0.220.
/// </summary>
public class SeguimientoDiarioLevanteCalculosTests
{
    private static DateOnly D(int dia) => new(2026, 8, dia);

    private static RegistroCrudo Reg(
        long? regId, int mortH, int mortM, double consH, double consM,
        double? pesoH, double? pesoM, double? unifH = null, double? unifM = null)
        => new(regId, mortH, mortM, 0, 0, 0, 0, consH, consM, 0, 0, 0, 0, 0, 0, pesoH, pesoM, unifH, unifM);

    [Fact]
    public void AgruparPorDia_ConUnSoloRegistro_EsIdenticoAEseRegistro()
    {
        var reg = Reg(1609, mortH: 0, mortM: 0, consH: 999.991, consM: 0, pesoH: null, pesoM: null);
        var filas = new[] { (D(21), new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc), reg) };

        var agrupado = AgruparPorDia(filas);

        Assert.Single(agrupado);
        Assert.Equal(reg, agrupado[0].Fila);
    }

    [Fact]
    public void AgruparPorDia_CasoTestigoLote152_SumaConsumoYPromediaPesoIgnorandoNulos()
    {
        // Fila real (id 1609): consumo 999.991, sin peso. Fila de prueba insertada (transacción
        // revertida contra Postgres real): consumo 5.0, peso 0.220. La fn devolvió consumo
        // 1004.991 y peso 0.220 (el AVG ignora el NULL, no lo cuenta como 0).
        var original = Reg(1609, mortH: 0, mortM: 0, consH: 999.991, consM: 0, pesoH: null, pesoM: null);
        var nueva = Reg(9999, mortH: 3, mortM: 1, consH: 5.0, consM: 2.0, pesoH: 0.220, pesoM: 0.250,
            unifH: 85.0, unifM: 84.0);
        var filas = new[]
        {
            (D(21), new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc), original),
            (D(21), new DateTime(2026, 8, 21, 17, 0, 0, DateTimeKind.Utc), nueva),
        };

        var agrupado = AgruparPorDia(filas);
        var f = agrupado[0].Fila;

        Assert.Single(agrupado);
        Assert.Equal(3, f.MortH);
        Assert.Equal(1, f.MortM);
        Assert.Equal(1004.991, f.ConsKgH, 3);
        Assert.Equal(2.0, f.ConsKgM);
        Assert.Equal(0.220, f.PesoH);     // AVG(null, 0.220) = 0.220, no 0.110
        Assert.Equal(0.250, f.PesoM);
        Assert.Equal(85.0, f.UnifH);      // gana el ÚLTIMO registro del día
        Assert.Equal(84.0, f.UnifM);
        Assert.Equal(1609, f.RegId);      // el primero (MIN) no nulo
    }

    [Fact]
    public void AgruparPorDia_UniformidadEnAmbasFilas_GanaLaUltimaNoElPromedio()
    {
        var temprano = Reg(1, 0, 0, 0, 0, null, null, unifH: 80.0);
        var tarde = Reg(2, 0, 0, 0, 0, null, null, unifH: 82.0);
        var filas = new[]
        {
            (D(1), new DateTime(2026, 8, 1, 5, 0, 0, DateTimeKind.Utc), temprano),
            (D(1), new DateTime(2026, 8, 1, 17, 0, 0, DateTimeKind.Utc), tarde),
        };

        Assert.Equal(82.0, AgruparPorDia(filas)[0].Fila.UnifH); // no 81.0 (promedio)
    }

    [Fact]
    public void ContarDias_CuentaDiasDistintosNoFilas()
    {
        // Caso testigo real: 3 filas crudas (2 el mismo día + 1 otro día) ⇒ 2 días, no 3.
        var fechas = new[] { D(21), D(21), D(22) };

        Assert.Equal(2, ContarDias(fechas));
    }
}
