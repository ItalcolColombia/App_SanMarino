// tests/ZooSanMarino.Application.Tests/TicketMetricasCalculosTests.cs
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

public class TicketMetricasCalculosTests
{
    private static readonly DateTime Ahora = new(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);

    // ── Semáforo de SLA ──────────────────────────────────────────────────────

    [Fact]
    public void SinFechaLimite_NoHaySla()
    {
        Assert.Equal(TicketMetricasCalculos.SlaSinCompromiso,
            TicketMetricasCalculos.EstadoSla(null, null, Ahora));
        Assert.Null(TicketMetricasCalculos.HorasParaVencer(null, null, Ahora));
    }

    [Fact]
    public void ConMargenAmplio_EstaEnTiempo()
        => Assert.Equal(TicketMetricasCalculos.SlaEnTiempo,
            TicketMetricasCalculos.EstadoSla(Ahora.AddDays(5), null, Ahora));

    [Fact]
    public void DentroDeLasProximas24Horas_EstaPorVencer()
        => Assert.Equal(TicketMetricasCalculos.SlaPorVencer,
            TicketMetricasCalculos.EstadoSla(Ahora.AddHours(6), null, Ahora));

    [Fact]
    public void ExactamenteEnElUmbral_TodaviaEsPorVencer()
        => Assert.Equal(TicketMetricasCalculos.SlaPorVencer,
            TicketMetricasCalculos.EstadoSla(
                Ahora.AddHours(TicketMetricasCalculos.HorasUmbralPorVencer), null, Ahora));

    [Fact]
    public void PasadaLaFechaSinSolucion_EstaVencido()
        => Assert.Equal(TicketMetricasCalculos.SlaVencido,
            TicketMetricasCalculos.EstadoSla(Ahora.AddHours(-1), null, Ahora));

