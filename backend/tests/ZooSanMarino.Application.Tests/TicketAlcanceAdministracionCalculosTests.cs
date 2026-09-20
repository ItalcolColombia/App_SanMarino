using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// F4: <c>tickets.admin</c> deja de significar «todas las empresas». Lo tienen roles de UNA empresa
/// (<c>Santa Reyes Administrador</c>, <c>Admin Demo</c>, <c>Lider Demanda &amp; Delivery</c>), así que
/// ahora administra su empresa activa y solo el admin global ve todas.
/// </summary>
public class TicketAlcanceAdministracionCalculosTests
{
    private const string Admin = "tickets.admin";
    private const string Gestionar = "tickets.gestionar";
    private const string Indicadores = "tickets.indicadores";
    private static readonly string[] Nada = Array.Empty<string>();

    // ── Tablero / roadmap / panel ──

    [Fact]
    public void El_admin_global_con_el_permiso_ve_todas_las_empresas()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Todas,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(true, new[] { Admin }, vistaSoloLectura: false));

    [Fact]
    public void El_admin_de_una_empresa_ve_el_tablero_de_su_empresa()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.EmpresaActiva,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(false, new[] { Admin }, vistaSoloLectura: false));

    [Fact]
    public void Sin_permiso_el_tablero_muestra_solo_lo_asignado()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Ninguno,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(true, new[] { Gestionar }, vistaSoloLectura: false));

    [Fact]
    public void Indicadores_sigue_valiendo_solo_en_las_vistas_de_lectura()
    {
        Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Ninguno,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(true, new[] { Indicadores }, vistaSoloLectura: false));
        Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Todas,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(true, new[] { Indicadores }, vistaSoloLectura: true));
        Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.EmpresaActiva,
            TicketAlcanceAdministracionCalculos.AlcanceTablero(false, new[] { Indicadores }, vistaSoloLectura: true));
    }

    // ── Bandeja de administración (api/tickets/global) ──

    [Fact]
    public void La_bandeja_global_exige_tickets_admin()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Ninguno,
            TicketAlcanceAdministracionCalculos.AlcanceAdministracion(false, new[] { "tickets.crear", Gestionar }));

    [Fact]
    public void La_bandeja_del_admin_de_empresa_es_su_empresa()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.EmpresaActiva,
            TicketAlcanceAdministracionCalculos.AlcanceAdministracion(false, new[] { Admin }));

    [Fact]
    public void El_admin_global_sin_el_permiso_tampoco_entra()
        => Assert.Equal(TicketAlcanceAdministracionCalculos.Alcance.Ninguno,
            TicketAlcanceAdministracionCalculos.AlcanceAdministracion(true, Nada));

    [Fact]
    public void Cubre_respeta_la_empresa_del_caso()
    {
        Assert.True(TicketAlcanceAdministracionCalculos.Cubre(
            TicketAlcanceAdministracionCalculos.Alcance.Todas, empresaActiva: 6, empresaCaso: 1));
        Assert.True(TicketAlcanceAdministracionCalculos.Cubre(
            TicketAlcanceAdministracionCalculos.Alcance.EmpresaActiva, 6, 6));
        Assert.False(TicketAlcanceAdministracionCalculos.Cubre(
            TicketAlcanceAdministracionCalculos.Alcance.EmpresaActiva, 6, 1));
        Assert.False(TicketAlcanceAdministracionCalculos.Cubre(
            TicketAlcanceAdministracionCalculos.Alcance.Ninguno, 6, 6));
    }

    // ── Gestionar un caso concreto ──

    [Fact]
    public void Gestionar_sigue_exigiendo_el_permiso()
        => Assert.False(TicketAlcanceAdministracionCalculos.PuedeGestionarCaso(
            esAdminEmpresas: false, new[] { "tickets.crear" }, empresaActiva: 6, empresaCaso: 6, asignadoAMi: true));

    [Fact]
    public void Con_el_permiso_se_gestiona_lo_de_la_empresa_activa()
        => Assert.True(TicketAlcanceAdministracionCalculos.PuedeGestionarCaso(
            false, new[] { Gestionar }, 6, 6, asignadoAMi: false));

    [Fact]
    public void Con_el_permiso_pero_de_otra_empresa_no_se_gestiona_por_id()
        => Assert.False(TicketAlcanceAdministracionCalculos.PuedeGestionarCaso(
            false, new[] { Gestionar }, 6, 1, asignadoAMi: false));

    [Fact]
    public void Lo_asignado_se_gestiona_aunque_sea_de_otra_empresa()
        => Assert.True(TicketAlcanceAdministracionCalculos.PuedeGestionarCaso(
            false, new[] { Gestionar }, 6, 1, asignadoAMi: true));

    [Fact]
    public void El_admin_global_gestiona_cualquier_empresa()
        => Assert.True(TicketAlcanceAdministracionCalculos.PuedeGestionarCaso(
            true, new[] { Admin }, 6, 1, asignadoAMi: false));
}
