namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Alcance de un resolutor de tickets (<see cref="TicketResolutor"/> y <see cref="TicketResolutorRol"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>EMPRESA</b> (default): la fila atiende SOLO los tickets de su <c>company_id</c>. La configura el
/// administrador de tickets de esa empresa (o el admin global).
/// </para>
/// <para>
/// <b>GLOBAL</b>: la fila atiende los tickets de TODAS las empresas, incluidas las que se creen
/// después. Solo la puede escribir el admin global (policy <c>AdminEmpresas</c>). Su <c>company_id</c>
/// queda como «empresa desde la que se configuró» y no filtra nada.
/// </para>
/// <para>
/// Antes del 19-sep-2026 «Global» era la etiqueta de <c>pais_id NULL</c> (todos los países DE ESA
/// empresa) y el global real se lograba repitiendo la fila del rol <c>Admin</c> en cada empresa. Ver
/// <c>fase_de_desarrollo/tickets_crear_vs_atender_empresa_global_plan.md</c>.
/// </para>
/// </remarks>
public static class TicketAlcance
{
    public const string Empresa = "EMPRESA";
    public const string Global  = "GLOBAL";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Empresa, Global };

    /// <summary>Normaliza a EMPRESA/GLOBAL; cualquier otra cosa (null, vacío, basura) ⇒ EMPRESA.</summary>
    public static string Normalizar(string? alcance) =>
        string.Equals(alcance?.Trim(), Global, StringComparison.OrdinalIgnoreCase) ? Global : Empresa;
}
