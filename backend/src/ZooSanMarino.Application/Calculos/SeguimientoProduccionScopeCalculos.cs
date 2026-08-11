namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de acotamiento (scoping) por empresa del seguimiento diario de PRODUCCIÓN.
/// Extraída de <c>SeguimientoProduccionService</c> para poder verificarla con xUnit sin EF ni HTTP.
///
/// <para>
/// <b>El defecto que corrige.</b> Ese service no filtraba por empresa en <b>ninguno</b> de sus seis
/// métodos: <c>GetAllAsync</c> devolvía los seguimientos de todas las empresas, y
/// <c>Update</c>/<c>Delete</c> resolvían la fila por id crudo (<c>FindAsync</c>), así que cualquier
/// usuario autenticado operaba sobre las filas de otra empresa pasando el id. No era un problema de
/// autenticación —<c>Program.cs</c> fija <c>FallbackPolicy = RequireAuthenticatedUser</c>— sino de
/// <b>autorización</b>.
/// </para>
///
/// <para>
/// <b>Por qué la empresa la dicta el LOTE y no la fila.</b> El scoping se resuelve por join a
/// <c>lotes</c> (<c>l.CompanyId</c>), igual que <c>ProduccionDiariaService</c> sobre esta misma
/// tabla, y NO por la columna <c>company_id</c> del seguimiento. Esa columna no es confiable: hay
/// filas guardadas con <c>0</c>, porque el alta hacía <c>_current?.CompanyId ?? 0</c>. El lote es el
/// dato maestro; la fila es un hecho colgado de él.
/// </para>
///
/// <para>
/// <b>Fail-closed, sin excepción de "uso interno".</b> El único consumidor de este service es su
/// controller HTTP (no hay llamadores internos ni construcción manual), así que la ausencia de
/// identidad no habilita un modo sin filtro: sin empresa resoluble no se lee ni se escribe nada. Es
/// la regla 3 de <i>Features por EMPRESA</i> de CLAUDE.md — ante ambigüedad, vacío o error, nunca
/// datos de otra empresa.
/// </para>
/// </summary>
public static class SeguimientoProduccionScopeCalculos
{
    /// <summary>Qué hacer con una consulta o una escritura, según la identidad disponible.</summary>
    public enum ScopeDecision
    {
        /// <summary>Empresa no resoluble: lecturas vacías y escrituras rechazadas.</summary>
        FailClosed,

        /// <summary>Empresa válida: acotar a los lotes de esa empresa.</summary>
        FilterByCompany
    }

    /// <summary>
    /// Empresa efectiva a partir del <c>CompanyId</c> de la sesión.
    /// <para>
    /// El <c>0</c> cuenta como <b>ausencia</b>, no como empresa cero: es el valor que deja
    /// <c>_current?.CompanyId ?? 0</c> cuando no hay identidad, y también el que quedó grabado en las
    /// filas nacidas por ese camino. Tratarlo como una empresa más las volvería alcanzables desde
    /// cualquier sesión sin empresa. Mismo criterio que la clave de partición de la caché offline del
    /// front, donde el <c>0</c> tampoco vale como id.
    /// </para>
    /// </summary>
    /// <param name="companyIdSesion">
    /// <c>CompanyId</c> de <c>ICurrentUser</c>, o <c>null</c> si no hay sesión inyectada.
    /// </param>
    /// <returns>La empresa efectiva, o <c>null</c> si no hay ninguna resoluble.</returns>
    public static int? EmpresaEfectiva(int? companyIdSesion) =>
        companyIdSesion is > 0 ? companyIdSesion : null;

    /// <summary>
    /// Decide el acotamiento: sin empresa efectiva ⇒ <see cref="ScopeDecision.FailClosed"/>;
    /// con empresa válida ⇒ <see cref="ScopeDecision.FilterByCompany"/>.
    /// </summary>
    public static ScopeDecision Decidir(int? empresaEfectiva) =>
        empresaEfectiva is > 0 ? ScopeDecision.FilterByCompany : ScopeDecision.FailClosed;

    /// <summary>
    /// ¿Esta fila es alcanzable por la sesión actual? Es la regla que aplican por igual la lectura
    /// por id, la edición y el borrado.
    /// <para>
    /// Una fila de otra empresa se trata como <b>inexistente</b> (el llamador responde 404 /
    /// <c>false</c>), no como prohibida: un 403 confirmaría que ese id existe en otra empresa, que es
    /// justamente lo que no se quiere filtrar.
    /// </para>
    /// </summary>
    /// <param name="companyIdDelLote">Empresa del lote al que cuelga la fila (<c>lotes.company_id</c>).</param>
    /// <param name="empresaEfectiva">Empresa de la sesión, ya resuelta por <see cref="EmpresaEfectiva"/>.</param>
    public static bool FilaAlcanzable(int? companyIdDelLote, int? empresaEfectiva) =>
        empresaEfectiva is > 0 && companyIdDelLote == empresaEfectiva;
}
