// tests/ZooSanMarino.Application.Tests/TicketAlcancePanelCalculosTests.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato del alcance de las vistas agregadas de ItalJira. Lo que se fija acá:
///  - sin <c>tickets.indicadores</c> el comportamiento es EXACTAMENTE el previo (regresión);
///  - <c>tickets.indicadores</c> abre el panel y su reporte, y NADA más — ni el tablero ni el
///    roadmap, tampoco llamando la API a mano;
///  - <c>tickets.admin</c> conserva el alcance global en todas las vistas;
///  - las keys se comparan sin distinguir mayúsculas y una lista nula no revienta.
/// </summary>
public class TicketAlcancePanelCalculosTests
{
    private const string ADMIN       = TicketAlcancePanelCalculos.PermisoAdmin;
    private const string INDICADORES = TicketAlcancePanelCalculos.PermisoIndicadores;
    private const string GESTIONAR   = "tickets.gestionar";

    /// <summary>Legibilidad: en el panel y el reporte va true; en tablero y roadmap, false.</summary>
    private const bool SOLO_LECTURA = true;
    private const bool TABLERO      = false;

    // ─────────────────────── Sin permisos: fail-closed ───────────────────────

    [Theory]
    [InlineData(SOLO_LECTURA)]
    [InlineData(TABLERO)]
    public void Sin_permisos_no_hay_alcance_global(bool vistaSoloLectura)
    {
        Assert.False(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            Array.Empty<string>(), vistaSoloLectura));
    }

    [Theory]
    [InlineData(SOLO_LECTURA)]
    [InlineData(TABLERO)]
    public void Lista_nula_no_revienta_y_no_concede_nada(bool vistaSoloLectura)
    {
        Assert.False(TicketAlcancePanelCalculos.TieneAlcanceGlobal(null, vistaSoloLectura));
    }

    // ─────────────────────── Regresión: el resolutor no cambia ───────────────────────

    /// <summary>
    /// `tickets.gestionar` NUNCA dio alcance global: el resolutor ve solo los casos que tiene
    /// asignados, también en el panel. Este test es el que impide que el permiso nuevo se cuele
    /// como "cualquier gestor ve todo".
    /// </summary>
    [Theory]
    [InlineData(SOLO_LECTURA)]
    [InlineData(TABLERO)]
    public void Gestionar_sigue_sin_ver_todo(bool vistaSoloLectura)
    {
        Assert.False(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { GESTIONAR }, vistaSoloLectura));
    }

    // ─────────────────────── El admin conserva todo ───────────────────────

    [Theory]
    [InlineData(SOLO_LECTURA)]
    [InlineData(TABLERO)]
    public void Admin_ve_todo_en_cualquier_vista(bool vistaSoloLectura)
    {
        Assert.True(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { ADMIN }, vistaSoloLectura));
    }

    [Theory]
    [InlineData("TICKETS.ADMIN")]
    [InlineData("Tickets.Admin")]
    public void La_key_del_admin_se_compara_sin_distinguir_mayusculas(string key)
    {
        Assert.True(TicketAlcancePanelCalculos.TieneAlcanceGlobal(new[] { key }, TABLERO));
    }

    // ─────────────────────── El permiso nuevo: solo el panel ───────────────────────

    [Fact]
    public void Indicadores_abre_el_panel_y_su_reporte()
    {
        Assert.True(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { INDICADORES }, SOLO_LECTURA));
    }

    /// <summary>
    /// El punto del diseño: gerencia mira los números, no gestiona. `GET /api/tickets/tablero`
    /// con este permiso tiene que seguir cayendo en "solo mis casos".
    /// </summary>
    [Fact]
    public void Indicadores_NO_abre_el_tablero_ni_el_roadmap()
    {
        Assert.False(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { INDICADORES }, TABLERO));
    }

    [Theory]
    [InlineData("TICKETS.INDICADORES")]
    [InlineData("Tickets.Indicadores")]
    public void La_key_de_indicadores_tambien_es_case_insensitive(string key)
    {
        Assert.True(TicketAlcancePanelCalculos.TieneAlcanceGlobal(new[] { key }, SOLO_LECTURA));
    }

    // ─────────────────────── Combinaciones ───────────────────────

    [Fact]
    public void Un_permiso_ajeno_no_concede_nada()
    {
        Assert.False(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { "tickets.crear", GESTIONAR, "lote.ver" }, SOLO_LECTURA));
    }

    [Fact]
    public void El_admin_manda_aunque_venga_acompanado()
    {
        Assert.True(TicketAlcancePanelCalculos.TieneAlcanceGlobal(
            new[] { GESTIONAR, ADMIN, INDICADORES }, TABLERO));
    }
}
