using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.FlujoValidacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de topología y transiciones del motor de flujos de validación parametrizables
/// (fase_de_desarrollo/flujos_validacion_parametrizables_por_empresa_plan.md, casos 6, 11-21).
/// </summary>
public class FlujoValidacionCalculosTests
{
    // ─── Topología para publicar ───────────────────────────────────────────────

    [Fact]
    public void Topologia_TresEtapasConCandidatos_EsPublicable()
    {
        var pasos = new List<PasoTopologia>
        {
            new(1, true), new(2, true), new(3, true)
        };

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Empty(errores);
    }

    [Fact]
    public void Topologia_CeroEtapas_NoEsPublicable()
    {
        var errores = ValidarTopologiaParaPublicar(new List<PasoTopologia>());

        Assert.Single(errores);
    }

    [Fact]
    public void Topologia_VeintiunEtapas_ExcedeElLimite()
    {
        var pasos = Enumerable.Range(1, 21).Select(o => new PasoTopologia(o, true)).ToList();

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Contains(errores, e => e.Contains("entre 1 y 20"));
    }

    [Fact]
    public void Topologia_VeinteEtapas_EsElLimiteValido()
    {
        var pasos = Enumerable.Range(1, 20).Select(o => new PasoTopologia(o, true)).ToList();

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Empty(errores);
    }

    [Fact]
    public void Topologia_OrdenesRepetidos_NoEsPublicable()
    {
        var pasos = new List<PasoTopologia> { new(1, true), new(1, true), new(3, true) };

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Contains(errores, e => e.Contains("consecutivo"));
    }

    [Fact]
    public void Topologia_ConHueco_NoEsPublicable()
    {
        var pasos = new List<PasoTopologia> { new(1, true), new(3, true) };

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Contains(errores, e => e.Contains("consecutivo"));
    }

    [Fact]
    public void Topologia_EtapaSinCandidatos_NoEsPublicable()
    {
        var pasos = new List<PasoTopologia> { new(1, true), new(2, false), new(3, true) };

        var errores = ValidarTopologiaParaPublicar(pasos);

        Assert.Contains(errores, e => e.Contains("etapa 2") && e.Contains("candidato"));
    }

    // ─── Avance / última etapa ─────────────────────────────────────────────────

    [Fact]
    public void EsUltimaEtapa_CuandoElOrdenIgualaElTotal()
    {
        Assert.True(EsUltimaEtapa(3, totalPasos: 3));
        Assert.False(EsUltimaEtapa(2, totalPasos: 3));
    }

    [Fact]
    public void SiguienteEtapa_Suma1()
    {
        Assert.Equal(2, SiguienteEtapa(1));
        Assert.Equal(3, SiguienteEtapa(2));
    }

    // ─── Devolución escalonada (casos 23-27 del plan) ──────────────────────────

    [Fact]
    public void EtapaDestinoAlDevolver_RetrocedeExactamenteUna()
    {
        Assert.Equal(2, EtapaDestinoAlDevolver(3));
        Assert.Equal(1, EtapaDestinoAlDevolver(2));
    }

    [Fact]
    public void EtapaDestinoAlDevolver_DesdeLaPrimera_DaCero()
    {
        // 0 = no hay etapa anterior: responsabilidad del creador (regla §4.10 del plan).
        Assert.Equal(0, EtapaDestinoAlDevolver(1));
    }

    [Fact]
    public void DevolucionApuntaAlCreador_SoloCuandoSeDevuelveLaEtapa1()
    {
        Assert.True(DevolucionApuntaAlCreador(1));
        Assert.False(DevolucionApuntaAlCreador(2));
        Assert.False(DevolucionApuntaAlCreador(3));
    }

    [Fact]
    public void EtapaTrasReenviar_VuelveAlPuntoDeRetorno()
    {
        Assert.Equal(2, EtapaTrasReenviar(pasoRetornoOrden: 2));
    }

    // ─── Plazo (default 24h, caso §4.4 del plan) ───────────────────────────────

    [Fact]
    public void CalcularVencimiento_Default24Horas()
    {
        var creada = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);

        var vencimiento = CalcularVencimiento(creada, plazoTotalHoras: 24);

        Assert.Equal(new DateTime(2026, 9, 24, 8, 0, 0, DateTimeKind.Utc), vencimiento);
    }

    [Fact]
    public void EstaVencida_TrasElPlazo()
    {
        var creada = new DateTime(2026, 9, 23, 8, 0, 0, DateTimeKind.Utc);

        Assert.False(EstaVencida(creada, 24, ahoraUtc: creada.AddHours(23)));
        Assert.True(EstaVencida(creada, 24, ahoraUtc: creada.AddHours(25)));
    }
}
