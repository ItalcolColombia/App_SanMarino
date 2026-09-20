using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Nivel para ABRIR tickets. Lo que sostienen estos tests es la <b>equivalencia</b>: mientras ningún rol
/// defina nivel (la columna nace NULL en todos), el resultado tiene que ser exactamente el de antes —
/// permiso primero, después el perfil personal, si no NORMAL.
/// </summary>
public class TicketNivelEfectivoCalculosTests
{
    private static readonly string[] SinPermisos = Array.Empty<string>();
    private static readonly string?[] SinRoles = Array.Empty<string?>();

    // ── Equivalencia con la lógica previa (ningún rol define nivel) ──

    [Theory]
    [InlineData("tickets.gestionar")]
    [InlineData("tickets.admin")]
    [InlineData("TICKETS.ADMIN")]
    public void El_permiso_de_gestion_sigue_dando_implementador(string permiso)
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelEfectivo(new[] { permiso }, SinRoles, nivelPerfilUsuario: null));

    [Fact]
    public void Sin_permiso_ni_perfil_es_normal()
        => Assert.Equal(NivelTicket.Normal,
            TicketNivelEfectivoCalculos.NivelEfectivo(SinPermisos, SinRoles, null));

    [Fact]
    public void Sin_permiso_manda_el_perfil_personal_tal_cual_esta_guardado()
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelEfectivo(new[] { "tickets.crear" }, SinRoles, NivelTicket.Implementador));

    [Fact]
    public void Un_perfil_normal_sigue_siendo_normal()
        => Assert.Equal(NivelTicket.Normal,
            TicketNivelEfectivoCalculos.NivelEfectivo(new[] { "tickets.crear" }, SinRoles, NivelTicket.Normal));

    [Fact]
    public void Permisos_null_no_revienta_y_cae_a_normal()
        => Assert.Equal(NivelTicket.Normal,
            TicketNivelEfectivoCalculos.NivelEfectivo(null, null, null));

    // ── Lo nuevo: el nivel lo puede dar el ROL ──

    [Fact]
    public void El_rol_implementador_alcanza_sin_tocar_al_usuario()
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelEfectivo(new[] { "tickets.crear" },
                new string?[] { null, NivelTicket.Implementador }, nivelPerfilUsuario: null));

    [Fact]
    public void Gana_el_mayor_entre_rol_y_perfil_personal()
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelEfectivo(SinPermisos,
                new string?[] { NivelTicket.Implementador }, nivelPerfilUsuario: NivelTicket.Normal));

    [Fact]
    public void Un_rol_normal_no_le_saca_el_implementador_al_perfil()
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelEfectivo(SinPermisos,
                new string?[] { NivelTicket.Normal }, nivelPerfilUsuario: NivelTicket.Implementador));

    [Fact]
    public void Roles_sin_nivel_definido_no_aportan()
        => Assert.Null(TicketNivelEfectivoCalculos.NivelDeRoles(new string?[] { null, "", "   " }));

    [Fact]
    public void Un_valor_basura_en_la_columna_no_habilita_nada()
    {
        Assert.Null(TicketNivelEfectivoCalculos.NivelDeRoles(new string?[] { "SUPER" }));
        Assert.Equal(NivelTicket.Normal,
            TicketNivelEfectivoCalculos.NivelEfectivo(SinPermisos, new string?[] { "SUPER" }, null));
    }

    [Theory]
    [InlineData("implementador", "IMPLEMENTADOR")]
    [InlineData("  Normal  ", "NORMAL")]
    [InlineData("NORMAL", "NORMAL")]
    public void El_nivel_del_rol_se_normaliza(string guardado, string esperado)
        => Assert.Equal(esperado, TicketNivelEfectivoCalculos.NormalizarNivelRol(guardado));

    [Fact]
    public void El_mayor_de_varios_roles_es_implementador()
        => Assert.Equal(NivelTicket.Implementador,
            TicketNivelEfectivoCalculos.NivelDeRoles(new string?[] { NivelTicket.Normal, null, NivelTicket.Implementador }));
}
