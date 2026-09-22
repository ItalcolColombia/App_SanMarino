// src/ZooSanMarino.Application/Calculos/TicketPerfilEmpresaCalculos.cs
// Regla PURA: en qué empresa se guarda / lee el perfil de tickets de un usuario o de un rol.
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// En qué EMPRESA vive el perfil de tickets (nivel de apertura del usuario, plantilla de atención
/// del rol) que se está leyendo o escribiendo.
/// </summary>
/// <remarks>
/// <para>
/// <b>Por qué existe.</b> Hasta el 19-sep-2026 <c>TicketPerfilService</c> usaba la empresa activa
/// DEL QUE EDITA. El admin global parado en Sanmarino habilitó a un usuario de Santa Reyes y el
/// perfil se escribió con <c>company_id</c> de Sanmarino: en Santa Reyes el usuario siguió sin
/// poder abrir tickets. Medido sobre la copia de producción: 5 de 10 perfiles estaban en una empresa
/// a la que el usuario no pertenece. Y como el GET usaba la misma empresa equivocada, al reabrirlo la
/// pantalla mostraba el nivel «guardado» y parecía correcto.
/// </para>
/// <para>
/// <b>La regla.</b> Manda la empresa del DESTINO (el usuario o el rol). Si el destino pertenece a la
/// empresa activa, se usa esa (así un usuario de varias empresas se configura en la que el admin está
/// parado). Si no, y el destino tiene UNA sola empresa, se usa esa. Si tiene varias o ninguna, no hay
/// respuesta segura ⇒ <c>null</c> y el service responde 400 (fail-closed: nunca se adivina).
/// </para>
/// </remarks>
public static class TicketPerfilEmpresaCalculos
{
    /// <param name="empresaActiva">Empresa activa de la sesión del que edita (o null si no hay).</param>
    /// <param name="empresasDelDestino">
    /// Empresas del destino: <c>user_companies</c> del usuario o <c>role_companies</c> del rol.
    /// </param>
    /// <returns>La empresa donde vive el perfil, o <c>null</c> si es ambiguo o no tiene empresa.</returns>
    public static int? ResolverEmpresa(int? empresaActiva, IEnumerable<int>? empresasDelDestino)
    {
        if (empresasDelDestino is null) return null;
        var empresas = empresasDelDestino.Where(id => id > 0).Distinct().ToList();
        if (empresas.Count == 0) return null;
        if (empresaActiva is int activa && empresas.Contains(activa)) return activa;
        return empresas.Count == 1 ? empresas[0] : null;
    }

    /// <summary>Mensaje del 400 cuando <see cref="ResolverEmpresa"/> no tiene respuesta.</summary>
    /// <param name="destino">«usuario» o «rol».</param>
    /// <param name="cantidadEmpresas">Cuántas empresas tiene el destino.</param>
    public static string MensajeSinEmpresa(string destino, int cantidadEmpresas) =>
        cantidadEmpresas == 0
            ? $"El {destino} no pertenece a ninguna empresa: asignale una empresa antes de configurar sus tickets."
            : $"El {destino} pertenece a varias empresas y ninguna es la empresa activa: cambiá a la empresa donde querés configurarlo.";
}
