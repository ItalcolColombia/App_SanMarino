using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Quién puede configurar los tickets de un usuario o de un rol. Hasta el 19-sep-2026 no había gate:
/// cualquiera podía hacerse Implementador o resolutor con un PUT sobre su propio id.
/// </summary>
public class TicketPerfilAutorizacionCalculosTests
{
    private static readonly string[] AdminTickets = { "tickets.admin" };
    private static readonly string[] SoloCrear = { "tickets.crear" };

    private static TicketPerfilAutorizacionCalculos.Decision Decidir(
        bool esAdminEmpresas = false, string[]? permisos = null, int activa = 6, int destino = 6,
        bool esUnoMismo = false, bool tocaGlobal = false) =>
        TicketPerfilAutorizacionCalculos.PuedeEscribir(
            esAdminEmpresas, permisos ?? AdminTickets, activa, destino, esUnoMismo, tocaGlobal);

    [Fact]
    public void El_admin_global_puede_todo_incluso_global_y_otra_empresa()
        => Assert.True(Decidir(esAdminEmpresas: true, permisos: Array.Empty<string>(),
            activa: 1, destino: 6, esUnoMismo: true, tocaGlobal: true).Permitido);

    [Fact]
    public void El_admin_de_empresa_configura_su_empresa()
        => Assert.True(Decidir().Permitido);

    [Fact]
    public void El_admin_de_empresa_no_puede_marcar_global()
    {
        var d = Decidir(tocaGlobal: true);
        Assert.False(d.Permitido);
        Assert.Contains("administrador global", d.Motivo);
    }

    [Fact]
    public void Nadie_se_configura_a_si_mismo()
    {
        var d = Decidir(esUnoMismo: true);
        Assert.False(d.Permitido);
        Assert.Contains("tu propio perfil", d.Motivo);
    }

    [Fact]
    public void Sin_el_permiso_de_administrador_de_tickets_no_se_escribe()
    {
        var d = Decidir(permisos: SoloCrear);
        Assert.False(d.Permitido);
        Assert.Contains("tickets.admin", d.Motivo);
    }

    [Fact]
    public void El_admin_de_una_empresa_no_toca_otra()
    {
        var d = Decidir(activa: 6, destino: 1);
        Assert.False(d.Permitido);
        Assert.Contains("empresa activa", d.Motivo);
    }

    [Fact]
    public void Permisos_null_es_fail_closed()
        => Assert.False(TicketPerfilAutorizacionCalculos.PuedeEscribir(
            false, null, 6, 6, esUnoMismo: false, tocaGlobal: false).Permitido);

    [Fact]
    public void Solo_el_admin_global_ve_la_opcion_global_en_pantalla()
    {
        Assert.True(TicketPerfilAutorizacionCalculos.PuedeElegirGlobal(true));
        Assert.False(TicketPerfilAutorizacionCalculos.PuedeElegirGlobal(false));
    }
}
