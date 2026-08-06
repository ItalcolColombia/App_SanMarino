using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Santa Reyes — Fase 3. Edades por COHORTE (cada grupo de aves cuenta su edad desde el
/// encasetamiento de su lote de origen) y habilitación del traslado CROSS-ETAPA por empresa.
/// La fórmula de semanas es la misma del resto del sistema (días/7 + 1 ⇒ día 0 = semana 1).
/// </summary>
public class LoteCohortesCalculosTests
{
    private static readonly DateOnly Encaset = new(2026, 1, 1);

    // ── Edad en días ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, 0)]     // día del encasetamiento
    [InlineData(1, 1)]
    [InlineData(6, 6)]
    [InlineData(7, 7)]
    [InlineData(133, 133)] // 19 semanas cumplidas → semana 20
    public void EdadDias_CuentaDiasTranscurridosDesdeElEncaset(int diasTranscurridos, int esperado)
    {
        var fecha = Encaset.AddDays(diasTranscurridos);

        Assert.Equal(esperado, LoteCohortesCalculos.EdadDias(Encaset, fecha));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-30)]
    public void EdadDias_FechaAnteriorAlEncaset_ClampeaEnCero(int diasAntes)
    {
        var fecha = Encaset.AddDays(diasAntes);

        Assert.Equal(0, LoteCohortesCalculos.EdadDias(Encaset, fecha));
    }

    // ── Edad en semanas (misma aritmética que MovimientoAvesCalculos) ────

    [Theory]
    [InlineData(0, 1)]     // día 0 → semana 1
    [InlineData(6, 1)]     // último día de la semana 1
    [InlineData(7, 2)]     // primer día de la semana 2
    [InlineData(13, 2)]
    [InlineData(14, 3)]
    [InlineData(133, 20)]  // 20 semanas
    [InlineData(139, 20)]  // último día de la semana 20
    [InlineData(140, 21)]
    public void EdadSemanas_DivisionEnteraPor7MasUno(int diasTranscurridos, int semanaEsperada)
    {
        var fecha = Encaset.AddDays(diasTranscurridos);

        Assert.Equal(semanaEsperada, LoteCohortesCalculos.EdadSemanas(Encaset, fecha));
    }

    [Fact]
    public void EdadSemanas_FechaAnteriorAlEncaset_ClampeaEnLaSemanaUno()
    {
        var fecha = Encaset.AddDays(-10);

        Assert.Equal(1, LoteCohortesCalculos.EdadSemanas(Encaset, fecha));
    }

    [Fact]
    public void EdadSemanas_EsEquivalenteAlCalculoDelRestoDelSistema()
    {
        for (var dias = 0; dias <= 400; dias++)
        {
            var fecha = Encaset.AddDays(dias);
            var esperado = MovimientoAvesCalculos.SemanaDesdeEncaset(
                Encaset.ToDateTime(TimeOnly.MinValue).AddDays(dias),
                Encaset.ToDateTime(TimeOnly.MinValue));

            Assert.Equal(esperado, LoteCohortesCalculos.EdadSemanas(Encaset, fecha));
        }
    }

    // ── Etapas ───────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Levante", "Levante")]
    [InlineData("Produccion", "Produccion")]
    [InlineData("levante", "LEVANTE")]   // comparación case-insensitive (como el servicio)
    public void EsMismaEtapa_IgnoraMayusculas(string origen, string destino)
    {
        Assert.True(LoteCohortesCalculos.EsMismaEtapa(origen, destino));
    }

    [Fact]
    public void EsMismaEtapa_EtapasDistintas_False()
    {
        Assert.False(LoteCohortesCalculos.EsMismaEtapa("Levante", "Produccion"));
    }

    [Theory]
    [InlineData("Levante", true)]
    [InlineData("levante", true)]
    [InlineData("Produccion", false)]
    [InlineData(null, false)]
    public void EsLevante_SoloLevanteEsLevante(string? tipo, bool esperado)
    {
        Assert.Equal(esperado, LoteCohortesCalculos.EsLevante(tipo));
    }

    // ── Habilitación del traslado ────────────────────────────────────────

    [Theory]
    [InlineData(false, "Levante", "Levante")]
    [InlineData(true, "Levante", "Levante")]
    [InlineData(false, "Produccion", "Produccion")]
    [InlineData(true, "Produccion", "Produccion")]
    public void PuedeTrasladar_MismaEtapa_SiempreTrue(bool companyPermite, string origen, string destino)
    {
        Assert.True(LoteCohortesCalculos.PuedeTrasladarCrossEtapa(companyPermite, origen, destino));
    }

    [Theory]
    [InlineData("Levante", "Produccion")]
    [InlineData("Produccion", "Levante")]
    public void PuedeTrasladar_CrossEtapaSinFlag_False(string origen, string destino)
    {
        Assert.False(LoteCohortesCalculos.PuedeTrasladarCrossEtapa(companyPermite: false, origen, destino));
    }

    [Fact]
    public void PuedeTrasladar_LevanteAProduccionConFlag_True()
    {
        Assert.True(LoteCohortesCalculos.PuedeTrasladarCrossEtapa(companyPermite: true, "Levante", "Produccion"));
    }

    [Fact]
    public void PuedeTrasladar_ProduccionALevanteConFlag_NuncaSePermite()
    {
        Assert.False(LoteCohortesCalculos.PuedeTrasladarCrossEtapa(companyPermite: true, "Produccion", "Levante"));
    }

    [Fact]
    public void MensajeCrossEtapaBloqueado_ConservaElTextoHistorico()
    {
        var mensaje = LoteCohortesCalculos.MensajeCrossEtapaBloqueado("Levante", "Produccion");

        Assert.Equal(
            "No se permite cross-phase: origen=Levante no coincide con destino=Produccion. " +
            "Sólo se puede trasladar dentro de la misma etapa (Levante→Levante o Producción→Producción).",
            mensaje);
    }

    // ── Techo de venta del lote receptor (engorde) ────────────────────────────────────────

    [Fact]
    public void BaselineConCohortes_SinCohortes_DevuelveElInicioIntacto()
    {
        // Retrocompatibilidad: TODOS los lotes actuales caen acá y su techo no se puede mover.
        var r = LoteCohortesCalculos.BaselineConCohortes((10_000, 500, 0), (0, 0, 0));

        Assert.Equal((10_000, 500, 0), r);
    }

    [Fact]
    public void BaselineConCohortes_SumaLasAvesRecibidas()
    {
        // El lote arrancó con 10.000 H y recibió 1.500 H de otro lote: puede vender 11.500.
        var r = LoteCohortesCalculos.BaselineConCohortes((10_000, 500, 0), (1_500, 200, 50));

        Assert.Equal((11_500, 700, 50), r);
    }

    [Fact]
    public void BaselineConCohortes_LoteMixto_SumaSoloEnSuBucket()
    {
        var r = LoteCohortesCalculos.BaselineConCohortes((0, 0, 8_000), (0, 0, 1_000));

        Assert.Equal((0, 0, 9_000), r);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, -5)]
    [InlineData(-5, -5)]
    public void BaselineConCohortes_ClampeaNegativosDeLaBd(int inicio, int recibidas)
    {
        var r = LoteCohortesCalculos.BaselineConCohortes((inicio, 0, 0), (recibidas, 0, 0));

        Assert.Equal(0, r.Hembras);
    }

    // ── Aves propias del lote ────────────────────────────────────────────────────────────

    [Fact]
    public void PropiasDelLote_RestaLoRecibidoDelSaldo()
    {
        Assert.Equal(8_500, LoteCohortesCalculos.PropiasDelLote(saldoActual: 10_000, recibidasVigentes: 1_500));
    }

    [Fact]
    public void PropiasDelLote_SinCohortes_EsTodoElSaldo()
    {
        Assert.Equal(10_000, LoteCohortesCalculos.PropiasDelLote(saldoActual: 10_000, recibidasVigentes: 0));
    }

    [Fact]
    public void PropiasDelLote_SiLasBajasSeComieronLasPropias_NoDevuelveNegativo()
    {
        // Las bajas se registran por lote, no por cohorte: pueden dejar el saldo por debajo de lo recibido.
        Assert.Equal(0, LoteCohortesCalculos.PropiasDelLote(saldoActual: 900, recibidasVigentes: 1_500));
    }
}
