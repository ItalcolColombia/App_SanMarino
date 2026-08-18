// Vacunacion/Funciones/VacunacionMaterializadorService.Aplicar.cs
// ESCRITURA: lo único de W2 que toca datos de lotes vivos.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;
using Calc = ZooSanMarino.Application.Calculos.VacunacionMaterializadorCalculos;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionMaterializadorService
{
    /// <inheritdoc />
    public async Task<VacunacionMaterializacionLoteDto> AplicarLoteAsync(
        string lineaProductiva, int loteId, CancellationToken ct = default)
    {
        var (resolucion, nombres) = await ResolverUnLoteAsync(lineaProductiva, loteId, ct);
        await EscribirAsync(resolucion, ct);

        // El informe es el MISMO plan que devolvió la vista previa: lo que se vio antes de confirmar
        // es literalmente lo que se escribió.
        return MapLote(resolucion, nombres, aplicado: true);
    }

    /// <inheritdoc />
    public async Task<VacunacionMaterializacionMasivaDto> AplicarPlantillaAsync(int plantillaId, CancellationToken ct = default)
    {
        var (cabecera, resoluciones, nombres, evaluados) = await ResolverPlantillaAsync(plantillaId, ct);

        var informes = new List<VacunacionMaterializacionLoteDto>();
        foreach (var r in resoluciones)
        {
            try
            {
                await EscribirAsync(r, ct);
                informes.Add(MapLote(r, nombres, aplicado: true));
            }
            catch (Exception ex)
            {
                // Un lote que falla no puede dejar a los otros a medio materializar: se reporta con su
                // error y el recorrido sigue. El detalle viaja al usuario porque los motivos reales
                // (una vacuna que se sacó del catálogo) los corrige él, no el soporte.
                _logger.LogError(ex, "Materialización de la plantilla {PlantillaId} fallida en el lote {Linea} {LoteId}",
                    plantillaId, r.Lote.LineaProductiva, r.Lote.LoteId);
                informes.Add(MapLote(r, nombres, aplicado: false, error: ex.Message));
            }
        }

        return MapMasiva(cabecera.Id, cabecera.Nombre, cabecera.LineaProductiva, evaluados, informes);
    }

    /// <inheritdoc />
    public async Task<int> MaterializarAlCrearLoteAsync(string lineaProductiva, int loteId, CancellationToken ct = default)
    {
        try
        {
            var linea = ValidarLinea(lineaProductiva);

            // Sin chequeo de alcance a propósito: el usuario acaba de crear este lote, así que no hay
            // nada que ocultarle. Chequearlo sólo lograría que un usuario restringido se quedara sin
            // cronograma en un lote propio.
            var lote = await LoteAsync(linea, loteId, ct);
            if (lote is null) return 0;

            var resolucion = (await ResolverAsync(linea, [lote], null, ct)).SingleOrDefault();
            if (resolucion is null || resolucion.PlantillaId is null) return 0;

            return await EscribirAsync(resolucion, ct);
        }
        catch (Exception ex)
        {
            // Fail-soft, y es una decisión: el lote es el hecho operativo y el plan es derivado. Que no
            // se pueda copiar el cronograma no puede impedir que se cree el lote — el botón «aplicar a
            // los lotes» lo recupera después, y el aviso del cronograma lo hace visible mientras tanto.
            _logger.LogWarning(ex, "No se pudo materializar el plan de vacunación al crear el lote {Linea} {LoteId}",
                lineaProductiva, loteId);
            return 0;
        }
    }

    // ─── La escritura ─────────────────────────────────────────────────────────

    /// <summary>
    /// Ejecuta el plan de un lote: da de alta lo que falta y alinea lo que cambió. <b>No borra nada</b>
    /// —ni sobrantes, ni duplicados— y no toca una sola fila que el plan haya marcado como preservada.
    /// </summary>
    /// <returns>Filas escritas. <c>0</c> significa que el lote ya estaba al día.</returns>
    private async Task<int> EscribirAsync(Resolucion r, CancellationToken ct)
    {
        if (r.Plan.NoEscribeNada) return 0;

        await ValidarVacunasAsync(r, ct);

        var agregadas = new List<VacunacionCronogramaItem>();
        var modificadas = new List<VacunacionCronogramaItem>();

        // Si el llamador ya abrió una transacción —el enganche corre dentro del alta del lote— se usa
        // la suya: abrir una anidada tiraría, y de paso el cronograma tiene que caerse con el lote si
        // el alta se revierte.
        var propia = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            foreach (var alta in r.Plan.Faltantes)
            {
                var entidad = NuevoItem(r.Lote, alta);
                _ctx.VacunacionCronogramaItem.Add(entidad);
                agregadas.Add(entidad);
            }

            if (r.Plan.Actualizables.Count > 0)
            {
                var ids = r.Plan.Actualizables.Select(a => a.CronogramaItemId).ToList();
                var entidades = await _ctx.VacunacionCronogramaItem
                    .Where(x => ids.Contains(x.Id) && x.CompanyId == _currentUser.CompanyId)
                    .ToDictionaryAsync(x => x.Id, ct);

                foreach (var upd in r.Plan.Actualizables)
                {
                    // Desapareció entre el preview y el guardado: se saltea. La alternativa —recrearla—
                    // resucitaría algo que alguien acaba de borrar a mano.
                    if (!entidades.TryGetValue(upd.CronogramaItemId, out var entidad)) continue;

                    Alinear(entidad, upd.Plantilla);
                    modificadas.Add(entidad);
                }
            }

            var escritas = await _ctx.SaveChangesAsync(ct);
            if (propia is not null) await propia.CommitAsync(ct);
            return escritas;
        }
        catch
        {
            if (propia is not null) await propia.RollbackAsync(ct);

            // Se deshace SÓLO lo que tocó este lote. Limpiar el ChangeTracker entero sería más simple y
            // también descartaría el lote que el llamador acaba de crear y todavía no guardó.
            foreach (var e in agregadas) _ctx.Entry(e).State = EntityState.Detached;
            foreach (var e in modificadas) await _ctx.Entry(e).ReloadAsync(ct);
            throw;
        }
        finally
        {
            if (propia is not null) await propia.DisposeAsync();
        }
    }

    /// <summary>
    /// Fila nueva del cronograma a partir de un ítem del plan.
    ///
    /// <para>
    /// La ubicación (granja/núcleo/galpón) sale del <b>lote</b> y no del plan, igual que en el alta
    /// manual, y <c>FechaObjetivo</c> queda en <c>null</c> porque una plantilla no puede tener unidad
    /// <c>Fecha</c>: una fecha fija sería la misma para lotes encasetados en meses distintos.
    /// </para>
    /// </summary>
    private VacunacionCronogramaItem NuevoItem(LoteVivo lote, Calc.ItemPlantilla item)
    {
        var entidad = new VacunacionCronogramaItem
        {
            CompanyId = _currentUser.CompanyId,
            PaisId = _currentUser.PaisId,
            LineaProductiva = lote.LineaProductiva,
            GranjaId = lote.GranjaId,
            NucleoId = lote.NucleoId,
            GalponId = lote.GalponId,
            ItemInventarioId = item.ItemInventarioId,
            UnidadObjetivo = (item.UnidadObjetivo ?? "").Trim(),
            // El plan admite objetivo 0 (día del encaset: hay vacunas que van ese mismo día) y el
            // cronograma también, aunque su alta manual pida >= 1. Manda la regla del plan.
            ValorObjetivo = item.ValorObjetivo,
            FechaObjetivo = null,
            RangoDiasAntes = item.RangoDiasAntes,
            RangoDiasDespues = item.RangoDiasDespues,
            Orden = item.Orden,
            Activo = true,
            Notas = item.Notas,
            OrigenPlantillaItemId = item.Id,
            GeneradoAutomatico = true,
            CreatedByUserId = _currentUser.UserId,
            CreatedAt = DateTime.UtcNow,
        };

        switch (lote.LineaProductiva)
        {
            case "Levante": entidad.LotePosturaLevanteId = lote.LoteId; break;
            case "Produccion": entidad.LotePosturaProduccionId = lote.LoteId; break;
            default: entidad.LoteAveEngordeId = lote.LoteId; break;
        }

        return entidad;
    }

    /// <summary>
    /// Alinea una fila derivada con su ítem del plan. Se tocan exactamente los siete campos que el
    /// materializador copia: la ubicación, la línea y el lote no se re-sincronizan nunca.
    /// </summary>
    private void Alinear(VacunacionCronogramaItem entidad, Calc.ItemPlantilla item)
    {
        entidad.ItemInventarioId = item.ItemInventarioId;
        entidad.UnidadObjetivo = (item.UnidadObjetivo ?? "").Trim();
        entidad.ValorObjetivo = item.ValorObjetivo;
        entidad.FechaObjetivo = null;
        entidad.RangoDiasAntes = item.RangoDiasAntes;
        entidad.RangoDiasDespues = item.RangoDiasDespues;
        entidad.Orden = item.Orden;
        entidad.Notas = item.Notas;
        entidad.UpdatedByUserId = _currentUser.UserId;
        entidad.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Las vacunas que se van a escribir tienen que seguir estando en el catálogo de la empresa.
    ///
    /// <para>
    /// El FK impide que el id no exista, pero no que sea de otra empresa ni que el ítem se haya dado
    /// de baja desde que se armó el plan. Sin este chequeo el fallo llegaría como una violación de
    /// constraint —o, peor, no llegaría— en vez de un mensaje que diga qué corregir.
    /// </para>
    /// </summary>
    private async Task ValidarVacunasAsync(Resolucion r, CancellationToken ct)
    {
        var ids = r.Plan.Faltantes.Select(f => f.ItemInventarioId)
            .Concat(r.Plan.Actualizables.Select(a => a.Plantilla.ItemInventarioId))
            .Distinct()
            .ToList();
        if (ids.Count == 0) return;

        var vivas = await _ctx.ItemInventario.AsNoTracking()
            .Where(v => ids.Contains(v.Id) && v.CompanyId == _currentUser.CompanyId)
            .Select(v => v.Id)
            .ToListAsync(ct);

        var faltan = ids.Except(vivas).ToList();
        if (faltan.Count == 0) return;

        throw new InvalidOperationException(
            $"La plantilla \"{r.PlantillaNombre}\" usa {faltan.Count} vacuna(s) que ya no están en el catálogo de la " +
            $"empresa (ids {string.Join(", ", faltan)}). Corregí el plan y volvé a aplicarlo.");
    }
}
