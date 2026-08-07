using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.ReporteDiarioCostosPostura;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Reporte Diario Área de Costos de POSTURA — cálculo puro.
///
/// Los testigos de huevo son filas REALES del lote S-369B (lote_id 145, granja Pruebas Moises),
/// cargado por carga masiva desde informes verídicos de la granja MANGOS. La clasificación
/// fértil/comercial/inservible es la decisión D1 (07-ago-2026) y estos tests son su contrato:
/// los tres grupos tienen que sumar EXACTO el huevo total.
/// </summary>
public class ReporteDiarioCostosPosturaCalculosTests
{
    // ── Testigos reales de S-369B ────────────────────────────────────────────
    // 2026-05-15: tot 7.799 · inc 7.506 (limpio 7.351 + tratado 155)
    private static readonly HuevoCrudo Dia15May = new(
        Tot: 7799, Inc: 7506, Limpio: 7351, Tratado: 155,
        Sucio: 26, Deforme: 63, Blanco: 0, DobleYema: 38, Piso: 48, Pequeno: 6,
        Roto: 68, Desecho: 44, Otro: 0);

    // 2026-06-15: tot 7.157 · inc 6.884 (limpio 6.669 + tratado 215)
    private static readonly HuevoCrudo Dia15Jun = new(
        Tot: 7157, Inc: 6884, Limpio: 6669, Tratado: 215,
        Sucio: 16, Deforme: 67, Blanco: 0, DobleYema: 33, Piso: 45, Pequeno: 12,
        Roto: 67, Desecho: 33, Otro: 0);

    // ── T1 · Clasificación del día real 15-may-2026 ──────────────────────────
    [Fact]
    public void ClasificarHuevo_DiaRealMayo_DevuelveLosTresGruposYCuadraElTotal()
    {
        var h = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(Dia15May);

        Assert.Equal(7506, h.Fertil);
        Assert.Equal(181, h.Comercial);      // 26 + 63 + 0 + 38 + 48 + 6
        Assert.Equal(112, h.Inservible);     // 68 + 44 + 0
        Assert.Equal(7799, h.Total);
        Assert.Equal(h.Total, h.Fertil + h.Comercial + h.Inservible);
        Assert.True(h.ParticionCuadra);
    }

    // ── T2 · Clasificación del día real 15-jun-2026 ──────────────────────────
    [Fact]
    public void ClasificarHuevo_DiaRealJunio_DevuelveLosTresGruposYCuadraElTotal()
    {
        var h = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(Dia15Jun);

        Assert.Equal(6884, h.Fertil);
        Assert.Equal(173, h.Comercial);      // 16 + 67 + 0 + 33 + 45 + 12
        Assert.Equal(100, h.Inservible);     // 67 + 33 + 0
        Assert.Equal(7157, h.Total);
        Assert.True(h.ParticionCuadra);
    }

    // ── T3 · Día sin huevo (levante o inicio de producción) ──────────────────
    [Fact]
    public void ClasificarHuevo_TodoEnCero_NoRompeYDevuelveCeros()
    {
        var h = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(default);

        Assert.Equal(0, h.Fertil);
        Assert.Equal(0, h.Comercial);
        Assert.Equal(0, h.Inservible);
        Assert.Equal(0, h.Total);
        Assert.Equal(0, h.Venta);
        Assert.Equal(0, h.TrasladoPlanta);
        Assert.True(h.ParticionCuadra);
    }

    // ── T4 · El invariante de BD que sostiene D1: inc == limpio + tratado ────
    [Theory]
    [InlineData(7506, 7351, 155)]
    [InlineData(6884, 6669, 215)]
    public void HuevoIncubable_EsLimpioMasTratado_EnLosDatosReales(int inc, int limpio, int tratado)
    {
        // Si este invariante se rompiera, «fértil = inc» dejaría de ser equivalente a
        // «limpio + tratado» y la partición D1 ya no cerraría contra el total.
        Assert.Equal(inc, limpio + tratado);
    }

