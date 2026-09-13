// tests/ZooSanMarino.Application.Tests/SeguimientoDiarioLevanteCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.SeguimientoDiarioLevanteCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de <c>fn_seguimiento_diario_levante</c> (la fn SQL es la dueña; esta clase es el
/// test — regla «una sola fórmula por número»). El caso testigo (lote 152, Santa Reyes) está
/// validado contra Postgres real en una transacción revertida: 2 registros el mismo día
/// (999.991 + 5.0 kg de consumo, peso null + 0.220) dieron consumo=1004.991 y peso=0.220.
/// Los casos v2 (13-sep-2026) fijan el criterio de producción v4: uniformidad/CV = último que la
/// trae, desempate por id y promedios de los registros que midieron (&gt; 0).
/// </summary>
public class SeguimientoDiarioLevanteCalculosTests
{
    private static DateOnly D(int dia) => new(2026, 8, dia);

    /// <summary>Mediodía: la hora a la que los forms graban TODOS los registros del día.</summary>
    private static DateTime Mediodia(int dia) => new(2026, 8, dia, 17, 0, 0, DateTimeKind.Utc);

    private static RegistroCrudo Reg(
        long? regId, int mortH, int mortM, double consH, double consM,
        double? pesoH, double? pesoM, double? unifH = null, double? unifM = null,
        double? cvH = null, double? cvM = null, double? kcalH = null, double? protH = null)
        => new(regId, mortH, mortM, 0, 0, 0, 0, consH, consM, 0, 0, 0, 0, 0, 0, pesoH, pesoM, unifH, unifM,
            cvH, cvM, kcalH, protH);

    private static RegistroCrudo Medicion(
        long id, double? pesoH = null, double? pesoM = null, double? unifH = null, double? unifM = null,
        double? cvH = null, double? cvM = null, double? kcalH = null, double? protH = null)
        => Reg(id, 0, 0, 0, 0, pesoH, pesoM, unifH, unifM, cvH, cvM, kcalH, protH);