    [Fact]
    public void SolucionadoATiempo_QuedaCumplidoAunqueLaFechaYaPaso()
    {
        // El caso se resolvió el día 1 con compromiso al día 2; hoy es el día 6.
        var limite   = new DateTime(2026, 8, 2, 0, 0, 0, DateTimeKind.Utc);
        var solucion = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TicketMetricasCalculos.SlaCumplido,
            TicketMetricasCalculos.EstadoSla(limite, solucion, Ahora));
    }

    [Fact]
    public void SolucionadoTarde_QuedaIncumplidoParaSiempre()
    {
        var limite   = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var solucion = new DateTime(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TicketMetricasCalculos.SlaIncumplido,
            TicketMetricasCalculos.EstadoSla(limite, solucion, Ahora));
    }

    [Fact]
    public void HorasParaVencer_SeCongelaEnLaFechaDeSolucion()
    {
        var limite   = new DateTime(2026, 8, 3, 12, 0, 0, DateTimeKind.Utc);
        var solucion = new DateTime(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(24, TicketMetricasCalculos.HorasParaVencer(limite, solucion, Ahora));
    }

    [Fact]
    public void HorasParaVencer_EsNegativoSiYaVencio()
        => Assert.Equal(-2, TicketMetricasCalculos.HorasParaVencer(Ahora.AddHours(-2), null, Ahora));

    // ── Tiempos del caso ─────────────────────────────────────────────────────

    [Fact]
    public void PrimeraRespuesta_NullMientrasNadieLoTomo()
        => Assert.Null(TicketMetricasCalculos.HorasPrimeraRespuesta(Ahora, null));

    [Fact]
    public void PrimeraRespuesta_CuentaDesdeLaCreacion()
        => Assert.Equal(3.5, TicketMetricasCalculos.HorasPrimeraRespuesta(Ahora, Ahora.AddHours(3.5)));

    [Fact]
    public void FechasIncoherentes_NuncaDevuelvenHorasNegativas()
        => Assert.Equal(0, TicketMetricasCalculos.HorasPrimeraRespuesta(Ahora, Ahora.AddHours(-5)));

    [Fact]
    public void Resolucion_SinSolucionCuentaElTranscurrido()
        => Assert.Equal(10, TicketMetricasCalculos.HorasResolucion(Ahora.AddHours(-10), null, Ahora));

    [Fact]
    public void Resolucion_ConSolucionSeCongela()
    {
        var creado   = Ahora.AddHours(-48);
        var solucion = Ahora.AddHours(-24);
        Assert.Equal(24, TicketMetricasCalculos.HorasResolucion(creado, solucion, Ahora));
    }

    [Fact]
    public void ConfirmacionDeCierre_NullSiFaltaAlgunHito()
    {
        Assert.Null(TicketMetricasCalculos.HorasConfirmacionCierre(null, Ahora));
        Assert.Null(TicketMetricasCalculos.HorasConfirmacionCierre(Ahora, null));
    }

    [Fact]
    public void ConfirmacionDeCierre_MideDeSolucionACierre()
        => Assert.Equal(6, TicketMetricasCalculos.HorasConfirmacionCierre(Ahora, Ahora.AddHours(6)));

    // ── Permanencia por estado ───────────────────────────────────────────────

    [Fact]
    public void SinCambios_TodoElTiempoQuedaEnAbierto()
    {
        var creado = Ahora.AddHours(-10);
        var r = TicketMetricasCalculos.PermanenciaPorEstado(
            creado, Array.Empty<TicketMetricasCalculos.CambioEstado>(), Ahora);

        Assert.Single(r);
        Assert.Equal(TicketEstados.Abierto, r[0].Estado);
        Assert.Equal(10, r[0].Horas);
    }

    [Fact]
    public void RepartExactoEntreLosTramos()
    {
        var creado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var cambios = new[]
        {
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis,       creado.AddHours(4)),
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnImplementacion, creado.AddHours(10)),
        };
        var r = TicketMetricasCalculos.PermanenciaPorEstado(creado, cambios, creado.AddHours(20))
                                      .ToDictionary(x => x.Estado, x => x.Horas);

        Assert.Equal(4,  r[TicketEstados.Abierto]);           // 0 → 4
        Assert.Equal(6,  r[TicketEstados.EnAnalisis]);        // 4 → 10
        Assert.Equal(10, r[TicketEstados.EnImplementacion]);  // 10 → 20
        Assert.Equal(20, r.Values.Sum());                     // sin fugas
    }

    [Fact]
    public void CambiosDesordenados_SeOrdenanAntesDeRepartir()
    {
        var creado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var desordenados = new[]
        {
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnImplementacion, creado.AddHours(10)),
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis,       creado.AddHours(4)),
        };
        var r = TicketMetricasCalculos.PermanenciaPorEstado(creado, desordenados, creado.AddHours(20))
                                      .ToDictionary(x => x.Estado, x => x.Horas);

        Assert.Equal(6, r[TicketEstados.EnAnalisis]);
        Assert.Equal(10, r[TicketEstados.EnImplementacion]);
    }

    [Fact]
    public void CambiosRepetidosConsecutivos_NoAbrenTramoNuevo()
    {
        var creado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var cambios = new[]
        {
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis, creado.AddHours(2)),
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis, creado.AddHours(5)),
        };
        var r = TicketMetricasCalculos.PermanenciaPorEstado(creado, cambios, creado.AddHours(8));

        Assert.Equal(2, r.Count);
        Assert.Equal(6, r.First(x => x.Estado == TicketEstados.EnAnalisis).Horas);   // 2 → 8
    }

    [Fact]
    public void VolverAUnEstadoAcumulaSusDosTramos()
    {
        var creado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var cambios = new[]
        {
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis,       creado.AddHours(1)),
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnImplementacion, creado.AddHours(3)),
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis,       creado.AddHours(4)),
        };
        var r = TicketMetricasCalculos.PermanenciaPorEstado(creado, cambios, creado.AddHours(6))
                                      .ToDictionary(x => x.Estado, x => x.Horas);

        Assert.Equal(4, r[TicketEstados.EnAnalisis]);   // (1→3) + (4→6)
        Assert.Equal(1, r[TicketEstados.EnImplementacion]);
    }

    [Fact]
    public void CorteAnteriorAlUltimoTramo_NoGeneraHorasNegativas()
    {
        var creado = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);
        var cambios = new[]
        {
            new TicketMetricasCalculos.CambioEstado(TicketEstados.EnAnalisis, creado.AddHours(10)),
        };
        var r = TicketMetricasCalculos.PermanenciaPorEstado(creado, cambios, creado.AddHours(5));

        Assert.All(r, p => Assert.True(p.Horas >= 0));
    }

    // ── Avance ───────────────────────────────────────────────────────────────

    [Fact]
    public void SinTareas_ElAvancePorTareasEsCero()
        => Assert.Equal(0m, TicketMetricasCalculos.PorcentajeAvanceTareas(0, 0));

    [Theory]
    [InlineData(4, 1, 25.0)]
    [InlineData(3, 1, 33.3)]
    [InlineData(5, 5, 100.0)]
    public void AvancePorTareas(int total, int listas, double esperado)
        => Assert.Equal((decimal)esperado, TicketMetricasCalculos.PorcentajeAvanceTareas(total, listas));

    [Fact]
    public void MasListasQueTotal_SeRecortaA100()
        => Assert.Equal(100m, TicketMetricasCalculos.PorcentajeAvanceTareas(2, 7));

    [Fact]
    public void SinEstimacion_NoHayDesvio()
    {
        Assert.Null(TicketMetricasCalculos.DesvioHoras(null, 10m));
        Assert.Null(TicketMetricasCalculos.DesvioHoras(0m, 10m));
    }

    [Fact]
    public void Desvio_PositivoCuandoSePasoDeLaEstimacion()
        => Assert.Equal(2.5m, TicketMetricasCalculos.DesvioHoras(8m, 10.5m));

    [Fact]
    public void Desvio_NegativoCuandoQuedoPorDebajo()
        => Assert.Equal(-3m, TicketMetricasCalculos.DesvioHoras(8m, 5m));

    [Fact]
    public void AvanceDeFlujo_VaDeCeroACien()
    {
        Assert.Equal(0m, TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.Abierto));
        Assert.Equal(100m, TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.Cerrado));
        Assert.True(TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.EnRevision) >
                    TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.EnAnalisis));
    }

    [Fact]
    public void AvanceDeFlujo_EstadosEspecialesDevuelvenCero()
    {
        Assert.Equal(0m, TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.Suspendido));
        Assert.Equal(0m, TicketMetricasCalculos.PorcentajeAvanceFlujo(TicketEstados.Transferido));
        Assert.Equal(0m, TicketMetricasCalculos.PorcentajeAvanceFlujo(null));
    }
}