    // ── T5 · Partición exacta sobre el acumulado del ciclo completo ──────────
    [Fact]
    public void ClasificarHuevo_AcumuladoDelCiclo_ParticionaExactoElTotal()
    {
        // Acumulado real de los 161 días de producción de S-369B.
        var acumulado = new HuevoCrudo(
            Tot: 1115079, Inc: 1021041, Limpio: 992662, Tratado: 28379,
            Sucio: 5313, Deforme: 8356, Blanco: 0, DobleYema: 11358, Piso: 14320, Pequeno: 33843,
            Roto: 11549, Desecho: 9299, Otro: 0);

        var h = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(acumulado);

        Assert.Equal(1021041, h.Fertil);
        Assert.Equal(73190, h.Comercial);
        Assert.Equal(20848, h.Inservible);
        Assert.Equal(1115079, h.Total);
        Assert.Equal(h.Total, h.Fertil + h.Comercial + h.Inservible);
    }

    // ── T5b · Fila inconsistente: NO se cuadra a la fuerza ───────────────────
    [Fact]
    public void ClasificarHuevo_FilaInconsistente_ConservaElTotalRegistradoYMarcaElDescuadre()
    {
        var roto = new HuevoCrudo(Tot: 100, Inc: 10, Limpio: 10, Tratado: 0,
            Sucio: 0, Deforme: 0, Blanco: 0, DobleYema: 0, Piso: 0, Pequeno: 0,
            Roto: 0, Desecho: 0, Otro: 0);

        var h = ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(roto);

        Assert.Equal(100, h.Total);          // el total registrado se respeta
        Assert.Equal(10, h.Fertil);
        Assert.False(h.ParticionCuadra);     // y el descuadre queda visible
    }

    // ── T6 · Totales de la pestaña Aves ──────────────────────────────────────
    [Fact]
    public void TotalesAves_SumaLasCuatroCategoriasPorSexo()
    {
        var filas = new[]
        {
            Fila(mortH: 10, mortM: 2, selH: 3, selM: 1, errH: 5, errM: 0, venH: 0, venM: 20),
            Fila(mortH: 7,  mortM: 1, selH: 2, selM: 4, errH: 1, errM: 3, venH: 9, venM: 2)
        };

        var t = ReporteDiarioCostosPosturaCalculos.TotalesAves(filas);

        Assert.Equal(17, t.MortalidadH);
        Assert.Equal(3, t.MortalidadM);
        Assert.Equal(5, t.SeleccionH);
        Assert.Equal(5, t.SeleccionM);
        Assert.Equal(6, t.ErrorSexajeH);
        Assert.Equal(3, t.ErrorSexajeM);
        Assert.Equal(9, t.VentaAvesH);
        Assert.Equal(22, t.VentaAvesM);
        Assert.Equal(37, t.TotalH);          // 17 + 5 + 6 + 9
        Assert.Equal(33, t.TotalM);          // 3 + 5 + 3 + 22
        Assert.Equal(70, t.Total);
    }

    // ── T7 · Un día con DOS alimentos de hembras produce DOS grupos (D4) ─────
    [Fact]
    public void TotalesAlimento_DosAlimentosDelMismoSexo_NoSeFusionan()
    {
        // Caso real de S-369B el 23-feb-2026: PREPICO 567,966 kg + PREPOSTURA 546,134 kg = 1.114,1
        var filas = new[]
        {
            Fila(alimentos: new[]
            {
                Alimento("H", "PREPICO REPRODUCTORA PESADA MED H", 567.966),
                Alimento("H", "PREPOSTURA REPRODUCTORA PESADA", 546.134),
                Alimento("M", "POLLA LEVANTE REPRODUCTORA PESADA", 124)
            }),
            Fila(alimentos: new[]
            {
                Alimento("H", "PREPICO REPRODUCTORA PESADA MED H", 100),
                Alimento("M", "POLLA LEVANTE REPRODUCTORA PESADA", 26)
            })
        };

        var t = ReporteDiarioCostosPosturaCalculos.TotalesAlimento(filas);

        Assert.Equal(3, t.Count);
        Assert.Equal(667.966, t.Single(x => x.Sexo == "H" && x.Nombre.StartsWith("PREPICO")).CantidadKg, 3);
        Assert.Equal(546.134, t.Single(x => x.Sexo == "H" && x.Nombre.StartsWith("PREPOSTURA")).CantidadKg, 3);
        Assert.Equal(150, t.Single(x => x.Sexo == "M").CantidadKg, 3);
    }

