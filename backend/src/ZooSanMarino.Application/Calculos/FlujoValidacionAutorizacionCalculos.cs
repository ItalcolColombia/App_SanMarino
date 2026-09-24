namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Cálculo puro de autorización de firma: decide si un usuario puede aprobar/devolver la etapa
/// actual de una instancia, dados sus candidatos, sus roles vigentes EN LA EMPRESA de la instancia
/// y las reglas del flujo (personas distintas, creador sin autoaprobación). No conoce EF ni HTTP;
/// el service resuelve los roles del usuario en la empresa y le pasa el resultado acá.
/// </summary>
public static class FlujoValidacionAutorizacionCalculos
{
    public enum TipoCandidato { Rol, Usuario }

    public sealed record Candidato(TipoCandidato Tipo, int? RoleId, Guid? UserId);

    /// <summary>
    /// True si <paramref name="userId"/> (con los roles vigentes <paramref name="rolesDelUsuarioEnEmpresa"/>)
    /// coincide con alguno de los candidatos de la etapa. Implementa ANY: basta un candidato.
    /// </summary>
    public static bool EsCandidatoDeLaEtapa(
        Guid userId,
        IReadOnlySet<int> rolesDelUsuarioEnEmpresa,
        IReadOnlyList<Candidato> candidatosDeLaEtapa)
    {
        foreach (var c in candidatosDeLaEtapa)
        {
            if (c.Tipo == TipoCandidato.Usuario && c.UserId == userId) return true;
            if (c.Tipo == TipoCandidato.Rol && c.RoleId.HasValue && rolesDelUsuarioEnEmpresa.Contains(c.RoleId.Value))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Regla "personas distintas entre etapas": si el flujo la exige, el usuario no puede firmar si
    /// ya firmó (APROBAR) otra etapa de este mismo intento.
    /// </summary>
    public static bool PuedeFirmarPorReglaPersonasDistintas(
        bool requierePersonasDistintas,
        Guid userId,
        IReadOnlySet<Guid> usuariosQueYaFirmaronEsteIntento) =>
        !requierePersonasDistintas || !usuariosQueYaFirmaronEsteIntento.Contains(userId);

    /// <summary>Regla "el creador no se autoaprueba" (default ON, configurable por flujo).</summary>
    public static bool PuedeFirmarPorReglaCreador(
        bool permiteAprobacionCreador,
        Guid userId,
        Guid creadorDelRegistro) =>
        permiteAprobacionCreador || userId != creadorDelRegistro;

    /// <summary>
    /// Decisión final: true si el usuario puede aprobar/devolver la etapa actual de la instancia,
    /// combinando candidatura + personas distintas + creador. No valida por sí solo que la etapa
    /// realmente esté PENDIENTE ni que la instancia pertenezca a la empresa activa: eso lo exige el
    /// service antes de invocar este cálculo (fail-closed por datos, no por este helper).
    /// </summary>
    public static bool PuedeAprobarOFirmar(
        Guid userId,
        Guid creadorDelRegistro,
        bool requierePersonasDistintas,
        bool permiteAprobacionCreador,
        IReadOnlySet<int> rolesDelUsuarioEnEmpresa,
        IReadOnlyList<Candidato> candidatosDeLaEtapa,
        IReadOnlySet<Guid> usuariosQueYaFirmaronEsteIntento) =>
        EsCandidatoDeLaEtapa(userId, rolesDelUsuarioEnEmpresa, candidatosDeLaEtapa) &&
        PuedeFirmarPorReglaPersonasDistintas(requierePersonasDistintas, userId, usuariosQueYaFirmaronEsteIntento) &&
        PuedeFirmarPorReglaCreador(permiteAprobacionCreador, userId, creadorDelRegistro);

    /// <summary>
    /// Autorización para corregir/reenviar o eliminar durante una devolución: solo el destinatario
    /// de la novedad activa (firmante anterior o creador) puede hacerlo. No es un permiso de módulo:
    /// es acceso acotado a ese registro concreto mientras la novedad siga ACTIVA.
    /// </summary>
    public static bool PuedeCorregirODevolverInstancia(Guid userId, Guid destinatarioNovedadActiva) =>
        userId == destinatarioNovedadActiva;
}
