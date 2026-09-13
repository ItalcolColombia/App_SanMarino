// tests/ZooSanMarino.Application.Tests/ReporteDiarioCostosPosturaVariosRegistrosCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.ReporteDiarioCostosPosturaVariosRegistrosCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de <c>fn_reporte_diario_costos_postura</c> v3 con varios registros el mismo día (la fn SQL es
/// la dueña; esta clase es el test). Los testigos son los registros de Santa Reyes del clon del 13-sep-2026
/// (lote 155 el 04-sep: 1678/1717/1718 reales + 1721 de prueba; producción lote 152 el 04-sep: 672 real +
/// 675 de prueba) y los números esperados son los que devolvió la fn v3 en Postgres.
/// </summary>
public class ReporteDiarioCostosPosturaVariosRegistrosCalculosTests
{
    private static readonly DateTimeOffset Mediodia04Sep = new(2026, 9, 4, 12, 0, 0, TimeSpan.FromHours(-5));

    private static IReadOnlyList<ItemAlimento> Items(params (string? Nombre, double Kg)[] items)
        => items.Select(i => new ItemAlimento(i.Nombre, i.Kg)).ToList();

    // Lote 155 (Santa Reyes), 04-sep-2026: los forms graban los cuatro registros a la misma hora.
    private static readonly RegistroDia R1678 = new("sdl", 1678, Mediodia04Sep, MortH: 15, ConsKgH: 3000m,
        TipoAlimento: "H: CAMPESINO SR H", ItemsH: Items(("CAMPESINO SR H", 3000)));
    private static readonly RegistroDia R1717 = new("sdl", 1717, Mediodia04Sep, ConsKgH: 400m,
        TipoAlimento: "H: CAMPESINO SR H", ItemsH: Items(("CAMPESINO SR H", 400)));
    private static readonly RegistroDia R1718 = new("sdl", 1718, Mediodia04Sep, ConsKgH: 99m,
        TipoAlimento: "H: CAMPESINO SR H", ItemsH: Items(("CAMPESINO SR H", 99)));
    private static readonly RegistroDia R1721 = new("sdl", 1721, Mediodia04Sep,
        MortM: 2, SelH: 2, SelM: 1, ErrH: 1, ErrM: 1, VentaH: 5, ConsKgM: 50m,
        TipoAlimento: "H: CAMPESINO SR H / M: CAMPESINO SR M");

    private static readonly RegistroDia[] Lote155Dia04 = { R1721, R1718, R1678, R1717 }; // desordenados a propósito

    // ── Fila de levante del día ──────────────────────────────────────────────

    [Fact]
    public void FilaLevante_ConUnSoloRegistro_EsIgualConYSinFlag()
    {
        var sinFlag = FilaLevanteDelDia(new[] { R1678 }, permiteMultiples: false);
        var conFlag = FilaLevanteDelDia(new[] { R1678 }, permiteMultiples: true);

        Assert.Equal(sinFlag, conFlag);
        Assert.Equal(new FilaDia(15, 0, 0, 0, 0, 0, 0, 0, 3000, 0), conFlag);
    }

    [Fact]
    public void FilaLevante_Lote155ConFlag_SumaLosCuatroRegistrosDelDia()
    {
        var fila = FilaLevanteDelDia(Lote155Dia04, permiteMultiples: true);

        // v2 devolvía 15/0 · 0/0 · 0/0 · 0/0 · 3000/0 (solo el registro 1678).
        Assert.Equal(new FilaDia(
            MortalidadH: 15, MortalidadM: 2,
            SeleccionH: 2, SeleccionM: 1,
            ErrorSexajeH: 1, ErrorSexajeM: 1,
            VentaAvesH: 5, VentaAvesM: 0,
            ConsumoKgH: 3499, ConsumoKgM: 50), fila);
    }

    [Fact]
    public void FilaLevante_SinFlag_GanaElPrimeroDelDiaYElEmpateDeHoraLoDecideElId()
    {
        var fila = FilaLevanteDelDia(Lote155Dia04, permiteMultiples: false);

        Assert.Equal(new FilaDia(15, 0, 0, 0, 0, 0, 0, 0, 3000, 0), fila);
    }

    [Fact]
    public void FilaLevante_SinFlag_GanaElMasTempranoAunqueTengaIdMayor()
    {
        // Demo lote 120 (flag OFF) el 30-jun: 1053 a las 12:00 y 1051 a las 19:00 ⇒ gana 1053.
        var temprano = new RegistroDia("sdl", 1053, new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.FromHours(-5)), MortH: 50, SelH: 30);
        var tarde = new RegistroDia("sdl", 1051, new DateTimeOffset(2026, 6, 30, 19, 0, 0, TimeSpan.FromHours(-5)), MortH: 7);

