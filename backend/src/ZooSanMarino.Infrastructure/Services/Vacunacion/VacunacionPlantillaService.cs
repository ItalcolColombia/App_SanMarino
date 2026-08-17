// Vacunacion/VacunacionPlantillaService.cs
// Partial 'ancla': campos, ctor, validaciones compartidas y mapeos. La interfaz va SOLO acá.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Administración del plan de vacunación estándar de la empresa.
///
/// <para>
/// Todo lo que consulta filtra por <c>CompanyId</c> de la empresa activa <b>y</b> por
/// <c>DeletedAt == null</c>: un id de otra empresa no devuelve datos, devuelve «no existe».
/// </para>
/// </summary>
public partial class VacunacionPlantillaService : IVacunacionPlantillaService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public VacunacionPlantillaService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    private static readonly HashSet<string> LineasValidas = new(StringComparer.Ordinal) { "Levante", "Produccion", "Engorde" };

    /// <summary>Plantillas VIVAS de la empresa activa. Base de toda consulta del servicio.</summary>
    private IQueryable<VacunacionPlanPlantilla> PlantillasDeLaEmpresa() =>
        _ctx.VacunacionPlanPlantilla
            .Where(p => p.CompanyId == _currentUser.CompanyId && p.DeletedAt == null);

    /// <summary>Ítems VIVOS de una plantilla de la empresa activa.</summary>
    private IQueryable<VacunacionPlanPlantillaItem> ItemsDeLaPlantilla(int plantillaId) =>
        _ctx.VacunacionPlanPlantillaItem
            .Where(i => i.PlantillaId == plantillaId && i.CompanyId == _currentUser.CompanyId && i.DeletedAt == null);

    /// <summary>
    /// Valida la cabecera y devuelve la línea/raza ya normalizadas. Lanza con el motivo exacto:
    /// el controller lo traduce a 400 para que la pantalla pueda mostrarlo tal cual.
    /// </summary>
    private static (string Linea, string? Raza) ValidarCabecera(string? nombre, string? lineaProductiva, string? raza)
    {
        var n = (nombre ?? "").Trim();
        if (n.Length == 0)
            throw new InvalidOperationException("El nombre de la plantilla es obligatorio.");

        var linea = (lineaProductiva ?? "").Trim();
        if (!LineasValidas.Contains(linea))
            throw new InvalidOperationException($"lineaProductiva inválida: '{lineaProductiva}'. Debe ser Levante, Produccion o Engorde.");

        var r = (raza ?? "").Trim();
        return (linea, r.Length == 0 ? null : r);
    }

    /// <summary>
    /// Corre las reglas puras del ítem contra la línea de SU plantilla y contra los ítems que ya
    /// tiene. Cada una devuelve el motivo o <c>null</c>; el servicio sólo las encadena.
    /// </summary>
    private static void ValidarItem(
        string lineaDeLaPlantilla,
        IEnumerable<VacunacionPlantillaCalculos.ItemExistente> existentes,
        int itemInventarioId,
        string? unidadObjetivo,
        int valorObjetivo,
        int rangoAntes,
        int rangoDespues,
        int? idEditando)
    {
        var motivo = VacunacionPlantillaCalculos.MotivoItemInvalido(unidadObjetivo, valorObjetivo, rangoAntes, rangoDespues)
            ?? VacunacionPlantillaCalculos.MotivoUnidadNoCorrespondeALinea(lineaDeLaPlantilla, unidadObjetivo)
            ?? VacunacionPlantillaCalculos.MotivoItemDuplicado(existentes, itemInventarioId, unidadObjetivo, valorObjetivo, idEditando);

        if (motivo is not null) throw new InvalidOperationException(motivo);
    }

    /// <summary>Vacuna del catálogo de la empresa activa, o error nombrando el id que no se pudo usar.</summary>
    private async Task<ItemInventario> ResolverVacunaAsync(int itemInventarioId, CancellationToken ct) =>
        await _ctx.ItemInventario.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == itemInventarioId && x.CompanyId == _currentUser.CompanyId, ct)
        ?? throw new InvalidOperationException($"Vacuna (ItemInventario) {itemInventarioId} no existe o no pertenece a la empresa activa.");

    // ─── Mapeos ───────────────────────────────────────────────────────────────

    private static VacunacionPlantillaItemDto MapItem(VacunacionPlanPlantillaItem i, string vacunaNombre) =>
        new(i.Id, i.PlantillaId, i.ItemInventarioId, vacunaNombre,
            i.UnidadObjetivo, i.ValorObjetivo, i.RangoDiasAntes, i.RangoDiasDespues, i.Orden, i.Notas);

    /// <summary>
    /// Orden en que se muestran y se van a materializar: primero el <c>Orden</c> que puso el usuario y
    /// después el objetivo, para que dos ítems sin orden explícito no dependan del azar de la base.
    /// </summary>
    private async Task<VacunacionPlantillaDetalleDto> MapDetalleAsync(VacunacionPlanPlantilla p, CancellationToken ct)
    {
        var items = await (
            from i in ItemsDeLaPlantilla(p.Id).AsNoTracking()
            join v in _ctx.ItemInventario.AsNoTracking() on i.ItemInventarioId equals v.Id
            orderby i.Orden, i.ValorObjetivo, i.Id
            select new VacunacionPlantillaItemDto(
                i.Id, i.PlantillaId, i.ItemInventarioId, v.Nombre,
                i.UnidadObjetivo, i.ValorObjetivo, i.RangoDiasAntes, i.RangoDiasDespues, i.Orden, i.Notas)
        ).ToListAsync(ct);

        return new VacunacionPlantillaDetalleDto(
            p.Id, p.Nombre, p.LineaProductiva, p.Raza, p.VigenteDesde, p.Activa, p.Notas, items);
    }
}