    [Fact]
    public void AgruparPorDia_ConUnSoloRegistro_EsIdenticoAEseRegistro()
    {
        var reg = Reg(1609, mortH: 2, mortM: 1, consH: 999.991, consM: 3.5, pesoH: 0.480, pesoM: 0.0,
            unifH: 0.0, unifM: null, cvH: 8.2, cvM: null, kcalH: 2750, protH: 0.0);
        var filas = new[] { (D(21), new DateTime(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc), reg) };

        var agrupado = AgruparPorDia(filas);

        Assert.Single(agrupado);
        Assert.Equal(reg, agrupado[0].Fila); // incluso con ceros: un registro no se «corrige»
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
    public void AgruparPorDia_UltimoRegistroSinUniformidadNiCv_NoTapaLaMedicionDelDia()
    {
        // Antes: (array_agg(x ORDER BY c_ts DESC))[1] devolvía el NULL del registro posterior.
        var filas = new[]
        {
            (D(4), Mediodia(4), Medicion(1678, pesoH: 500, unifH: 80, unifM: 76, cvH: 8, cvM: 9)),
            (D(4), Mediodia(4), Medicion(1717, pesoH: 520)),
        };

        var f = AgruparPorDia(filas)[0].Fila;

        Assert.Equal(80, f.UnifH);
        Assert.Equal(76, f.UnifM);
        Assert.Equal(8, f.CvH);
        Assert.Equal(9, f.CvM);
    }

    [Fact]
    public void AgruparPorDia_EmpateDeTimestamp_GanaElMayorIdSinImportarElOrdenDeEntrada()
    {
        var a = Medicion(10, unifH: 80, cvH: 7);
        var b = Medicion(11, unifH: 82, cvH: 6);

        var enOrden = AgruparPorDia(new[] { (D(4), Mediodia(4), a), (D(4), Mediodia(4), b) })[0].Fila;
        var alReves = AgruparPorDia(new[] { (D(4), Mediodia(4), b), (D(4), Mediodia(4), a) })[0].Fila;

        Assert.Equal(82, enOrden.UnifH);
        Assert.Equal(6, enOrden.CvH);
        Assert.Equal(enOrden, alReves);
    }

    [Fact]
    public void AgruparPorDia_TimestampPosteriorGanaAunqueTengaMenorId()
    {
        // El id solo desempata: un registro con hora posterior sigue siendo «el último».
        var filas = new[]
        {
            (D(4), new DateTime(2026, 8, 4, 22, 0, 0, DateTimeKind.Utc), Medicion(5, unifH: 90)),
            (D(4), Mediodia(4), Medicion(9, unifH: 70)),
        };

        Assert.Equal(90, AgruparPorDia(filas)[0].Fila.UnifH);
    }

    [Fact]
    public void AgruparPorDia_PesoKcalYProtConUnCero_PromedianSoloLosQueMidieron()
    {
        var filas = new[]
        {
            (D(4), Mediodia(4), Medicion(1, pesoH: 500, pesoM: 0, kcalH: 2800, protH: 17)),
            (D(4), Mediodia(4), Medicion(2, pesoH: 0, pesoM: 620, kcalH: 0, protH: 0)),
            (D(4), Mediodia(4), Medicion(3, pesoH: 520, pesoM: null)),
        };

        var f = AgruparPorDia(filas)[0].Fila;

        Assert.Equal(510, f.PesoH);   // (500 + 520) / 2 — el AVG de v1 daba 340
        Assert.Equal(620, f.PesoM);   // v1: 310
        Assert.Equal(2800, f.KcalH);  // v1: 1400
        Assert.Equal(17, f.ProtH);
    }

    [Fact]
    public void AgruparPorDia_NingunoMidio_DevuelveElPromedioDeSiempre()
    {
        var todoCero = AgruparPorDia(new[]
        {
            (D(4), Mediodia(4), Medicion(1, pesoH: 0, kcalH: 0)),
            (D(4), Mediodia(4), Medicion(2, pesoH: 0, kcalH: null)),
        })[0].Fila;
        var todoNulo = AgruparPorDia(new[]
        {
            (D(4), Mediodia(4), Medicion(1)),
            (D(4), Mediodia(4), Medicion(2)),
        })[0].Fila;

        Assert.Equal(0, todoCero.PesoH);
        Assert.Equal(0, todoCero.KcalH);
        Assert.Null(todoNulo.PesoH);
        Assert.Null(todoNulo.UnifH);
        Assert.Null(todoNulo.CvM);
        Assert.Null(todoNulo.ProtH);
    }

    [Fact]
    public void AgruparPorDia_UniformidadEnCeroTecleada_CuentaComoMedida()
    {
        // Mismo criterio que producción v4: «la trae» = no NULL. Ningún escritor guarda 0 por defecto
        // (el form y la carga masiva mandan NULL); un 0 solo lo teclea el usuario.
        var filas = new[]
        {
            (D(4), Mediodia(4), Medicion(1, unifH: 80)),
            (D(4), Mediodia(4), Medicion(2, unifH: 0)),
        };

        Assert.Equal(0, AgruparPorDia(filas)[0].Fila.UnifH);
    }

    [Fact]
    public void AgruparPorDia_DiasDistintos_NoSeMezclan()
    {
        var filas = new[]
        {
            (D(5), Mediodia(5), Medicion(3, pesoH: 600, unifH: 90)),
            (D(4), Mediodia(4), Medicion(1, pesoH: 500, unifH: 80)),
        };

        var agrupado = AgruparPorDia(filas);

        Assert.Equal(new[] { D(4), D(5) }, agrupado.Select(x => x.Dia));
        Assert.Equal(500, agrupado[0].Fila.PesoH);
        Assert.Equal(90, agrupado[1].Fila.UnifH);
    }

    [Fact]
    public void ContarDias_CuentaDiasDistintosNoFilas()
    {
        // Caso testigo real: 3 filas crudas (2 el mismo día + 1 otro día) ⇒ 2 días, no 3.
        var fechas = new[] { D(21), D(21), D(22) };

        Assert.Equal(2, ContarDias(fechas));
    }
}
