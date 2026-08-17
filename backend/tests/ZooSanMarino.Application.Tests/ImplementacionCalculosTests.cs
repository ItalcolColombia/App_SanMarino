// tests/ZooSanMarino.Application.Tests/ImplementacionCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

public class ImplementacionCalculosTests
{
    // ── CalcularResumen ──────────────────────────────────────────────────────

    [Fact]
    public void Resumen_SinTareas_TodoCero()
    {
        var r = ImplementacionCalculos.CalcularResumen(0, 0, 0);
        Assert.Equal(0, r.TotalTareas);
        Assert.Equal(0m, r.PorcentajeAvance);
        Assert.Equal(0m, r.PorcentajeConfirmado);
    }

    [Fact]
    public void Resumen_ConfirmadasCuentanComoAvance()
    {
        // 10 tareas: 3 completadas (check del gestor) + 2 confirmadas → avance 5/10.
        var r = ImplementacionCalculos.CalcularResumen(10, 3, 2);
        Assert.Equal(50m, r.PorcentajeAvance);
        Assert.Equal(20m, r.PorcentajeConfirmado);
    }

    [Theory]
    [InlineData(3, 1, 0, 33.3)]  // 1/3 = 33.33… → 33.3
    [InlineData(3, 2, 0, 66.7)]  // 2/3 = 66.66… → 66.7
    [InlineData(8, 0, 1, 12.5)]  // exacto, sin redondeo
    public void Resumen_RedondeaAUnDecimal(int total, int completadas, int confirmadas, decimal esperado)
    {
        var r = ImplementacionCalculos.CalcularResumen(total, completadas, confirmadas);
        Assert.Equal(esperado, r.PorcentajeAvance);
    }

    [Fact]
    public void Resumen_TodasConfirmadas_Cien()
    {
        var r = ImplementacionCalculos.CalcularResumen(4, 0, 4);
        Assert.Equal(100m, r.PorcentajeAvance);
        Assert.Equal(100m, r.PorcentajeConfirmado);
    }

    // ── DeterminarEstadoPlan ─────────────────────────────────────────────────

    [Fact]
    public void EstadoPlan_CanceladoSeRespeta()
        => Assert.Equal("cancelado", ImplementacionCalculos.DeterminarEstadoPlan("cancelado", 10, 10, 10));

    [Fact]
    public void EstadoPlan_SinTareas_Borrador()
        => Assert.Equal("borrador", ImplementacionCalculos.DeterminarEstadoPlan("en_progreso", 0, 0, 0));

    [Fact]
    public void EstadoPlan_TodasConfirmadas_Completado()
        => Assert.Equal("completado", ImplementacionCalculos.DeterminarEstadoPlan("en_progreso", 5, 5, 5));

    [Fact]
    public void EstadoPlan_AvanceParcial_EnProgreso()
        => Assert.Equal("en_progreso", ImplementacionCalculos.DeterminarEstadoPlan("borrador", 5, 1, 3));

    [Fact]
    public void EstadoPlan_ConTareasSinNingunCheck_Borrador()
        => Assert.Equal("borrador", ImplementacionCalculos.DeterminarEstadoPlan("borrador", 5, 0, 0));

    // ── EsTareaVencida ───────────────────────────────────────────────────────

    private static readonly DateTime Hoy = new(2026, 7, 20);

