using System.Globalization;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del APAGADO de la doble validación: se validan los pendientes de la empresa y solo entonces
/// se apaga; solo en la transición encendido → apagado; y si algo no se puede validar no se apaga.
///
/// <para>
/// Origen: Santa Reyes, 18-sep-2026. El flag se apagó con 6 registros pendientes (7.011 kg y 259 aves
/// separados y nunca aplicados): sin el flag no hay botón de validar, así que quedaron colgados; borrar uno
/// devolvió stock que nunca había salido y dejó una reserva sin dueño.
/// </para>
/// </summary>
public class ApagadoDobleValidacionCalculosTests
{
    // ─── Solo la transición encendido → apagado ───────────────────────────────

    [Theory]
    [InlineData(true, false, true)]     // encendido → apagado: valida antes
    [InlineData(true, true, false)]     // sigue encendido
    [InlineData(true, null, false)]     // el PUT no lo manda: se conserva
    [InlineData(false, false, false)]   // ya estaba apagado: nada que validar
    [InlineData(false, true, false)]    // se ENCIENDE: no valida nada
    [InlineData(false, null, false)]
    public void EsApagado_SoloEnLaTransicionEncendidoApagado(bool actual, bool? solicitado, bool esperado) =>
        Assert.Equal(esperado, ApagadoDobleValidacionCalculos.EsApagado(actual, solicitado));

    // ─── La validación se aplica bajo la empresa ACTIVA ───────────────────────

    [Theory]
    [InlineData(6, 6, true)]
    [InlineData(6, 5, false)]    // apagar Santa Reyes con Panamá activa: inventario ajeno
    [InlineData(5, 6, false)]
    [InlineData(0, 0, false)]    // sin empresa resuelta, fail-closed
    [InlineData(-1, -1, false)]
    public void PuedeValidarDesdeEmpresaActiva_ExigeQueSeanLaMisma(int objetivo, int activa, bool esperado) =>
        Assert.Equal(esperado, ApagadoDobleValidacionCalculos.PuedeValidarDesdeEmpresaActiva(objetivo, activa));

    [Fact]
    public void MensajeEmpresaActivaDistinta_DiceQueHacerYQueNoSeCambioNada()
    {
        var uno = ApagadoDobleValidacionCalculos.MensajeEmpresaActivaDistinta(1);
        var varios = ApagadoDobleValidacionCalculos.MensajeEmpresaActivaDistinta(6);

        Assert.Contains("1 registro pendiente de la empresa", uno);
        Assert.Contains("6 registros pendientes de la empresa", varios);
        Assert.Contains("Cambie a esa empresa", uno);
        Assert.Contains("No se cambió nada.", uno);
    }

    // ─── Etiquetas ────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(ModuloSeguimiento.Levante, "Levante")]
    [InlineData(ModuloSeguimiento.Produccion, "Producción")]
    [InlineData(ModuloSeguimiento.Engorde, "Engorde")]
    [InlineData(ModuloSeguimiento.EngordeEcuador, "Engorde")]   // los dos módulos de engorde son el mismo registro
    [InlineData(ModuloSeguimiento.Reproductora, "Reproductora")]
    [InlineData("OTRO", "OTRO")]
    public void EtiquetaModulo_EsLegible(string modulo, string esperado) =>
        Assert.Equal(esperado, ApagadoDobleValidacionCalculos.EtiquetaModulo(modulo));

    // ─── Mensaje cuando no se pudo validar todo ───────────────────────────────

    private static ResultadoValidacionEmpresaDto Resultado(
        int pendientes, int validados, IReadOnlyList<FalloValidacionEmpresaDto> fallos, int noIntentados = 0) =>
        new(LotesConPendientes: fallos.Count, Pendientes: pendientes, Validados: validados, YaValidados: 0,
            Fallidos: fallos.Count, NoIntentados: noIntentados, KgAplicados: 0m, AvesDescontadas: 0, Fallos: fallos);

