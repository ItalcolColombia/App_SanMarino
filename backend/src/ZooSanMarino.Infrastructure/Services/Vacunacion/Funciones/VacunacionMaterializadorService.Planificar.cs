// Vacunacion/Funciones/VacunacionMaterializadorService.Planificar.cs
// SOLO LECTURA: resolver la plantilla de cada lote y calcular qué habría que escribirle.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using Calc = ZooSanMarino.Application.Calculos.VacunacionMaterializadorCalculos;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionMaterializadorService
{
    /// <summary>
    /// Resuelve la plantilla de cada lote y le calcula el plan de escritura. <b>No escribe nada.</b>
    ///
    /// <para>
    /// Trabaja por lotes en bloque —una consulta de plantillas, una de ítems y una de cronograma para
    /// todos— en vez de resolver lote por lote: el masivo de una empresa con cientos de lotes vivos
    /// sería, si no, un N+1 de tres consultas por lote.
    /// </para>
    /// </summary>
    /// <param name="soloPlantillaId">
    /// Si viene, se quedan sólo los lotes a los que hoy les toca <b>esa</b> plantilla. Es la diferencia
    /// entre «el plan de este lote» y «los lotes de este plan», y la resolución es la misma en los dos
    /// casos: nunca se le impone una plantilla a un lote que no la resuelve.
    /// </param>
    private async Task<List<Resolucion>> ResolverAsync(
        string linea, IReadOnlyList<LoteVivo> lotes, int? soloPlantillaId, CancellationToken ct)
    {
        if (lotes.Count == 0) return [];

        // DateOnly se arma en memoria: DateOnly.FromDateTime dentro de la proyección compila y pasa
        // los tests, pero Npgsql no lo traduce sobre una columna 'date' y revienta en runtime.
        var cabeceras = await PlantillasDeLaEmpresa()
            .Where(p => p.LineaProductiva == linea)
            .Select(p => new { p.Id, p.Nombre, p.LineaProductiva, p.Raza, p.VigenteDesde, p.Activa })
            .ToListAsync(ct);

        var candidatas = cabeceras
            .Select(p => new VacunacionPlantillaCalculos.Candidata(
                p.Id, p.LineaProductiva, p.Raza,
                p.VigenteDesde == null ? null : DateOnly.FromDateTime(p.VigenteDesde.Value),
                p.Activa))
            .ToList();

        var elegidas = new Dictionary<int, (int? Id, string? Nombre, string Motivo)>();
        foreach (var lote in lotes)
        {
            var encaset = lote.FechaEncaset is { } f ? DateOnly.FromDateTime(f) : (DateOnly?)null;
            var elegida = VacunacionPlantillaCalculos.ResolverEfectiva(candidatas, linea, lote.Raza, encaset);
            var nombre = elegida is { } e ? cabeceras.FirstOrDefault(p => p.Id == e.Id)?.Nombre : null;
            var motivo = VacunacionPlantillaCalculos.DescribirResolucion(
                candidatas, linea, lote.Raza, encaset, elegida?.Id, nombre);

            elegidas[lote.LoteId] = (elegida?.Id, nombre, motivo);
        }

        var alcanzados = soloPlantillaId is { } filtro
            ? lotes.Where(l => elegidas[l.LoteId].Id == filtro).ToList()
            : lotes.ToList();

        if (alcanzados.Count == 0) return [];

        var plantillaIds = alcanzados.Select(l => elegidas[l.LoteId].Id).OfType<int>().Distinct().ToList();
        var itemsPorPlantilla = await ItemsDePlantillasAsync(plantillaIds, ct);
        var cronograma = await CronogramaAsync(linea, alcanzados.Select(l => l.LoteId).ToList(), ct);

        return alcanzados.Select(lote =>
        {
            var (plantillaId, plantillaNombre, motivo) = elegidas[lote.LoteId];
            var items = plantillaId is { } pid && itemsPorPlantilla.TryGetValue(pid, out var lista)
                ? lista
                : [];

            return new Resolucion(
                lote, plantillaId, plantillaNombre, motivo,
                Calc.Planificar(items, cronograma[lote.LoteId]),
                items.ToDictionary(i => i.Id));
        }).ToList();
    }

    /// <summary>Plantillas VIVAS de la empresa activa. Misma base que <c>VacunacionPlantillaService</c>.</summary>
    private IQueryable<Domain.Entities.VacunacionPlanPlantilla> PlantillasDeLaEmpresa() =>
        _ctx.VacunacionPlanPlantilla.AsNoTracking()
            .Where(p => p.CompanyId == _currentUser.CompanyId && p.DeletedAt == null);

    /// <summary>
    /// Ítems vivos de varias plantillas, en el <b>mismo orden</b> en que los muestra la pantalla de
    /// plantillas (<c>Orden, ValorObjetivo, Id</c>). Que coincidan no es cosmético: es lo que hace que
    /// el <c>orden</c> que se copia al cronograma sea el que el usuario vio al armar el plan.
    /// </summary>
    private async Task<Dictionary<int, List<Calc.ItemPlantilla>>> ItemsDePlantillasAsync(
        IReadOnlyCollection<int> plantillaIds, CancellationToken ct)
    {
        if (plantillaIds.Count == 0) return [];

        var companyId = _currentUser.CompanyId;

        var items = await _ctx.VacunacionPlanPlantillaItem.AsNoTracking()
            .Where(i => plantillaIds.Contains(i.PlantillaId) && i.CompanyId == companyId && i.DeletedAt == null)
            .OrderBy(i => i.Orden).ThenBy(i => i.ValorObjetivo).ThenBy(i => i.Id)
            .Select(i => new { i.PlantillaId, Item = new Calc.ItemPlantilla(
                i.Id, i.ItemInventarioId, i.UnidadObjetivo, i.ValorObjetivo,
                i.RangoDiasAntes, i.RangoDiasDespues, i.Orden, i.Notas) })
            .ToListAsync(ct);

        return items.GroupBy(x => x.PlantillaId).ToDictionary(g => g.Key, g => g.Select(x => x.Item).ToList());
    }

    /// <summary>
    /// Todas las vacunas que aparecen en un conjunto de resoluciones: las que vienen del plan y las
    /// que ya están en el cronograma.
    /// </summary>
    private static IEnumerable<int> VacunasDe(IEnumerable<Resolucion> resoluciones) =>
        resoluciones.SelectMany(r => r.ItemsPlantilla.Values.Select(i => i.ItemInventarioId));

    // ─── API pública de lectura ───────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<VacunacionMaterializacionLoteDto> PreviewLoteAsync(
        string lineaProductiva, int loteId, CancellationToken ct = default)
    {
        var (resolucion, nombres) = await ResolverUnLoteAsync(lineaProductiva, loteId, ct);
        return MapLote(resolucion, nombres);
    }

    /// <summary>
    /// Resuelve un lote puntual con todo lo que hace falta para informarlo o aplicarlo. Lanza —y no
    /// devuelve vacío— cuando el lote no existe, es de otra empresa o está fuera del alcance del
    /// usuario: son tres cosas distintas de «este lote no tiene plan».
    /// </summary>
    private async Task<(Resolucion Resolucion, Dictionary<int, string> Nombres)> ResolverUnLoteAsync(
        string lineaProductiva, int loteId, CancellationToken ct)
    {
        var linea = ValidarLinea(lineaProductiva);

        var lote = await LoteAsync(linea, loteId, ct)
            ?? throw new InvalidOperationException($"Lote {linea} {loteId} no existe o no pertenece a la empresa activa.");

        if (!await PermiteAsync(lote))
            throw new InvalidOperationException($"El lote {lote.LoteNombre} está fuera de su alcance de ubicación.");

        var resolucion = (await ResolverAsync(linea, [lote], null, ct)).Single();
        var nombres = await NombresDeVacunasAsync(VacunasDe([resolucion]), ct);
        return (resolucion, nombres);
    }

    /// <inheritdoc />
    public async Task<VacunacionMaterializacionMasivaDto> PreviewPlantillaAsync(int plantillaId, CancellationToken ct = default)
    {
        var (cabecera, resoluciones, nombres, evaluados) = await ResolverPlantillaAsync(plantillaId, ct);
        return MapMasiva(cabecera.Id, cabecera.Nombre, cabecera.LineaProductiva, evaluados,
            resoluciones.Select(r => MapLote(r, nombres)).ToList());
    }

    /// <summary>Cabecera de la plantilla + los lotes vivos a los que hoy les toca, ya planificados.</summary>
    private async Task<(PlantillaCabecera Cabecera, List<Resolucion> Resoluciones, Dictionary<int, string> Nombres, int Evaluados)>
        ResolverPlantillaAsync(int plantillaId, CancellationToken ct)
    {
        var cabecera = await PlantillasDeLaEmpresa()
            .Where(p => p.Id == plantillaId)
            .Select(p => new PlantillaCabecera(p.Id, p.Nombre, p.LineaProductiva, p.Activa))
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Plantilla {plantillaId} no existe o no pertenece a la empresa activa.");

        if (!cabecera.Activa)
            throw new InvalidOperationException(
                $"La plantilla \"{cabecera.Nombre}\" está inactiva: una plantilla apagada no le corresponde a ningún lote. " +
                "Activala antes de aplicarla.");

        // Sólo lotes abiertos: aplicarle el plan sanitario a un ciclo cerrado programaría vacunas para
        // aves que ya no están. El filtro va DENTRO de la consulta (sobre la entidad), no sobre el
        // record proyectado: eso último no lo traduce EF.
        var vivos = await LotesDeLaEmpresa(cabecera.LineaProductiva, soloAbiertos: true).ToListAsync(ct);

        var alcanzables = new List<LoteVivo>();
        foreach (var lote in vivos)
            if (await PermiteAsync(lote)) alcanzables.Add(lote);

        var resoluciones = await ResolverAsync(cabecera.LineaProductiva, alcanzables, plantillaId, ct);
        var nombres = await NombresDeVacunasAsync(VacunasDe(resoluciones), ct);

        return (cabecera, resoluciones, nombres, alcanzables.Count);
    }

    private sealed record PlantillaCabecera(int Id, string Nombre, string LineaProductiva, bool Activa);

    private static VacunacionMaterializacionMasivaDto MapMasiva(
        int plantillaId, string nombre, string linea, int evaluados, List<VacunacionMaterializacionLoteDto> lotes) =>
        new(plantillaId, nombre, linea,
            evaluados,
            lotes.Count,
            lotes.Count(l => l.Conteos.EscribeAlgo),
            Sumar(lotes.Select(l => l.Conteos)),
            lotes,
            lotes.Count(l => l.Error is not null));
}