    [Fact]
    public void Vencida_FechaPasadaYPendiente_True()
        => Assert.True(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 7, 19), Hoy, "pendiente"));

    [Fact]
    public void Vencida_SinFechaProgramada_False()
        => Assert.False(ImplementacionCalculos.EsTareaVencida(null, Hoy, "pendiente"));

    [Fact]
    public void Vencida_FechaHoy_False()
        => Assert.False(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 7, 20), Hoy, "pendiente"));

    [Theory]
    [InlineData("completada")]
    [InlineData("confirmada")]
    public void Vencida_YaConCheck_False(string estado)
        => Assert.False(ImplementacionCalculos.EsTareaVencida(new DateTime(2026, 1, 1), Hoy, estado));

    // ── PuedeConfirmar ───────────────────────────────────────────────────────

    private static readonly Guid UsuarioA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UsuarioB = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public void PuedeConfirmar_AsignadoYCompletada_True()
        => Assert.True(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, UsuarioA));

    [Fact]
    public void PuedeConfirmar_OtroUsuario_False()
        => Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, UsuarioB));

    [Fact]
    public void PuedeConfirmar_TareaPendiente_False()
        => Assert.False(ImplementacionCalculos.PuedeConfirmar("pendiente", UsuarioA, UsuarioA));

    [Fact]
    public void PuedeConfirmar_NullsFailClosed()
    {
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", null, UsuarioA));
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", UsuarioA, null));
        Assert.False(ImplementacionCalculos.PuedeConfirmar("completada", null, null));
    }

    // ── PlantillaPorDefecto ──────────────────────────────────────────────────

    [Fact]
    public void Plantilla_NoVaciaYOrdenGlobalCreciente()
    {
        var plantilla = ImplementacionCalculos.PlantillaPorDefecto();
        Assert.NotEmpty(plantilla);
        var ordenes = plantilla.Select(t => t.Orden).ToList();
        Assert.Equal(ordenes.OrderBy(o => o).ToList(), ordenes);
        Assert.Equal(ordenes.Count, ordenes.Distinct().Count());
    }

    [Fact]
    public void Plantilla_IncluyeCategoriasDeEntrega()
    {
        var categorias = ImplementacionCalculos.PlantillaPorDefecto().Select(t => t.Categoria).Distinct().ToList();
        Assert.Contains("Parametrizaciones", categorias);
        Assert.Contains("Capacitación", categorias);
        Assert.Contains("Carga de datos", categorias);
        Assert.Contains("Puesta en marcha", categorias);
    }

    // ── NormalizarTipoPlan ───────────────────────────────────────────────────

    [Theory]
    [InlineData(null, "implementacion")]           // default histórico
    [InlineData("", "implementacion")]
    [InlineData("   ", "implementacion")]
    [InlineData("implementacion", "implementacion")]
    [InlineData("capacitacion", "capacitacion")]
    [InlineData("mixto", "mixto")]
    [InlineData("  Capacitacion  ", "capacitacion")] // trim + case-insensitive
    public void TipoPlan_NormalizaValidos(string? entrada, string esperado)
        => Assert.Equal(esperado, ImplementacionCalculos.NormalizarTipoPlan(entrada));

    [Theory]
    [InlineData("entrenamiento")]
    [InlineData("otro")]
    public void TipoPlan_InvalidoLanza(string entrada)
        => Assert.Throws<InvalidOperationException>(() => ImplementacionCalculos.NormalizarTipoPlan(entrada));

    // ── CalcularResumenFirmas ────────────────────────────────────────────────

    [Fact]
    public void ResumenFirmas_SinParticipantes_TodoCero()
    {
        var r = ImplementacionCalculos.CalcularResumenFirmas(0, 0, 0);
        Assert.Equal(0, r.Total);
        Assert.Equal(0, r.Pendientes);
        Assert.Equal(0m, r.PorcentajeFirmado);
    }

    [Fact]
    public void ResumenFirmas_Mixto_CuentaPendientesYPorcentaje()
    {
        // 5 participantes: 2 firmaron, 1 novedad → 2 pendientes, 40 % firmado.
        var r = ImplementacionCalculos.CalcularResumenFirmas(5, 2, 1);
        Assert.Equal(2, r.Pendientes);
        Assert.Equal(1, r.Rechazadas);
        Assert.Equal(40m, r.PorcentajeFirmado);
    }

    [Theory]
    [InlineData(3, 1, 0, 33.3)]  // 33.33… → 33.3 (1 decimal AwayFromZero, igual que avance)
    [InlineData(3, 2, 1, 66.7)]  // 66.66… → 66.7
    [InlineData(8, 1, 0, 12.5)]  // exacto
    public void ResumenFirmas_RedondeaAUnDecimal(int total, int firmadas, int rechazadas, decimal esperado)
        => Assert.Equal(esperado, ImplementacionCalculos.CalcularResumenFirmas(total, firmadas, rechazadas).PorcentajeFirmado);

    // ── PuedeFirmar / PuedeRechazar ──────────────────────────────────────────

    [Fact]
    public void Firma_PendienteSePuedeFirmarYRechazar()
    {
        Assert.True(ImplementacionCalculos.PuedeFirmar("pendiente"));
        Assert.True(ImplementacionCalculos.PuedeRechazar("pendiente"));
    }

    [Fact]
    public void Firma_RechazadaPuedeRetractarseFirmandoPeroNoReRechazar()
    {
        Assert.True(ImplementacionCalculos.PuedeFirmar("rechazada"));
        Assert.False(ImplementacionCalculos.PuedeRechazar("rechazada"));
    }

    [Fact]
    public void Firma_FirmadaNoAdmiteNada()
    {
        Assert.False(ImplementacionCalculos.PuedeFirmar("firmada"));
        Assert.False(ImplementacionCalculos.PuedeRechazar("firmada"));
    }

    // ── ValidarFirmaTexto ────────────────────────────────────────────────────

    [Fact]
    public void FirmaTexto_ValidaYTrimea()
        => Assert.Equal("Moisés Murillo", ImplementacionCalculos.ValidarFirmaTexto("  Moisés Murillo  "));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ab ")]  // < 3 tras trim
    public void FirmaTexto_CortaOVaciaLanza(string? firma)
        => Assert.Throws<InvalidOperationException>(() => ImplementacionCalculos.ValidarFirmaTexto(firma));

    [Fact]
    public void FirmaTexto_MayorA300Lanza()
        => Assert.Throws<InvalidOperationException>(
            () => ImplementacionCalculos.ValidarFirmaTexto(new string('a', 301)));

    // ── TareaHabilitadaParaFirmar (gate: se firma lo ya realizado) ───────────

    [Fact]
    public void Gate_TareaPendienteNoSeFirma()
        => Assert.False(ImplementacionCalculos.TareaHabilitadaParaFirmar("pendiente"));

    [Theory]
    [InlineData("completada")]
    [InlineData("confirmada")]
    public void Gate_DesdeCompletadaSeHabilita(string estado)
        => Assert.True(ImplementacionCalculos.TareaHabilitadaParaFirmar(estado));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("cualquier_cosa")]
    public void Gate_EstadoDesconocidoEsFailClosed(string? estado)
        => Assert.False(ImplementacionCalculos.TareaHabilitadaParaFirmar(estado));

    // ── ValidarFirmaImagen ───────────────────────────────────────────────────

    /// <summary>Data URL PNG válida con payload suficientemente largo (trazo real).</summary>
    private static string ImagenValida(int payloadChars = 400)
        => "data:image/png;base64," + Convert.ToBase64String(new byte[payloadChars]);

    [Fact]
    public void FirmaImagen_VaciaDevuelveNull()
    {
        Assert.Null(ImplementacionCalculos.ValidarFirmaImagen(null));
        Assert.Null(ImplementacionCalculos.ValidarFirmaImagen("   "));
    }

    [Fact]
    public void FirmaImagen_ValidaSeNormaliza()
    {
        var img = ImagenValida();
        Assert.Equal(img, ImplementacionCalculos.ValidarFirmaImagen("  " + img + "  "));
    }

    [Fact]
    public void FirmaImagen_OtroFormatoLanza()
        => Assert.Throws<InvalidOperationException>(
            () => ImplementacionCalculos.ValidarFirmaImagen("data:image/jpeg;base64,AAAA"));

    [Fact]
    public void FirmaImagen_CanvasEnBlancoLanza()
        => Assert.Throws<InvalidOperationException>(
            () => ImplementacionCalculos.ValidarFirmaImagen("data:image/png;base64,AAAA"));

    [Fact]
    public void FirmaImagen_Base64CorruptoLanza()
        => Assert.Throws<InvalidOperationException>(
            () => ImplementacionCalculos.ValidarFirmaImagen("data:image/png;base64," + new string('!', 300)));

    [Fact]
    public void FirmaImagen_DemasiadoPesadaLanza()
        => Assert.Throws<InvalidOperationException>(
            () => ImplementacionCalculos.ValidarFirmaImagen(
                "data:image/png;base64," + new string('A', ImplementacionCalculos.FirmaImagenMaxChars)));

    // ── CalcularContenidoHash (evidencia de QUÉ se firmó) ────────────────────

    [Fact]
    public void Hash_MismoContenidoMismoHash()
    {
        var f = new DateTime(2026, 8, 12, 15, 30, 0, DateTimeKind.Utc);
        var a = ImplementacionCalculos.CalcularContenidoHash("Plan A", "Capacitación", "Módulo inventario", "Detalle", f);
        var b = ImplementacionCalculos.CalcularContenidoHash("Plan A", "Capacitación", "Módulo inventario", "Detalle", f);
        Assert.Equal(a, b);
        Assert.Equal(64, a.Length);           // SHA-256 en hex
        Assert.Equal(a, a.ToLowerInvariant()); // siempre minúsculas
    }

    [Fact]
    public void Hash_CambiarElTituloCambiaElHash()
    {
        var f = new DateTime(2026, 8, 12, 15, 30, 0, DateTimeKind.Utc);
        var antes   = ImplementacionCalculos.CalcularContenidoHash("Plan A", "Capacitación", "Módulo inventario", "Detalle", f);
        var despues = ImplementacionCalculos.CalcularContenidoHash("Plan A", "Capacitación", "Módulo INVENTARIO v2", "Detalle", f);
        Assert.NotEqual(antes, despues);
    }

    [Fact]
    public void Hash_TrimNoCambiaElResultado()
    {
        var f = new DateTime(2026, 8, 12, 15, 30, 0, DateTimeKind.Utc);
        var a = ImplementacionCalculos.CalcularContenidoHash("Plan A", "Capacitación", "Módulo", "Detalle", f);
        var b = ImplementacionCalculos.CalcularContenidoHash("  Plan A ", " Capacitación", " Módulo  ", "Detalle ", f);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_DescripcionNulaYVaciaSonEquivalentes()
    {
        var a = ImplementacionCalculos.CalcularContenidoHash("P", "C", "T", null, null);
        var b = ImplementacionCalculos.CalcularContenidoHash("P", "C", "T", "   ", null);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_LaFechaSeNormalizaAUtc()
    {
        var utc   = new DateTime(2026, 8, 12, 15, 30, 0, DateTimeKind.Utc);
        var local = utc.ToLocalTime();
        Assert.Equal(
            ImplementacionCalculos.CalcularContenidoHash("P", "C", "T", "D", utc),
            ImplementacionCalculos.CalcularContenidoHash("P", "C", "T", "D", local));
    }

    // ─── Vínculo con ItalJira (I1.3) ──────────────────────────────────────────

    [Fact]
    public void TareaEnListo_CompletaElPuntoPendiente()
    {
        Assert.Equal(
            ImplementacionCalculos.TareaCompletada,
            ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(true, ImplementacionCalculos.TareaPendiente));
    }

    [Fact]
    public void TareaQueSaleDeListo_DevuelveElPuntoAPendiente()
    {
        // Si se reabrio en el tablero es porque no estaba terminado; dejarlo completado habilitaria
        // firmar algo que se esta rehaciendo.
        Assert.Equal(
            ImplementacionCalculos.TareaPendiente,
            ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(false, ImplementacionCalculos.TareaCompletada));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnPuntoCONFIRMADO_NoLoToca_NingunMovimientoDelTablero(bool terminal)
    {
        // Confirmar es un acto de una persona y detras vienen las firmas con su hash de contenido.
        // Un drag & drop en el tablero no puede deshacer eso.
        Assert.Null(ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(
            terminal, ImplementacionCalculos.TareaConfirmada));
    }

    [Theory]
    [InlineData(true, ImplementacionCalculos.TareaCompletada)]
    [InlineData(false, ImplementacionCalculos.TareaPendiente)]
    public void SiElPuntoYaEstaComoCorresponde_NoDevuelveCambio(bool terminal, string estadoActual)
    {
        // Evita escrituras (y sellos de fecha/autor) por movimientos entre columnas no terminales.
        Assert.Null(ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(terminal, estadoActual));
    }

    [Fact]
    public void UnEstadoDesconocidoOVacio_SeTrataComoPendiente()
    {
        Assert.Equal(
            ImplementacionCalculos.TareaCompletada,
            ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(true, null));
        Assert.Equal(
            ImplementacionCalculos.TareaPendiente,
            ImplementacionCalculos.EstadoPuntoSegunTareaItalJira(false, "  "));
    }
}