    [Fact]
    public void TotalesAlimento_MismoNombreDistintaGrafia_SeAgrupaUnaSolaVez()
    {
        var filas = new[]
        {
            Fila(alimentos: new[] { Alimento("H", "Prepico Reproductora", 10) }),
            Fila(alimentos: new[] { Alimento("H", "PREPICO REPRODUCTORA", 5) })
        };

        var t = ReporteDiarioCostosPosturaCalculos.TotalesAlimento(filas);

        Assert.Single(t);
        Assert.Equal(15, t[0].CantidadKg, 3);
    }

    // ── T8 · Normalización de la fase ────────────────────────────────────────
    [Theory]
    [InlineData("levante", "Levante")]
    [InlineData("Levante", "Levante")]
    [InlineData("LEVANTE", "Levante")]
    [InlineData("produccion", "Produccion")]
    [InlineData("Producción", "Produccion")]
    [InlineData("PRODUCCION", "Produccion")]
    public void NormalizarFase_ReconoceLasDosFases(string entrada, string esperado)
        => Assert.Equal(esperado, ReporteDiarioCostosPosturaCalculos.NormalizarFase(entrada));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ambas")]
    [InlineData("cualquier cosa")]
    public void NormalizarFase_SinFaseValida_DevuelveNullQueSignificaAmbas(string? entrada)
        => Assert.Null(ReporteDiarioCostosPosturaCalculos.NormalizarFase(entrada));

    // ── T9 · Etiqueta lote:galpón ────────────────────────────────────────────
    [Fact]
    public void EtiquetaLoteGalpon_ConAmbosDatos_UneConDosPuntos()
        => Assert.Equal("S-369B : G0443",
            ReporteDiarioCostosPosturaCalculos.EtiquetaLoteGalpon("S-369B", "G0443"));

    [Theory]
    [InlineData("S-369B", null, "S-369B : (sin galpón)")]
    [InlineData("S-369B", "  ", "S-369B : (sin galpón)")]
    [InlineData(null, "G0443", "(sin lote) : G0443")]
    public void EtiquetaLoteGalpon_ConDatosFaltantes_NoLanza(string? lote, string? galpon, string esperado)
        => Assert.Equal(esperado, ReporteDiarioCostosPosturaCalculos.EtiquetaLoteGalpon(lote, galpon));

    // ── T10 · Fases presentes en el resultado, en orden de ciclo ─────────────
    [Fact]
    public void FasesPresentes_ConAmbasFases_DevuelveLevanteAntesQueProduccion()
    {
        var filas = new[] { Fila(fase: "Produccion"), Fila(fase: "Levante") };

        var fases = ReporteDiarioCostosPosturaCalculos.FasesPresentes(filas);

        Assert.Equal(new[] { "Levante", "Produccion" }, fases);
    }

    [Fact]
    public void FasesPresentes_SinFilas_DevuelveVacio()
        => Assert.Empty(ReporteDiarioCostosPosturaCalculos.FasesPresentes(
            Array.Empty<ReporteDiarioCostosPosturaFilaDto>()));

