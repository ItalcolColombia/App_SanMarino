using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Equivalencia numérica de SeguimientoAvesEngordeCalculos (extraído verbatim de
/// SeguimientoAvesEngordeService en el refactor a partial classes): fecha efectiva del
/// histórico, apertura de galpón, recálculo de saldo de alimento por seguimiento,
/// búsqueda de candidato histórico y detección de campos de kardex en metadata.
/// Misma aritmética/orden que vivía inline en el servicio.
/// </summary>
public class SeguimientoAvesEngordeCalculosTests
{
    private static LoteRegistroHistoricoUnificado Hist(
        string tipoEvento,
        DateTime fechaOperacion,
        decimal? cantidadKg,
        DateTimeOffset? createdAt = null,
        string? referencia = null,
        string? numeroDocumento = null,
        bool anulado = false,
        long id = 0)
        => new()
        {
            Id = id,
            TipoEvento = tipoEvento,
            OrigenTabla = "origen",
            FechaOperacion = fechaOperacion,
            CantidadKg = cantidadKg,
            CreatedAt = createdAt ?? new DateTimeOffset(fechaOperacion, TimeSpan.Zero),
            Referencia = referencia,
            NumeroDocumento = numeroDocumento,
            Anulado = anulado,
        };

    private static SeguimientoDiarioAvesEngorde Seg(long id, DateTime fecha, decimal? consumoH = null, decimal? consumoM = null)
        => new() { Id = id, Fecha = fecha, ConsumoKgHembras = consumoH, ConsumoKgMachos = consumoM };

    // ---------- YmdHistoricoEfectivo ----------

    [Fact]
    public void YmdHistoricoEfectivo_PatronSeguimientoEnReferencia_ExtraeFechaDelGrupo()
    {
        var h = Hist("INV_INGRESO", new DateTime(2026, 1, 1), 10m, referencia: "Seguimiento aves engorde #45 2026-02-10");

        Assert.Equal("2026-02-10", SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h));
    }

    [Fact]
    public void YmdHistoricoEfectivo_PatronPartidoEntreReferenciaYNumeroDocumento_SeConcatenanParaElMatch()
    {
        var h = Hist("INV_INGRESO", new DateTime(2026, 1, 1), 10m,
            referencia: "Seguimiento aves engorde #7", numeroDocumento: "2026-03-01");

        Assert.Equal("2026-03-01", SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h));
    }

    [Fact]
    public void YmdHistoricoEfectivo_InvConsumo_SinPatronSeguimiento_ExtraeCualquierFechaDeLaReferencia()
    {
        var h = Hist("INV_CONSUMO", new DateTime(2026, 1, 1), 10m, referencia: "RVN 2026-04-05 ajuste");

        Assert.Equal("2026-04-05", SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h));
    }

    [Fact]
    public void YmdHistoricoEfectivo_InvConsumo_SinFechaEnReferencia_UsaFechaOperacion()
    {
        var h = Hist("INV_CONSUMO", new DateTime(2026, 5, 20, 14, 30, 0), 10m, referencia: "sin fecha aqui");

        Assert.Equal("2026-05-20", SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h));
    }

    [Fact]
    public void YmdHistoricoEfectivo_TipoEventoDistintoSinPatron_UsaFechaOperacionTruncandoHora()
    {
        var h = Hist("INV_INGRESO", new DateTime(2026, 6, 1, 23, 59, 0), 10m);

        Assert.Equal("2026-06-01", SeguimientoAvesEngordeCalculos.YmdHistoricoEfectivo(h));
    }

    // ---------- FormatYmd / FormatKg / Ts* ----------

    [Fact]
    public void FormatYmd_TruncaHora()
    {
        Assert.Equal("2026-07-04", SeguimientoAvesEngordeCalculos.FormatYmd(new DateTime(2026, 7, 4, 22, 15, 0)));
    }

    [Theory]
    [InlineData(12.5, "12.5")]
    [InlineData(100.125, "100.125")]
    [InlineData(0, "0")]
    [InlineData(3, "3")]
    public void FormatKg_TresDecimalesSinCerosFinales(double kg, string esperado)
    {
        Assert.Equal(esperado, SeguimientoAvesEngordeCalculos.FormatKg((decimal)kg));
    }

    [Fact]
    public void TsHistorico_DelegaEnCreatedAtToUnixTimeMilliseconds()
    {
        var createdAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.FromHours(-5));
        var h = Hist("INV_INGRESO", new DateTime(2026, 1, 1), 10m, createdAt: createdAt);

        Assert.Equal(createdAt.ToUnixTimeMilliseconds(), SeguimientoAvesEngordeCalculos.TsHistorico(h));
    }

    [Fact]
    public void TsSeguimiento_UsaMediodiaUtcDeLaFechaIgnorandoHoraOriginal()
    {
        var s = Seg(1, new DateTime(2026, 3, 5, 8, 45, 0));

        var esperado = new DateTimeOffset(2026, 3, 5, 12, 0, 0, TimeSpan.Zero).ToUnixTimeMilliseconds();

        Assert.Equal(esperado, SeguimientoAvesEngordeCalculos.TsSeguimiento(s));
    }

    // ---------- ComputeSaldoAperturaGalponAntesPrimerSeguimiento ----------

    [Fact]
    public void ComputeSaldoApertura_OrdenaPorTsYPisaEnCeroTrasCadaMovimiento()
    {
        var firstSegDate = new DateTime(2026, 2, 1);
        // Misma fecha (2026-01-20): salida procesada primero (ts menor) deja el saldo en 0 antes del ingreso.
        var salida = Hist("INV_TRASLADO_SALIDA", new DateTime(2026, 1, 20), 80m,
            createdAt: new DateTimeOffset(2026, 1, 20, 8, 0, 0, TimeSpan.Zero));
        var ingreso = Hist("INV_INGRESO", new DateTime(2026, 1, 20), 50m,
            createdAt: new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero));

        var opening = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            new[] { salida, ingreso }, firstSegDate);

        Assert.Equal(50m, opening);
    }

    [Fact]
    public void ComputeSaldoApertura_FechaEncaset_ExcluyeMovimientosAnteriores()
    {
        var firstSegDate = new DateTime(2026, 2, 1);
        var previoAlEncaset = Hist("INV_INGRESO", new DateTime(2026, 1, 10), 999m,
            createdAt: new DateTimeOffset(2026, 1, 10, 0, 0, 0, TimeSpan.Zero));
        var salida = Hist("INV_TRASLADO_SALIDA", new DateTime(2026, 1, 20), 80m,
            createdAt: new DateTimeOffset(2026, 1, 20, 8, 0, 0, TimeSpan.Zero));
        var ingreso = Hist("INV_INGRESO", new DateTime(2026, 1, 20), 50m,
            createdAt: new DateTimeOffset(2026, 1, 20, 9, 0, 0, TimeSpan.Zero));
        var hist = new[] { previoAlEncaset, salida, ingreso };

        var sinEncaset = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(hist, firstSegDate);
        var conEncaset = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            hist, firstSegDate, fechaEncaset: new DateTime(2026, 1, 15));

        Assert.Equal(969m, sinEncaset); // 999 - 80 + 50 (sin floor, nunca queda negativo)
        Assert.Equal(50m, conEncaset);  // previoAlEncaset descartado por ser anterior al encaset: 0 -80 (floor 0) +50
    }

    [Fact]
    public void ComputeSaldoApertura_IgnoraMovimientosEnOTrasElPrimerSeguimiento()
    {
        var firstSegDate = new DateTime(2026, 2, 1);
        var antes = Hist("INV_INGRESO", new DateTime(2026, 1, 20), 50m);
        var mismoDiaPrimerSeguimiento = Hist("INV_INGRESO", new DateTime(2026, 2, 1), 999m);

        var opening = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            new[] { antes, mismoDiaPrimerSeguimiento }, firstSegDate);

        Assert.Equal(50m, opening);
    }

    [Fact]
    public void ComputeSaldoApertura_IgnoraMovimientosAnuladosOSinDeltaValido()
    {
        var firstSegDate = new DateTime(2026, 2, 1);
        var anulado = Hist("INV_INGRESO", new DateTime(2026, 1, 20), 50m, anulado: true);
        var kgCero = Hist("INV_INGRESO", new DateTime(2026, 1, 21), 0m);
        var tipoNoRelevante = Hist("INV_CONSUMO", new DateTime(2026, 1, 22), 999m);

        var opening = SeguimientoAvesEngordeCalculos.ComputeSaldoAperturaGalponAntesPrimerSeguimiento(
            new[] { anulado, kgCero, tipoNoRelevante }, firstSegDate);

        Assert.Equal(0m, opening);
    }

    // ---------- CalcularSaldoAlimentoPorSeguimiento ----------

    [Fact]
    public void CalcularSaldoAlimentoPorSeguimiento_AperturaMasEventosDelDiaOrdenadosPorOrd_PisaEnCero()
    {
        var apertura = Hist("INV_INGRESO", new DateTime(2026, 1, 15), 50m);
        // Mismo día del primer seguimiento: ord decide el orden, no el ts (h3 tiene ts anterior a h2 mas igual gana ord2 > ord0).
        var ingresoDia1 = Hist("INV_INGRESO", new DateTime(2026, 2, 1), 30m,
            createdAt: new DateTimeOffset(2026, 2, 1, 9, 0, 0, TimeSpan.Zero));
        var salidaDia1 = Hist("INV_TRASLADO_SALIDA", new DateTime(2026, 2, 1), 10m,
            createdAt: new DateTimeOffset(2026, 2, 1, 8, 0, 0, TimeSpan.Zero));

        var seg1 = Seg(1, new DateTime(2026, 2, 1), consumoH: 5m, consumoM: 5m);   // consumo 10
        var seg2 = Seg(2, new DateTime(2026, 2, 2), consumoH: 100m, consumoM: 0m); // consumo 100 > saldo disponible

        var (saldoPorSegId, saldoFinal) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            new[] { apertura, ingresoDia1, salidaDia1 },
            new[] { seg1, seg2 },
            fechaEncaset: null);

        // 50 (apertura) + 30 (ingreso) - 10 (salida) - 10 (consumo seg1) = 60
        Assert.Equal(60m, saldoPorSegId[1]);
        // 60 - 100 = -40 -> piso en 0
        Assert.Equal(0m, saldoPorSegId[2]);
        Assert.Equal(0m, saldoFinal);
        Assert.Equal(2, saldoPorSegId.Count);
    }

    [Fact]
    public void CalcularSaldoAlimentoPorSeguimiento_FechaEncaset_ExcluyeHistoricoAnterior()
    {
        var antesDelEncaset = Hist("INV_INGRESO", new DateTime(2026, 1, 15), 50m);
        var despuesDelEncaset = Hist("INV_INGRESO", new DateTime(2026, 1, 25), 20m);
        var seg = Seg(10, new DateTime(2026, 2, 1), consumoH: 5m, consumoM: 0m);

        var (saldoPorSegId, saldoFinal) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            new[] { antesDelEncaset, despuesDelEncaset },
            new[] { seg },
            fechaEncaset: new DateTime(2026, 1, 20));

        // Solo despuesDelEncaset entra a la apertura: 20 - 5 = 15
        Assert.Equal(15m, saldoPorSegId[10]);
        Assert.Equal(15m, saldoFinal);
    }

    [Fact]
    public void CalcularSaldoAlimentoPorSeguimiento_ConsumoNulo_SeTrataComoCero()
    {
        var apertura = Hist("INV_INGRESO", new DateTime(2026, 1, 15), 50m);
        var seg = Seg(1, new DateTime(2026, 2, 1), consumoH: null, consumoM: null);

        var (saldoPorSegId, saldoFinal) = SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
            new[] { apertura },
            new[] { seg },
            fechaEncaset: null);

        Assert.Equal(50m, saldoPorSegId[1]);
        Assert.Equal(50m, saldoFinal);
    }

    [Fact]
    public void CalcularSaldoAlimentoPorSeguimiento_SegsVacio_Lanza()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SeguimientoAvesEngordeCalculos.CalcularSaldoAlimentoPorSeguimiento(
                Array.Empty<LoteRegistroHistoricoUnificado>(),
                Array.Empty<SeguimientoDiarioAvesEngorde>(),
                fechaEncaset: null));
    }

    // ---------- BuscarCandidatoHistorico ----------

    [Fact]
    public void BuscarCandidatoHistorico_PriorizaMismoMontoYMismoDocumento()
    {
        var h1 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, referencia: "FAC-001", numeroDocumento: "FAC-001", id: 1);
        var h2 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, referencia: "FAC-002", numeroDocumento: "FAC-002", id: 2);

        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { h1, h2 }, new HashSet<long>(), "VENTA", 100m, "FAC-002");

        Assert.Same(h2, candidato);
    }

    [Fact]
    public void BuscarCandidatoHistorico_SinMatchPorDocumento_CaeAMontoSolo_PrimerCandidatoDeLaLista()
    {
        var h1 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, referencia: "FAC-001", numeroDocumento: "FAC-001", id: 1);
        var h2 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, referencia: "FAC-002", numeroDocumento: "FAC-002", id: 2);

        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { h1, h2 }, new HashSet<long>(), "VENTA", 100m, "FAC-999");

        Assert.Same(h1, candidato);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuscarCandidatoHistorico_DocumentoNuloOVacio_UsaSoloMonto(string? documento)
    {
        var h1 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, referencia: "FAC-001", numeroDocumento: "FAC-001", id: 1);

        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { h1 }, new HashSet<long>(), "VENTA", 100m, documento);

        Assert.Same(h1, candidato);
    }

    [Fact]
    public void BuscarCandidatoHistorico_ExcluyeIdsYaUsados()
    {
        var h1 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, id: 1);
        var h2 = Hist("VENTA", new DateTime(2026, 1, 1), 100m, id: 2);

        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { h1, h2 }, new HashSet<long> { 1 }, "VENTA", 100m, null);

        Assert.Same(h2, candidato);
    }

    [Fact]
    public void BuscarCandidatoHistorico_FiltraPorTipoEvento()
    {
        var h1 = Hist("COMPRA", new DateTime(2026, 1, 1), 100m, id: 1);

        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { h1 }, new HashSet<long>(), "VENTA", 100m, null);

        Assert.Null(candidato);
    }

    [Fact]
    public void BuscarCandidatoHistorico_SinCandidatos_DevuelveNull()
    {
        var candidato = SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            Array.Empty<LoteRegistroHistoricoUnificado>(), new HashSet<long>(), "VENTA", 100m, "FAC-001");

        Assert.Null(candidato);
    }

    [Fact]
    public void BuscarCandidatoHistorico_ToleranciaMonto_LimiteExclusivo()
    {
        // Diferencia exacta 0.001 no matchea (condición es estrictamente <).
        var enElLimite = Hist("VENTA", new DateTime(2026, 1, 1), 100.001m, id: 1);

        Assert.Null(SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { enElLimite }, new HashSet<long>(), "VENTA", 100m, null));

        var dentroDelLimite = Hist("VENTA", new DateTime(2026, 1, 1), 100.0009m, id: 2);

        Assert.Same(dentroDelLimite, SeguimientoAvesEngordeCalculos.BuscarCandidatoHistorico(
            new[] { dentroDelLimite }, new HashSet<long>(), "VENTA", 100m, null));
    }

    // ---------- MetadataYaTieneCamposKardex ----------

    [Fact]
    public void MetadataYaTieneCamposKardex_Null_DevuelveFalse()
    {
        Assert.False(SeguimientoAvesEngordeCalculos.MetadataYaTieneCamposKardex(null));
    }

    [Fact]
    public void MetadataYaTieneCamposKardex_RootNoEsObjeto_DevuelveFalse()
    {
        using var doc = JsonDocument.Parse("[1,2,3]");
        Assert.False(SeguimientoAvesEngordeCalculos.MetadataYaTieneCamposKardex(doc));
    }

    [Fact]
    public void MetadataYaTieneCamposKardex_ObjetoSinCamposRelevantes_DevuelveFalse()
    {
        using var doc = JsonDocument.Parse("{\"otro\":\"valor\"}");
        Assert.False(SeguimientoAvesEngordeCalculos.MetadataYaTieneCamposKardex(doc));
    }

    [Theory]
    [InlineData("{\"ingresoAlimento\":\"algo\"}", true)]
    [InlineData("{\"ingresoAlimento\":\"\"}", false)]
    [InlineData("{\"ingresoAlimento\":\"   \"}", false)]
    [InlineData("{\"traslado\":\"x\"}", true)]
    [InlineData("{\"documento\":\"x\"}", true)]
    [InlineData("{\"despachoHembras\":\"x\"}", true)]
    [InlineData("{\"despachoMachos\":\"x\"}", true)]
    [InlineData("{\"ingresoAlimento\":0}", false)]
    [InlineData("{\"ingresoAlimento\":5}", true)]
    [InlineData("{\"ingresoAlimento\":true}", true)]
    [InlineData("{\"ingresoAlimento\":false}", false)]
    public void MetadataYaTieneCamposKardex_EvaluaCadaCampoSegunSuTipo(string json, bool esperado)
    {
        using var doc = JsonDocument.Parse(json);
        Assert.Equal(esperado, SeguimientoAvesEngordeCalculos.MetadataYaTieneCamposKardex(doc));
    }
}
