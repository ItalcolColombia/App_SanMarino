// tests/ZooSanMarino.Application.Tests/TicketEstadosTransicionesTests.cs
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Gate de NO-REGRESIÓN de la máquina de estados del caso. Ampliar el flujo con EN_DOCUMENTACION
/// y EN_REVISION tiene que ser estrictamente aditivo: toda transición que era válida antes lo
/// sigue siendo, y las dos reglas de negocio duras (CERRADO terminal, solo alcanzable por la
/// confirmación del solicitante) se conservan.
/// </summary>
public class TicketEstadosTransicionesTests
{
    /// <summary>Transiciones tal como estaban ANTES de agregar las dos fases nuevas.</summary>
    public static TheoryData<string, string> TransicionesHistoricas() => new()
    {
        { TicketEstados.Abierto,          TicketEstados.EnAnalisis },
        { TicketEstados.Abierto,          TicketEstados.Suspendido },
        { TicketEstados.Abierto,          TicketEstados.Transferido },
        { TicketEstados.EnAnalisis,       TicketEstados.EnImplementacion },
        { TicketEstados.EnAnalisis,       TicketEstados.Solucionado },
        { TicketEstados.EnAnalisis,       TicketEstados.Suspendido },
        { TicketEstados.EnAnalisis,       TicketEstados.Transferido },
        { TicketEstados.EnImplementacion, TicketEstados.Solucionado },
        { TicketEstados.EnImplementacion, TicketEstados.EnAnalisis },
        { TicketEstados.EnImplementacion, TicketEstados.Suspendido },
        { TicketEstados.EnImplementacion, TicketEstados.Transferido },
        { TicketEstados.Solucionado,      TicketEstados.EnAnalisis },
        { TicketEstados.Solucionado,      TicketEstados.Cerrado },
        { TicketEstados.Transferido,      TicketEstados.EnAnalisis },
        { TicketEstados.Transferido,      TicketEstados.Suspendido },
        { TicketEstados.Suspendido,       TicketEstados.EnAnalisis },
    };

    [Theory]
    [MemberData(nameof(TransicionesHistoricas))]
    public void TodaTransicionPreviaSigueSiendoValida(string desde, string hacia)
    {
        Assert.True(TicketEstados.PuedeTransicionar(desde, hacia),
            $"La transición {desde} → {hacia} existía antes del cambio y dejó de ser válida.");
    }

    [Fact]
    public void LasCuatroFasesDeTrabajoSeMuevenLibrementeEntreSi()
    {
        foreach (var desde in TicketEstados.FasesTrabajo)
        foreach (var hacia in TicketEstados.FasesTrabajo)
        {
            if (desde == hacia) continue;
            Assert.True(TicketEstados.PuedeTransicionar(desde, hacia), $"Falta {desde} → {hacia}.");
        }
    }

    [Theory]
    [InlineData(TicketEstados.EnAnalisis)]
    [InlineData(TicketEstados.EnDocumentacion)]
    [InlineData(TicketEstados.EnImplementacion)]
    [InlineData(TicketEstados.EnRevision)]
    public void DesdeCualquierFaseSePuedeSolucionarSuspenderYTransferir(string fase)
    {
        Assert.True(TicketEstados.PuedeTransicionar(fase, TicketEstados.Solucionado));
        Assert.True(TicketEstados.PuedeTransicionar(fase, TicketEstados.Suspendido));
        Assert.True(TicketEstados.PuedeTransicionar(fase, TicketEstados.Transferido));
    }

    [Fact]
    public void CerradoSigueSiendoTerminal()
    {
        Assert.Empty(TicketEstados.Transiciones[TicketEstados.Cerrado]);
        foreach (var estado in TicketEstados.Todos)
            Assert.False(TicketEstados.PuedeTransicionar(TicketEstados.Cerrado, estado));
    }

    [Fact]
    public void CerradoSoloSeAlcanzaDesdeSolucionado()
    {
        foreach (var (desde, destinos) in TicketEstados.Transiciones)
        {
            if (desde.Equals(TicketEstados.Solucionado, StringComparison.OrdinalIgnoreCase)) continue;
            Assert.DoesNotContain(TicketEstados.Cerrado, destinos);
        }
    }

    [Fact]
    public void SuspendidoReactivaEnCualquierFaseDeTrabajo()
    {
        foreach (var fase in TicketEstados.FasesTrabajo)
            Assert.True(TicketEstados.PuedeTransicionar(TicketEstados.Suspendido, fase));
    }

    [Fact]
    public void NoSeSaltaDeAbiertoASolucionadoNiACerrado()
    {
        Assert.False(TicketEstados.PuedeTransicionar(TicketEstados.Abierto, TicketEstados.Solucionado));
        Assert.False(TicketEstados.PuedeTransicionar(TicketEstados.Abierto, TicketEstados.Cerrado));
    }

    [Fact]
    public void LosDosEstadosNuevosSonValidosYEntranEnElFlujoLineal()
    {
        Assert.True(TicketEstados.EsValido(TicketEstados.EnDocumentacion));
        Assert.True(TicketEstados.EsValido(TicketEstados.EnRevision));
        Assert.Contains(TicketEstados.EnDocumentacion, TicketEstados.FlujoLineal);
        Assert.Contains(TicketEstados.EnRevision, TicketEstados.FlujoLineal);
    }

    [Fact]
    public void ElFlujoLinealVaDeAbiertoACerradoEnOrden()
    {
        Assert.Equal(TicketEstados.Abierto, TicketEstados.FlujoLineal.First());
        Assert.Equal(TicketEstados.Cerrado, TicketEstados.FlujoLineal.Last());
        Assert.True(TicketEstados.OrdenDe(TicketEstados.EnAnalisis) < TicketEstados.OrdenDe(TicketEstados.EnRevision));
        Assert.True(TicketEstados.OrdenDe(TicketEstados.EnRevision) < TicketEstados.OrdenDe(TicketEstados.Solucionado));
    }

    [Fact]
    public void EstadosEspecialesNoTienenPosicionEnElFlujo()
    {
        Assert.Equal(-1, TicketEstados.OrdenDe(TicketEstados.Transferido));
        Assert.Equal(-1, TicketEstados.OrdenDe(TicketEstados.Suspendido));
        Assert.Equal(-1, TicketEstados.OrdenDe(null));
    }

    [Fact]
    public void TodoDestinoDeclaradoEsUnEstadoValido()
    {
        foreach (var (_, destinos) in TicketEstados.Transiciones)
        foreach (var destino in destinos)
            Assert.True(TicketEstados.EsValido(destino), $"Destino desconocido: {destino}");
    }

    // ── Prioridades ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("critica", 0)]
    [InlineData("ALTA", 1)]
    [InlineData("Media", 2)]
    [InlineData("baja", 3)]
    public void PrioridadOrdenaCriticaPrimero(string prioridad, int pesoEsperado)
        => Assert.Equal(pesoEsperado, TicketPrioridades.Peso(prioridad));

    [Fact]
    public void PrioridadDesconocidaPesaComoMedia()
    {
        Assert.Equal(TicketPrioridades.Peso(TicketPrioridades.Media), TicketPrioridades.Peso("URGENTISIMA"));
        Assert.Equal(TicketPrioridades.Peso(TicketPrioridades.Media), TicketPrioridades.Peso(null));
    }
}