    // ── T11 · Footer completo ────────────────────────────────────────────────
    [Fact]
    public void ConstruirTotales_ConsolidaAvesAlimentoYHuevo()
    {
        var filas = new[]
        {
            Fila(mortH: 10, mortM: 2, consumoH: 1113.2, consumoM: 125.5,
                 alimentos: new[] { Alimento("H", "PREPOSTURA", 1113.2), Alimento("M", "POLLA LEVANTE", 125.5) },
                 huevo: ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(Dia15May, venta: 0, trasladoPlanta: 5000)),
            Fila(mortH: 1, consumoH: 1113.2, consumoM: 125.5,
                 alimentos: new[] { Alimento("H", "PREPOSTURA", 1113.2), Alimento("M", "POLLA LEVANTE", 125.5) },
                 huevo: ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(Dia15Jun, venta: 2000, trasladoPlanta: 0))
        };

        var t = ReporteDiarioCostosPosturaCalculos.ConstruirTotales(filas);

        Assert.Equal(11, t.Aves.MortalidadH);
        Assert.Equal(2226.4, t.ConsumoKgH, 3);
        Assert.Equal(251, t.ConsumoKgM, 3);
        Assert.Equal(2477.4, t.ConsumoKgTotal, 3);
        Assert.Equal(2, t.Alimentos.Count);
        Assert.Equal(14390, t.Huevo.Fertil);      // 7.506 + 6.884
        Assert.Equal(354, t.Huevo.Comercial);     // 181 + 173
        Assert.Equal(212, t.Huevo.Inservible);    // 112 + 100
        Assert.Equal(14956, t.Huevo.Total);       // 7.799 + 7.157
        Assert.Equal(2000, t.Huevo.Venta);
        Assert.Equal(5000, t.Huevo.TrasladoPlanta);
        Assert.Equal(t.Huevo.Total, t.Huevo.Fertil + t.Huevo.Comercial + t.Huevo.Inservible);
    }

    [Fact]
    public void ConstruirTotales_SinFilas_DevuelveTodoEnCero()
    {
        var t = ReporteDiarioCostosPosturaCalculos.ConstruirTotales(
            Array.Empty<ReporteDiarioCostosPosturaFilaDto>());

        Assert.Equal(0, t.Aves.Total);
        Assert.Equal(0, t.ConsumoKgTotal);
        Assert.Empty(t.Alimentos);
        Assert.Equal(0, t.Huevo.Total);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────
    private static ReporteDiarioCostosPosturaAlimentoDto Alimento(string sexo, string nombre, double kg)
        => new(sexo, nombre, kg, "metadata");

    private static ReporteDiarioCostosPosturaFilaDto Fila(
        string fase = "Produccion",
        int mortH = 0, int mortM = 0,
        int selH = 0, int selM = 0,
        int errH = 0, int errM = 0,
        int venH = 0, int venM = 0,
        double consumoH = 0, double consumoM = 0,
        IReadOnlyList<ReporteDiarioCostosPosturaAlimentoDto>? alimentos = null,
        ReporteDiarioCostosPosturaHuevoDto? huevo = null)
        => new(
            Fecha: new DateTime(2026, 5, 15),
            Fase: fase,
            LoteId: 145,
            LoteNombre: "S-369B",
            GalponId: "G0443",
            GalponNombre: "G0443",
            LoteGalpon: "S-369B : G0443",
            NucleoId: "883195",
            GranjaId: 44,
            GranjaNombre: "Pruebas Moises",
            Regional: "",
            LotePosturaBaseId: 30,
            LoteBaseNombre: "S-369",
            EdadDias: 253,
            Semana: 37,
            MortalidadH: mortH, MortalidadM: mortM,
            SeleccionH: selH, SeleccionM: selM,
            ErrorSexajeH: errH, ErrorSexajeM: errM,
            VentaAvesH: venH, VentaAvesM: venM,
            ConsumoKgH: consumoH, ConsumoKgM: consumoM,
            Alimentos: alimentos ?? Array.Empty<ReporteDiarioCostosPosturaAlimentoDto>(),
            Huevo: huevo ?? ReporteDiarioCostosPosturaCalculos.ClasificarHuevo(default));
}
