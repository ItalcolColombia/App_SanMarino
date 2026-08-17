// Vacunacion/VacunacionMaterializadorService.cs
// Partial 'ancla': campos, ctor, tipos internos, alcance y mapeos. La interfaz va SOLO acá.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;
using Calc = ZooSanMarino.Application.Calculos.VacunacionMaterializadorCalculos;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Baja el plan de vacunación de la empresa al cronograma de los lotes.
///
/// <para>
/// Servicio propio y no un partial de los dos que ya existen: <c>VacunacionPlantillaService</c>
/// administra el plan de la empresa y <c>VacunacionCronogramaService</c> el cronograma de un lote.
/// Éste es el puente —lee el primero y escribe el segundo—, y meterlo en cualquiera de los dos le
/// daría a un servicio de plan la capacidad de escribir en lotes.
/// </para>
///
/// <para>
/// Toda decisión de <b>qué</b> escribir sale de <see cref="Calc.Planificar"/>, que es puro. Acá sólo
/// se resuelven los datos y se ejecuta lo que esa función devuelve — por eso la vista previa y la
/// aplicación no pueden divergir.
/// </para>
/// </summary>
public partial class VacunacionMaterializadorService : IVacunacionMaterializadorService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly ILocationScopeResolver _scopeResolver;
    private readonly ILogger<VacunacionMaterializadorService> _logger;

    public VacunacionMaterializadorService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        ILocationScopeResolver scopeResolver,
        ILogger<VacunacionMaterializadorService> logger)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _scopeResolver = scopeResolver;
        _logger = logger;
    }

    private static readonly HashSet<string> LineasValidas = new(StringComparer.Ordinal) { "Levante", "Produccion", "Engorde" };

    private static string ValidarLinea(string? lineaProductiva)
    {
        var linea = (lineaProductiva ?? "").Trim();
        if (!LineasValidas.Contains(linea))
            throw new InvalidOperationException($"lineaProductiva inválida: '{lineaProductiva}'. Debe ser Levante, Produccion o Engorde.");
        return linea;
    }

    // ─── Tipos internos ───────────────────────────────────────────────────────

    /// <summary>
    /// Lo que hace falta de un lote para resolverle la plantilla, escribirle el cronograma y decidir
    /// si el usuario lo puede ver.
    /// </summary>
    /// <param name="LoteTablaId"><c>lotes.lote_id</c> cuando existe; es el nivel fino del alcance.</param>
    /// <param name="Cerrado">Los cerrados quedan fuera del masivo (§3.8 del plan).</param>
    private sealed record LoteVivo(
        string LineaProductiva,
        int LoteId,
        string LoteNombre,
        string? Raza,
        DateTime? FechaEncaset,
        int GranjaId,
        string? NucleoId,
        string? GalponId,
        int? LoteTablaId,
        bool Cerrado);

    /// <summary>Un lote con su plantilla resuelta y el plan de escritura ya calculado.</summary>
    private sealed record Resolucion(
        LoteVivo Lote,
        int? PlantillaId,
        string? PlantillaNombre,
        string Motivo,
        Calc.Plan Plan,
        IReadOnlyDictionary<int, Calc.ItemPlantilla> ItemsPlantilla);

    // ─── Alcance ──────────────────────────────────────────────────────────────

    /// <summary>
    /// ¿El usuario alcanza a este lote? Mismo criterio que
    /// <c>VacunacionCronogramaService.PermiteLoteDeLineaAsync</c>: si la granja no está restringida
    /// pasa; con restricción manda el nivel lote y, si el registro no lo tiene, el galpón o el núcleo.
    ///
    /// <para>
    /// Acá pesa más que en la lectura: materializar <b>escribe</b>. Un lote fuera del alcance no se
    /// materializa ni por el botón ni por el masivo.
    /// </para>
    /// </summary>
    private async Task<bool> PermiteAsync(LoteVivo lote)
    {
        var scope = await _scopeResolver.GetScopeAsync(lote.GranjaId);
        if (scope.IsGlobal) return true;
        if (lote.LoteTablaId.HasValue) return scope.PermiteLote(lote.LoteTablaId.Value);
        return (!string.IsNullOrEmpty(lote.GalponId) && scope.PermiteGalpon(lote.GalponId))
            || (string.IsNullOrEmpty(lote.GalponId) && !string.IsNullOrEmpty(lote.NucleoId) && scope.PermiteNucleo(lote.NucleoId));
    }

    // ─── Mapeos ───────────────────────────────────────────────────────────────

    private static VacunacionMaterializacionConteosDto Conteos(Calc.Plan plan) =>
        new(plan.Faltantes.Count,
            plan.Actualizables.Count,
            plan.Preservados.Count(p => p.Motivo == Calc.MotivoPreservado.YaAplicado),
            plan.Preservados.Count(p => p.Motivo == Calc.MotivoPreservado.Manual),
            plan.Preservados.Count(p => p.Motivo == Calc.MotivoPreservado.SinCambios),
            plan.Sobrantes.Count);

    private static VacunacionMaterializacionConteosDto Sumar(IEnumerable<VacunacionMaterializacionConteosDto> partes)
    {
        var lista = partes.ToList();
        return new VacunacionMaterializacionConteosDto(
            lista.Sum(c => c.Faltantes),
            lista.Sum(c => c.Actualizables),
            lista.Sum(c => c.YaAplicados),
            lista.Sum(c => c.Manuales),
            lista.Sum(c => c.SinCambios),
            lista.Sum(c => c.Sobrantes));
    }

    /// <summary>
    /// El detalle línea por línea. Se ordena por acción y después por objetivo para que lo que va a
    /// cambiar quede arriba: quien mira el preview antes de confirmar busca eso, no el inventario
    /// completo de lo que ya estaba bien.
    /// </summary>
    private static List<VacunacionMaterializacionDetalleDto> Detalle(
        Resolucion r, IReadOnlyDictionary<int, string> nombresVacuna)
    {
        string Nombre(int itemInventarioId) =>
            nombresVacuna.TryGetValue(itemInventarioId, out var n) ? n : $"Vacuna {itemInventarioId}";

        var filas = new List<VacunacionMaterializacionDetalleDto>();

        foreach (var alta in r.Plan.Faltantes)
            filas.Add(new VacunacionMaterializacionDetalleDto(
                "Crear", null, alta.Id, alta.ItemInventarioId, Nombre(alta.ItemInventarioId),
                alta.UnidadObjetivo ?? "", alta.ValorObjetivo, null));

        foreach (var upd in r.Plan.Actualizables)
            filas.Add(new VacunacionMaterializacionDetalleDto(
                "Actualizar", upd.CronogramaItemId, upd.Plantilla.Id, upd.Plantilla.ItemInventarioId,
                Nombre(upd.Plantilla.ItemInventarioId), upd.Plantilla.UnidadObjetivo ?? "", upd.Plantilla.ValorObjetivo,
                "Se alinea con el plan de la empresa."));

        foreach (var p in r.Plan.Preservados)
        {
            var item = r.ItemsPlantilla.TryGetValue(p.OrigenPlantillaItemId, out var i) ? i : default;
            filas.Add(new VacunacionMaterializacionDetalleDto(
                p.Motivo.ToString(), p.CronogramaItemId, p.OrigenPlantillaItemId, item.ItemInventarioId,
                Nombre(item.ItemInventarioId), item.UnidadObjetivo ?? "", item.ValorObjetivo,
                p.Motivo switch
                {
                    Calc.MotivoPreservado.YaAplicado => "Ya tiene aplicación registrada: no se toca.",
                    Calc.MotivoPreservado.Manual     => "Se cargó o se corrigió a mano: el plan no la pisa.",
                    _                                => null,
                }));
        }

        foreach (var s in r.Plan.Sobrantes)
            filas.Add(new VacunacionMaterializacionDetalleDto(
                "Sobrante", s.CronogramaItemId, s.OrigenPlantillaItemId, 0, "—", "", null,
                s.Motivo == Calc.MotivoSobrante.Duplicado
                    ? "Hay otra fila del lote para la misma vacuna del plan. Se informa, no se corrige solo."
                    : "Salió de una vacuna que ya no está en el plan. Queda como está: si sobra, se borra desde el cronograma."));

        return filas
            .OrderBy(f => f.Accion switch { "Crear" => 0, "Actualizar" => 1, "Sobrante" => 2, "YaAplicado" => 3, "Manual" => 4, _ => 5 })
            .ThenBy(f => f.ValorObjetivo ?? int.MaxValue)
            .ThenBy(f => f.CronogramaItemId ?? int.MaxValue)
            .ToList();
    }

    private static VacunacionMaterializacionLoteDto MapLote(
        Resolucion r, IReadOnlyDictionary<int, string> nombresVacuna, bool aplicado = false, string? error = null) =>
        new(r.Lote.LineaProductiva, r.Lote.LoteId, r.Lote.LoteNombre, r.Lote.GranjaId, r.Lote.GalponId,
            r.PlantillaId, r.PlantillaNombre, r.Motivo,
            Conteos(r.Plan), Detalle(r, nombresVacuna), aplicado, error);

    /// <summary>Nombres de las vacunas que aparecen en el informe, en una sola consulta.</summary>
    private async Task<Dictionary<int, string>> NombresDeVacunasAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var lista = ids.Where(i => i > 0).Distinct().ToList();
        if (lista.Count == 0) return [];

        return await _ctx.ItemInventario.AsNoTracking()
            .Where(v => lista.Contains(v.Id) && v.CompanyId == _currentUser.CompanyId)
            .ToDictionaryAsync(v => v.Id, v => v.Nombre, ct);
    }
}