    [Fact]
    public void MensajeNoSePudoApagar_NombraModuloLoteRegistroFechaYMotivo()
    {
        var fallo = new FalloValidacionEmpresaDto(
            ModuloSeguimiento.Produccion, LoteId: 26, SeguimientoId: 679, new DateOnly(2026, 9, 13),
            "Stock insuficiente para '2237 - PREPICO' (granja 109): disponible 100 kg, requerido 1245 kg.");

        var mensaje = ApagadoDobleValidacionCalculos.MensajeNoSePudoApagar(Resultado(6, 5, [fallo]));

        Assert.Contains("1 de 6 registros pendientes no se pudo validar", mensaje);
        Assert.Contains("el flag sigue encendido", mensaje);
        Assert.Contains("• Producción, lote 26: #679 (13/09/2026): Stock insuficiente", mensaje);
        Assert.Contains("lo que ya se validó se conserva", mensaje);
    }

    [Fact]
    public void MensajeNoSePudoApagar_CuentaTambienLosNoIntentados()
    {
        var fallo = new FalloValidacionEmpresaDto(
            ModuloSeguimiento.Engorde, 7, 100, new DateOnly(2026, 8, 1), "sin stock");

        // 1 falló y 3 quedaron sin intentar detrás de él: 4 sin validar de 10.
        var mensaje = ApagadoDobleValidacionCalculos.MensajeNoSePudoApagar(Resultado(10, 6, [fallo], noIntentados: 3));

        Assert.Contains("4 de 10 registros pendientes no se pudo validar", mensaje);
    }

    [Fact]
    public void MensajeNoSePudoApagar_UnSoloPendienteVaEnSingular()
    {
        var fallo = new FalloValidacionEmpresaDto(
            ModuloSeguimiento.Levante, 1, 5, new DateOnly(2026, 1, 2), "x");

        var mensaje = ApagadoDobleValidacionCalculos.MensajeNoSePudoApagar(Resultado(1, 0, [fallo]));

        Assert.Contains("1 de 1 registro pendiente no se pudo validar", mensaje);
    }

    [Fact]
    public void MensajeNoSePudoApagar_TopeDeLineasYResumenDelResto()
    {
        var fallos = Enumerable.Range(1, ApagadoDobleValidacionCalculos.MaxLineasDeFallos + 3)
            .Select(i => new FalloValidacionEmpresaDto(
                ModuloSeguimiento.Produccion, LoteId: i, SeguimientoId: 1000 + i, new DateOnly(2026, 9, 1), $"motivo {i}"))
            .ToList();

        var mensaje = ApagadoDobleValidacionCalculos.MensajeNoSePudoApagar(Resultado(50, 39, fallos));

        Assert.Equal(ApagadoDobleValidacionCalculos.MaxLineasDeFallos, mensaje.Split('\n').Count(l => l.StartsWith("• ")));
        Assert.Contains("…y 3 lotes más.", mensaje);
        Assert.Contains("motivo 8", mensaje);
        Assert.DoesNotContain("motivo 9", mensaje);
    }

    /// <summary>La fecha sale dd/MM/yyyy con cualquier cultura del servidor (el servidor no fija cultura).</summary>
    [Fact]
    public void MensajeNoSePudoApagar_LaFechaNoDependeDeLaCulturaDelServidor()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var fallo = new FalloValidacionEmpresaDto(
                ModuloSeguimiento.Produccion, 26, 679, new DateOnly(2026, 9, 13), "x");

            var mensaje = ApagadoDobleValidacionCalculos.MensajeNoSePudoApagar(Resultado(1, 0, [fallo]));

            Assert.Contains("(13/09/2026)", mensaje);
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }

    // ─── Resultado.Completo: solo entonces se puede apagar ────────────────────

    [Fact]
    public void Completo_SoloSiNoHayFalloNiNoIntentado()
    {
        var fallo = new FalloValidacionEmpresaDto(ModuloSeguimiento.Levante, 1, 1, new DateOnly(2026, 1, 1), "x");

        Assert.True(Resultado(5, 5, []).Completo);
        Assert.False(Resultado(5, 4, [fallo]).Completo);
        Assert.False(Resultado(5, 4, [], noIntentados: 1).Completo);
    }
}
