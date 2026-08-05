// src/ZooSanMarino.Application/Calculos/InventarioGastoReporteCalculos.cs
namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Reglas PURAS del reporte de Gastos de inventario (sin EF, sin estado): qué gasto entra al
/// reporte y cómo se ordenan/etiquetan los conceptos del catálogo.
///
/// Existe porque la regla "un gasto eliminado NO va al reporte" estaba implícita y repartida:
/// la lista la respetaba desde la UI (badge) y el export no la aplicaba nunca, así que el Excel
/// mezclaba consumos anulados con los reales. Un solo dueño de la regla evita que vuelva a divergir.
/// </summary>
public static class InventarioGastoReporteCalculos
{
    /// <summary>Estado de una cabecera de gasto vigente.</summary>
    public const string EstadoActivo = "Activo";

    /// <summary>Estado de una cabecera de gasto anulada (el stock ya volvió al inventario).</summary>
    public const string EstadoEliminado = "Eliminado";

    /// <summary>Etiqueta para los ítems del catálogo que no tienen concepto asignado.</summary>
    public const string ConceptoSinAsignar = "(Sin concepto)";

    /// <summary>
    /// ¿El gasto está eliminado (anulado)? Comparación laxa (trim + case-insensitive) porque el
    /// estado es texto libre en BD. Un estado nulo/vacío NO es eliminado: se trata como vigente,
    /// igual que el default de la entidad.
    /// </summary>
    public static bool EsGastoEliminado(string? estado) =>
        string.Equals(estado?.Trim(), EstadoEliminado, StringComparison.OrdinalIgnoreCase);

    /// <summary>¿El gasto debe entrar al reporte? Complemento exacto de <see cref="EsGastoEliminado"/>.</summary>
    public static bool EsGastoActivo(string? estado) => !EsGastoEliminado(estado);

    /// <summary>
    /// Clave de ORDEN/agrupación de un concepto del catálogo. Normaliza espacios y capitalización
    /// para que variantes del mismo concepto ("Otros insumos" / "Otros Insumos", que conviven hoy
    /// en el catálogo) queden juntas en el reporte.
    ///
    /// Es SOLO clave de orden: el reporte muestra el concepto tal cual está en el catálogo, para no
    /// alterar lo que el usuario ve hoy. Los ítems sin concepto ordenan al final (clave '~').
    /// </summary>
    public static string ClaveOrdenConcepto(string? concepto)
    {
        var t = concepto?.Trim();
        return string.IsNullOrEmpty(t) ? "~" : t.ToLowerInvariant();
    }

    /// <summary>Concepto tal cual va al reporte: el del catálogo, o la etiqueta de "sin concepto".</summary>
    public static string EtiquetaConcepto(string? concepto)
    {
        var t = concepto?.Trim();
        return string.IsNullOrEmpty(t) ? ConceptoSinAsignar : t;
    }
}
