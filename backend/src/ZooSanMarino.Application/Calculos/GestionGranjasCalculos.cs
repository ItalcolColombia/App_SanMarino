// Reglas PURAS del módulo "Gestión de Granjas" (tabs Granjas / Núcleos / Galpones).
//
// 1) Visibilidad por granja: los listados de Núcleos y Galpones de las tabs deben mostrar
//    EXACTAMENTE el mismo alcance que la tab Granjas → solo lo de las granjas asignadas al
//    usuario (UserFarms) y que estén ACTIVAS (no eliminadas). Regla usada como contrato de lo
//    que la consulta EF filtra en BD (NucleoService/GalponService.GetAllAsync).
//
// 2) Cascada al eliminar: al deshabilitar una granja se deshabilitan (soft-delete) sus núcleos
//    y galpones que aún estén activos. `RequiereInhabilitar` decide, por registro, si hay que
//    tocarlo (idempotente: si ya está eliminado, no se re-toca).
namespace ZooSanMarino.Application.Calculos;

public static class GestionGranjasCalculos
{
    /// <summary>
    /// True si la granja <paramref name="granjaId"/> es visible para el usuario: debe estar entre
    /// las granjas asignadas y estar activa (no eliminada).
    /// </summary>
    public static bool EsVisiblePorGranja(int granjaId, ISet<int> granjasAsignadas, ISet<int> granjasActivas)
        => granjasAsignadas.Contains(granjaId) && granjasActivas.Contains(granjaId);

    /// <summary>
    /// Filtra una colección de ítems (núcleos/galpones) dejando solo los visibles según
    /// <see cref="EsVisiblePorGranja"/>. <paramref name="granjaSelector"/> extrae el GranjaId del ítem.
    /// </summary>
    public static IEnumerable<T> FiltrarVisiblesPorGranja<T>(
        IEnumerable<T> items,
        Func<T, int> granjaSelector,
        ISet<int> granjasAsignadas,
        ISet<int> granjasActivas)
        => (items ?? Enumerable.Empty<T>())
            .Where(x => EsVisiblePorGranja(granjaSelector(x), granjasAsignadas, granjasActivas));

    /// <summary>
    /// Indica si un registro con soft-delete debe ser inhabilitado durante una cascada.
    /// Idempotente: solo los que aún están activos (DeletedAt == null).
    /// </summary>
    public static bool RequiereInhabilitar(DateTime? deletedAt) => deletedAt is null;
}