        var fila = FilaLevanteDelDia(new[] { tarde, temprano }, permiteMultiples: false);

        Assert.Equal(50, fila.MortalidadH);
        Assert.Equal(30, fila.SeleccionH);
    }

    [Fact]
    public void FilaLevante_ConFlag_ElConsumoSumaEnDecimalAntesDePasarADouble()
    {
        // Lote 152 (Santa Reyes) el 21-ago: 999.991 kg + 20 kg ⇒ 1019.991 exacto (la fn: SUM(numeric)::float8).
        var fecha = new DateTimeOffset(2026, 8, 21, 12, 0, 0, TimeSpan.FromHours(-5));
        var fila = FilaLevanteDelDia(new[]
        {
            new RegistroDia("sdl", 1609, fecha, ConsKgH: 999.991m),
            new RegistroDia("sdl", 1722, fecha, MortH: 3, ConsKgH: 20m),
        }, permiteMultiples: true);

        Assert.Equal(1019.991, fila.ConsumoKgH);
        Assert.Equal(3, fila.MortalidadH);
    }

    // ── Alimentos ───────────────────────────────────────────────────────────

    [Fact]
    public void Alimentos_ConFlag_UnaEntradaPorItemDeCadaRegistroYElFallbackDelRegistroSinItems()
    {
        var alimentos = AlimentosDelDia(RegistrosQueAportan(Lote155Dia04, permiteMultiples: true));

        // v2: solo [H CAMPESINO SR H 3000]. Mismo orden que devolvió la fn v3 en el clon.
        Assert.Equal(new[]
        {
            ("H", "CAMPESINO SR H", 3000.0, OrigenMetadata),
            ("H", "CAMPESINO SR H", 400.0, OrigenMetadata),
            ("H", "CAMPESINO SR H", 99.0, OrigenMetadata),
            ("M", "CAMPESINO SR M", 50.0, OrigenTipoAlimento),
        }, alimentos.Select(a => (a.Sexo, a.Nombre, a.CantidadKg, a.Origen)));
        Assert.Equal(3499.0, alimentos.Where(a => a.Sexo == "H").Sum(a => a.CantidadKg));
    }

    [Fact]
    public void Alimentos_SinFlag_SoloElRegistroQueGana()
    {
        var alimentos = AlimentosDelDia(RegistrosQueAportan(Lote155Dia04, permiteMultiples: false));

        var unico = Assert.Single(alimentos);
        Assert.Equal(("H", "CAMPESINO SR H", 3000.0, OrigenMetadata), (unico.Sexo, unico.Nombre, unico.CantidadKg, unico.Origen));
    }

    [Fact]
    public void Alimentos_RegistroSinItemsPeroConKg_AportaSuFallbackAunqueOtroRegistroDelDiaTraigaItems()
    {
        var conItems = new RegistroDia("sdl", 1, Mediodia04Sep, ConsKgH: 100m, ItemsH: Items(("INICIACION", 100)));
        var sinItems = new RegistroDia("sdl", 2, Mediodia04Sep, ConsKgH: 30m, TipoAlimento: "H: LEVANTE / M: MACHOS");

        var alimentos = AlimentosDelDia(new[] { conItems, sinItems });

        // Con el fallback decidido por DÍA (v2) los 30 kg del segundo registro no tenían entrada.
        Assert.Equal(new[]
        {
            ("H", "INICIACION", 100.0, OrigenMetadata),
            ("H", "LEVANTE", 30.0, OrigenTipoAlimento),
        }, alimentos.Select(a => (a.Sexo, a.Nombre, a.CantidadKg, a.Origen)));
    }

    [Fact]
    public void Alimentos_ItemsVaciosYSinKg_NoAportaNada()
    {
        var vacio = new RegistroDia("sdl", 1, Mediodia04Sep, TipoAlimento: "—", ItemsH: Items());

        Assert.Empty(AlimentosDelDia(new[] { vacio }));
    }

    [Fact]
    public void Alimentos_ItemSinNombreNiTipoAlimento_SaleSinEspecificar()
    {
        var reg = new RegistroDia("sdp", 9, Mediodia04Sep, ConsKgM: 12m, ItemsH: Items((null, 5)));

        var alimentos = AlimentosDelDia(new[] { reg });

        Assert.Equal(new[]
        {
            ("H", SinEspecificar, 5.0, OrigenMetadata),
            ("M", SinEspecificar, 12.0, OrigenTipoAlimento),
        }, alimentos.Select(a => (a.Sexo, a.Nombre, a.CantidadKg, a.Origen)));
    }

    [Fact]
    public void Alimentos_ProduccionConFlag_LosItemsDeLosDosRegistrosOrdenadosPorNombre()
    {
        // Producción lote 152 el 04-sep: 672 (catalogItemId 404 ⇒ CAMPESINO SR H, 399 kg) + 675 (100 kg).
        // v2 mostraba solo el ítem del último registro (la metadata de fn_seguimiento_diario_produccion v4).
        var hora = new DateTimeOffset(2026, 9, 4, 7, 0, 0, TimeSpan.FromHours(-5));
        var alimentos = AlimentosDelDia(new[]
        {
            new RegistroDia("sdp", 675, hora, ConsKgH: 100m, ItemsH: Items(("CAMPESINO SR PROD", 100))),
            new RegistroDia("sdp", 672, hora, ConsKgH: 399m, ItemsH: Items(("CAMPESINO SR H", 399))),
        });

        Assert.Equal(new[] { ("CAMPESINO SR H", 399.0), ("CAMPESINO SR PROD", 100.0) },
            alimentos.Select(a => (a.Nombre, a.CantidadKg)));
    }

    [Fact]
    public void Alimentos_MismoIdEnLasDosTablas_SonRegistrosDistintos()
    {
        var sdl = new RegistroDia("sdl", 50, Mediodia04Sep, ConsKgH: 10m, TipoAlimento: "A");
        var sdp = new RegistroDia("sdp", 50, Mediodia04Sep, ConsKgH: 20m, ItemsH: Items(("A", 20)));

        var alimentos = AlimentosDelDia(new[] { sdp, sdl });

        Assert.Equal(new[] { ("A", 10.0, OrigenTipoAlimento), ("A", 20.0, OrigenMetadata) },
            alimentos.Select(a => (a.Nombre, a.CantidadKg, a.Origen)));
    }

    // ── tipo_alimento ⇒ nombre de fallback ──────────────────────────────────

    [Theory]
    [InlineData("H: CAMPESINO SR H / M: CAMPESINO SR M", "CAMPESINO SR H", "CAMPESINO SR M")]
    [InlineData("H: INICIO + CRECIMIENTO / M: MACHOS", "INICIO + CRECIMIENTO", "MACHOS")]
    [InlineData("POLLITA / POLLO", "POLLITA", "POLLO")]
    [InlineData("SOLO UNO", "SOLO UNO", "SOLO UNO")]
    [InlineData("H: CAMPESINO SR H", "H: CAMPESINO SR H", "H: CAMPESINO SR H")] // sin "/ M:" no se quita el prefijo
    [InlineData("   ", null, null)]
    [InlineData(null, null, null)]
    public void PartirTipoAlimento_EspejaElCaseDeLaFn(string? tipo, string? hembras, string? machos)
    {
        Assert.Equal((hembras, machos), PartirTipoAlimento(tipo));
    }

    // ── Producción: venta de aves ───────────────────────────────────────────

    private static readonly RegistroDia P672 = new("sdp", 672, new DateTimeOffset(2026, 9, 4, 7, 0, 0, TimeSpan.FromHours(-5)), MortH: 5);
    private static readonly RegistroDia P675 = new("sdp", 675, new DateTimeOffset(2026, 9, 4, 7, 0, 0, TimeSpan.FromHours(-5)), MortH: 1, VentaH: 7, VentaM: 2);

    [Fact]
    public void VentaProduccion_ConFlag_SumaLosRegistrosDelDia()
    {
        // v2 devolvía 0/0: el join por seg_id (672, el MIN del día) no veía la venta del registro 675.
        Assert.Equal((7, 2), VentaAvesProduccionDelDia(new[] { P672, P675 }, segIdDeLaFn: 672, permiteMultiples: true));
    }

    [Fact]
    public void VentaProduccion_SinFlag_EsLaDelRegistroSegId()
    {
        Assert.Equal((0, 0), VentaAvesProduccionDelDia(new[] { P672, P675 }, segIdDeLaFn: 672, permiteMultiples: false));
        Assert.Equal((7, 2), VentaAvesProduccionDelDia(new[] { P675 }, segIdDeLaFn: 675, permiteMultiples: false));
    }

    [Fact]
    public void VentaProduccion_DiaSoloDeMovimientos_EsCero()
    {
        Assert.Equal((0, 0), VentaAvesProduccionDelDia(Array.Empty<RegistroDia>(), segIdDeLaFn: null, permiteMultiples: true));
        Assert.Equal((0, 0), VentaAvesProduccionDelDia(Array.Empty<RegistroDia>(), segIdDeLaFn: null, permiteMultiples: false));
    }
}
