using System;
using Xunit;
using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.GastoLoteProgramadoCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reglas del gasto de inventario cargado contra un lote PROGRAMADO (desinsectación previa al
/// encaset) y de su re-atribución al lote real. Con lotes base REUTILIZABLES, el corte por fecha es
/// lo único que impide que un lote nuevo se lleve los insumos alistados para el ciclo siguiente.
/// </summary>
public class GastoLoteProgramadoCalculosTests
{
    private static readonly DateTime Encaset = new(2026, 8, 20);

    private static LoteCreado Lote(int farmId = 7, string? galpon = "G1", int? baseId = 42, DateTime? encaset = null)
        => new(farmId, galpon, baseId, encaset ?? Encaset);

    private static GastoPendiente Gasto(
        string estado = "Activo",
        int farmId = 7,
        string? galpon = "G1",
        int? loteReal = null,
        int? loteBase = 42,
        DateTime? fecha = null)
        => new(estado, farmId, galpon, loteReal, loteBase, fecha ?? new DateTime(2026, 8, 15));

    // ── Destino excluyente ───────────────────────────────────────────────────

    [Fact]
    public void ValidarDestino_SoloLoteReal_EsValido()
        => Assert.Null(ValidarDestino(loteAveEngordeId: 10, loteBaseEngordeId: null));

    [Fact]
    public void ValidarDestino_SoloLoteProgramado_EsValido()
        => Assert.Null(ValidarDestino(loteAveEngordeId: null, loteBaseEngordeId: 42));

    [Fact]
    public void ValidarDestino_SinLote_EsValido_PorqueEsGastoDeGranja()
        => Assert.Null(ValidarDestino(loteAveEngordeId: null, loteBaseEngordeId: null));

    [Fact]
    public void ValidarDestino_AmbosLotes_Rechaza()
        => Assert.NotNull(ValidarDestino(loteAveEngordeId: 10, loteBaseEngordeId: 42));

    // ── Re-atribución ────────────────────────────────────────────────────────

    [Fact]
    public void DebeReatribuir_MismaGranjaBaseYGalpon_AntesDelEncaset_Reatribuye()
        => Assert.True(DebeReatribuir(Gasto(), Lote()));

    [Fact]
    public void DebeReatribuir_GastoDeGranjaSinGalpon_Reatribuye()
        => Assert.True(DebeReatribuir(Gasto(galpon: null), Lote()));

    [Fact]
    public void DebeReatribuir_MismoDiaDelEncaset_Reatribuye()
        => Assert.True(DebeReatribuir(Gasto(fecha: Encaset), Lote()));

    [Fact]
    public void DebeReatribuir_GalponDistinto_NoReatribuye()
        => Assert.False(DebeReatribuir(Gasto(galpon: "G2"), Lote()));

    [Fact]
    public void DebeReatribuir_OtraGranja_NoReatribuye()
        => Assert.False(DebeReatribuir(Gasto(farmId: 99), Lote()));

    [Fact]
    public void DebeReatribuir_OtroLoteBase_NoReatribuye()
        => Assert.False(DebeReatribuir(Gasto(loteBase: 43), Lote()));

    [Fact]
    public void DebeReatribuir_FechaPosteriorAlEncaset_NoReatribuye_EsDelCicloSiguiente()
        => Assert.False(DebeReatribuir(Gasto(fecha: Encaset.AddDays(1)), Lote()));

    [Fact]
    public void DebeReatribuir_GastoEliminado_NoReatribuye_SuStockYaVolvio()
        => Assert.False(DebeReatribuir(Gasto(estado: "Eliminado"), Lote()));

    [Fact]
    public void DebeReatribuir_GastoYaAtribuido_NoSeMueve()
        => Assert.False(DebeReatribuir(Gasto(loteReal: 500), Lote()));

    [Fact]
    public void DebeReatribuir_LoteSinBase_NoBarreNada()
        => Assert.False(DebeReatribuir(Gasto(), Lote(baseId: null)));

    [Fact]
    public void DebeReatribuir_GastoSinBase_NoEsPendiente()
        => Assert.False(DebeReatribuir(Gasto(loteBase: null), Lote()));

    /// <summary>
    /// Base reutilizable: el 2º lote del mismo base+galpón solo puede tomar lo que el 1º no reclamó.
    /// El gasto del 25-ago queda fuera del lote encasetado el 20-ago y entra en el del 30-ago.
    /// </summary>
    [Fact]
    public void DebeReatribuir_SegundaCorrida_SoloTomaLoNoReclamado()
        {
            var gastoDelCicloSiguiente = Gasto(fecha: new DateTime(2026, 8, 25));

            Assert.False(DebeReatribuir(gastoDelCicloSiguiente, Lote(encaset: Encaset)));
            Assert.True(DebeReatribuir(gastoDelCicloSiguiente, Lote(encaset: new DateTime(2026, 8, 30))));
        }

    /// <summary>La hora no decide: el corte es por DÍA (el gasto se guarda como date).</summary>
    [Fact]
    public void DebeReatribuir_IgnoraLaHora()
        => Assert.True(DebeReatribuir(
            Gasto(fecha: Encaset.AddHours(23)),
            Lote(encaset: Encaset.AddHours(1))));
}
