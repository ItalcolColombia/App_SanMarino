using ZooSanMarino.Application.Calculos;
using static ZooSanMarino.Application.Calculos.FlujoValidacionAutorizacionCalculos;

namespace ZooSanMarino.Application.Tests;

/// <summary>
/// Contrato de autorización de firma del motor de flujos de validación (plan, casos 12-17, 31-35).
/// </summary>
public class FlujoValidacionAutorizacionCalculosTests
{
    private static readonly Guid Tecnico = Guid.NewGuid();
    private static readonly Guid Lider = Guid.NewGuid();
    private static readonly Guid Creador = Guid.NewGuid();
    private const int RolTecnico = 10;
    private const int RolLider = 20;

    // ─── Candidatura ANY (caso 15) ─────────────────────────────────────────────

    [Fact]
    public void EsCandidatoDeLaEtapa_PorUsuarioEspecifico()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Usuario, null, Tecnico) };

        Assert.True(EsCandidatoDeLaEtapa(Tecnico, new HashSet<int>(), candidatos));
        Assert.False(EsCandidatoDeLaEtapa(Lider, new HashSet<int>(), candidatos));
    }

    [Fact]
    public void EsCandidatoDeLaEtapa_PorRolVigenteEnLaEmpresa()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Rol, RolTecnico, null) };

        Assert.True(EsCandidatoDeLaEtapa(Tecnico, new HashSet<int> { RolTecnico }, candidatos));
    }

    [Fact]
    public void EsCandidatoDeLaEtapa_RolEnOtraEmpresaNoCuenta()
    {
        // El caller solo debe pasar los roles vigentes EN LA EMPRESA de la instancia (caso 14 del plan).
        var candidatos = new List<Candidato> { new(TipoCandidato.Rol, RolTecnico, null) };

        Assert.False(EsCandidatoDeLaEtapa(Tecnico, new HashSet<int>(), candidatos));
    }

    [Fact]
    public void EsCandidatoDeLaEtapa_VariosCandidatos_BastaUno_ANY()
    {
        var candidatos = new List<Candidato>
        {
            new(TipoCandidato.Usuario, null, Tecnico),
            new(TipoCandidato.Usuario, null, Lider),
        };

        Assert.True(EsCandidatoDeLaEtapa(Lider, new HashSet<int>(), candidatos));
    }

    // ─── Personas distintas (caso 17) ──────────────────────────────────────────

    [Fact]
    public void PersonasDistintas_ON_BloqueaAQuienYaFirmoOtraEtapa()
    {
        var yaFirmaron = new HashSet<Guid> { Tecnico };

        Assert.False(PuedeFirmarPorReglaPersonasDistintas(true, Tecnico, yaFirmaron));
        Assert.True(PuedeFirmarPorReglaPersonasDistintas(true, Lider, yaFirmaron));
    }

    [Fact]
    public void PersonasDistintas_OFF_PermiteFirmarDosEtapas()
    {
        var yaFirmaron = new HashSet<Guid> { Tecnico };

        Assert.True(PuedeFirmarPorReglaPersonasDistintas(false, Tecnico, yaFirmaron));
    }

    // ─── Creador sin autoaprobación (caso 16) ──────────────────────────────────

    [Fact]
    public void CreadorSinAutoaprobacion_ON_BloqueaAlCreador()
    {
        Assert.False(PuedeFirmarPorReglaCreador(permiteAprobacionCreador: false, Creador, Creador));
        Assert.True(PuedeFirmarPorReglaCreador(permiteAprobacionCreador: false, Tecnico, Creador));
    }

    [Fact]
    public void CreadorSinAutoaprobacion_OFF_PermiteAlCreador()
    {
        Assert.True(PuedeFirmarPorReglaCreador(permiteAprobacionCreador: true, Creador, Creador));
    }

    // ─── Decisión combinada ────────────────────────────────────────────────────

    [Fact]
    public void PuedeAprobarOFirmar_TodasLasReglasEnRegla_Aprueba()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Rol, RolTecnico, null) };

        var puede = PuedeAprobarOFirmar(
            userId: Tecnico,
            creadorDelRegistro: Creador,
            requierePersonasDistintas: true,
            permiteAprobacionCreador: false,
            rolesDelUsuarioEnEmpresa: new HashSet<int> { RolTecnico },
            candidatosDeLaEtapa: candidatos,
            usuariosQueYaFirmaronEsteIntento: new HashSet<Guid>());

        Assert.True(puede);
    }

    [Fact]
    public void PuedeAprobarOFirmar_UsuarioSinAsignacion_NoAprueba()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Rol, RolTecnico, null) };

        var puede = PuedeAprobarOFirmar(
            userId: Lider,
            creadorDelRegistro: Creador,
            requierePersonasDistintas: true,
            permiteAprobacionCreador: false,
            rolesDelUsuarioEnEmpresa: new HashSet<int> { RolLider },
            candidatosDeLaEtapa: candidatos,
            usuariosQueYaFirmaronEsteIntento: new HashSet<Guid>());

        Assert.False(puede);
    }

    [Fact]
    public void PuedeAprobarOFirmar_CandidatoPeroYaFirmoOtraEtapa_NoAprueba()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Usuario, null, Tecnico) };

        var puede = PuedeAprobarOFirmar(
            userId: Tecnico,
            creadorDelRegistro: Creador,
            requierePersonasDistintas: true,
            permiteAprobacionCreador: false,
            rolesDelUsuarioEnEmpresa: new HashSet<int>(),
            candidatosDeLaEtapa: candidatos,
            usuariosQueYaFirmaronEsteIntento: new HashSet<Guid> { Tecnico });

        Assert.False(puede);
    }

    [Fact]
    public void PuedeAprobarOFirmar_CreadorCandidatoSinPermiso_NoAprueba()
    {
        var candidatos = new List<Candidato> { new(TipoCandidato.Usuario, null, Creador) };

        var puede = PuedeAprobarOFirmar(
            userId: Creador,
            creadorDelRegistro: Creador,
            requierePersonasDistintas: true,
            permiteAprobacionCreador: false,
            rolesDelUsuarioEnEmpresa: new HashSet<int>(),
            candidatosDeLaEtapa: candidatos,
            usuariosQueYaFirmaronEsteIntento: new HashSet<Guid>());

        Assert.False(puede);
    }

    // ─── Corrección acotada al destinatario de la novedad (casos 31-33) ───────

    [Fact]
    public void PuedeCorregirODevolverInstancia_SoloElDestinatarioExacto()
    {
        Assert.True(PuedeCorregirODevolverInstancia(Lider, destinatarioNovedadActiva: Lider));
        Assert.False(PuedeCorregirODevolverInstancia(Tecnico, destinatarioNovedadActiva: Lider));
    }
}
