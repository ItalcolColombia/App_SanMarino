using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// En qué empresa se guarda el perfil de tickets. El caso que da nombre a estos tests es real: el admin
/// global, parado en Sanmarino (1), habilitó a Lenin —usuario de Santa Reyes (6)— y el perfil se guardó
/// en la empresa 1, así que en Santa Reyes Lenin siguió sin poder abrir casos.
/// </summary>
public class TicketPerfilEmpresaCalculosTests
{
    [Fact]
    public void Manda_la_empresa_del_usuario_cuando_no_es_la_activa()
        => Assert.Equal(6, TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 1, new[] { 6 }));

    [Fact]
    public void Si_el_destino_pertenece_a_la_empresa_activa_se_usa_esa()
        => Assert.Equal(1, TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 1, new[] { 6, 1, 4 }));

    [Fact]
    public void Varias_empresas_y_ninguna_es_la_activa_es_ambiguo()
        => Assert.Null(TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 1, new[] { 3, 6 }));

    [Fact]
    public void Sin_empresas_no_hay_donde_guardarlo()
        => Assert.Null(TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 1, Array.Empty<int>()));

    [Fact]
    public void Null_es_fail_closed()
        => Assert.Null(TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 1, null));

    [Fact]
    public void Sin_empresa_activa_pero_con_una_sola_empresa_se_resuelve()
        => Assert.Equal(5, TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: null, new[] { 5 }));

    [Fact]
    public void Ids_invalidos_y_repetidos_no_confunden_la_cuenta()
        => Assert.Equal(6, TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 0, new[] { 6, 6, 0, -3 }));

    [Fact]
    public void Empresa_activa_cero_no_matchea_aunque_haya_varias()
        => Assert.Null(TicketPerfilEmpresaCalculos.ResolverEmpresa(empresaActiva: 0, new[] { 3, 6 }));

    [Theory]
    [InlineData("usuario", 0)]
    [InlineData("rol", 3)]
    public void El_mensaje_distingue_sin_empresa_de_ambigua(string destino, int cantidad)
    {
        var mensaje = TicketPerfilEmpresaCalculos.MensajeSinEmpresa(destino, cantidad);

        Assert.Contains(destino, mensaje);
        if (cantidad == 0) Assert.Contains("no pertenece a ninguna empresa", mensaje);
        else Assert.Contains("varias empresas", mensaje);
    }
}
