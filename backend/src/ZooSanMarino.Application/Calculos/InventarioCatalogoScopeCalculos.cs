namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Lógica PURA de acotamiento (scoping) del catálogo de ítems de inventario por empresa/país.
/// Extraída de <c>ItemInventarioService</c> para poder verificarla con xUnit sin EF/HTTP.
///
/// Regla de negocio (fix multi-empresa): el catálogo de ítems debe resolver la MISMA empresa que
/// las granjas (evita ver granjas de una empresa e ítems de otra), y NUNCA exponer todo el catálogo
/// cuando hay sesión pero no se resuelve empresa (evita la fuga que mostraba los ítems de Ecuador en Panamá).
/// </summary>
public static class InventarioCatalogoScopeCalculos
{
    public enum ScopeDecision
    {
        /// <summary>Sin sesión (uso interno/no-HTTP): no se aplica filtro de empresa.</summary>
        NoSession,
        /// <summary>Con sesión pero empresa no resoluble: devolver vacío (fail-closed).</summary>
        FailClosed,
        /// <summary>Con sesión y empresa válida: filtrar por esa empresa.</summary>
        FilterByCompany
    }

    /// <summary>
    /// Empresa efectiva: prioriza la resuelta por NOMBRE del header (X-Active-Company) y cae al
    /// CompanyId del token. Devuelve null si ninguna es válida (&gt; 0).
    /// </summary>
    public static int? EmpresaEfectiva(int? companyIdPorNombre, int companyIdToken)
    {
        if (companyIdPorNombre is > 0) return companyIdPorNombre;
        return companyIdToken > 0 ? companyIdToken : (int?)null;
    }

    /// <summary>
    /// Decide cómo acotar el catálogo:
    /// sin sesión ⇒ <see cref="ScopeDecision.NoSession"/>;
    /// con sesión y empresa no resoluble ⇒ <see cref="ScopeDecision.FailClosed"/>;
    /// con sesión y empresa válida ⇒ <see cref="ScopeDecision.FilterByCompany"/>.
    /// </summary>
    public static ScopeDecision Decidir(bool currentUserPresent, int? empresaEfectiva)
    {
        if (!currentUserPresent) return ScopeDecision.NoSession;
        if (empresaEfectiva is null or <= 0) return ScopeDecision.FailClosed;
        return ScopeDecision.FilterByCompany;
    }
}
