// Vacunacion/Funciones/VacunacionMaterializadorService.Lotes.cs
// SOLO LECTURA: qué lotes existen y qué tiene hoy su cronograma.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Infrastructure.Services;

public partial class VacunacionMaterializadorService
{
    /// <summary>
    /// Lotes de una línea, de la <b>empresa activa</b> y no borrados. Misma base que
    /// <c>fn_vacunacion_filter_data</c>, para que el materializador no vea lotes que el módulo no ve.
    /// </summary>
    /// <param name="loteId">Un lote puntual, o <c>null</c> para todos.</param>
    /// <param name="soloAbiertos">
    /// El masivo deja afuera lo cerrado —programarle vacunas a un ciclo cerrado sería programárselas a
    /// aves que ya no están—; el botón de un lote puntual no filtra, porque ahí el usuario lo nombró.
    /// </param>
    /// <remarks>
    /// ⚠️ Los dos filtros se aplican sobre la ENTIDAD, antes del <c>Select</c>. Filtrarlos después
    /// sobre <see cref="LoteVivo"/> compila igual y revienta en runtime: EF no traduce un
    /// <c>Where</c> sobre las propiedades de un record proyectado. Se descubrió así, con el smoke.
    /// </remarks>
    private IQueryable<LoteVivo> LotesDeLaEmpresa(string linea, int? loteId = null, bool soloAbiertos = false)
    {
        var companyId = _currentUser.CompanyId;

        switch (linea)
        {
            case "Levante":
            {
                var q = _ctx.LotePosturaLevante.AsNoTracking()
                    .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaLevanteId != null);
                if (loteId is { } id) q = q.Where(l => l.LotePosturaLevanteId == id);
                if (soloAbiertos) q = q.Where(l => l.EstadoCierre == null || l.EstadoCierre != Cerrado);
                return q.Select(l => new LoteVivo(
                    "Levante", l.LotePosturaLevanteId!.Value, l.LoteNombre, l.Raza, l.FechaEncaset,
                    l.GranjaId, l.NucleoId, l.GalponId, l.LoteId, l.EstadoCierre == Cerrado));
            }

            case "Produccion":
            {
                var q = _ctx.LotePosturaProduccion.AsNoTracking()
                    .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LotePosturaProduccionId != null);
                if (loteId is { } id) q = q.Where(l => l.LotePosturaProduccionId == id);
                if (soloAbiertos) q = q.Where(l => l.EstadoCierre == null || l.EstadoCierre != Cerrado);
                return q.Select(l => new LoteVivo(
                    "Produccion", l.LotePosturaProduccionId!.Value, l.LoteNombre, l.Raza, l.FechaEncaset,
                    l.GranjaId, l.NucleoId, l.GalponId, l.LoteId, l.EstadoCierre == Cerrado));
            }

            default:
            {
                var q = _ctx.LoteAveEngorde.AsNoTracking()
                    .Where(l => l.CompanyId == companyId && l.DeletedAt == null && l.LoteAveEngordeId != null);
                if (loteId is { } id) q = q.Where(l => l.LoteAveEngordeId == id);
                if (soloAbiertos) q = q.Where(l => l.EstadoOperativoLote != Cerrado);
                return q.Select(l => new LoteVivo(
                    "Engorde", l.LoteAveEngordeId!.Value, l.LoteNombre, l.Raza, l.FechaEncaset,
                    l.GranjaId, l.NucleoId, l.GalponId, null, l.EstadoOperativoLote == Cerrado));
            }
        }
    }

    /// <summary>
    /// Único marcador de cierre que se compara, y siempre por <b>desigualdad</b>.
    ///
    /// <para>
    /// El vocabulario del dato está partido: <c>LoteService</c> escribe <c>'Abierto'</c> y
    /// <c>LotePosturaLevanteService</c> escribe <c>'Abierta'</c>. Filtrar por <c>= 'Abierto'</c>
    /// saltearía en silencio a todos los lotes creados por el segundo camino, y el síntoma sería el
    /// peor posible: el masivo diciendo «listo» sobre la mitad de los lotes.
    /// </para>
    /// </summary>
    private const string Cerrado = "Cerrado";

    /// <summary>Un lote puntual de la empresa activa, o <c>null</c> si no existe o es de otra empresa.</summary>
    private Task<LoteVivo?> LoteAsync(string linea, int loteId, CancellationToken ct) =>
        LotesDeLaEmpresa(linea, loteId).FirstOrDefaultAsync(ct)!;

    /// <summary>
    /// Filas del cronograma que hoy tienen esos lotes, con su <c>TieneRegistro</c> resuelto en la
    /// misma consulta. Devuelve <c>(loteId, fila)</c> porque la fila no dice sola de qué lote es: el
    /// id vive en uno de tres FK excluyentes.
    /// </summary>
    /// <remarks>
    /// <c>TieneRegistro</c> usa el mismo criterio que el guard de <c>VacunacionRegistroService</c>
    /// —registro presente y con estado distinto de <c>Pendiente</c>—, para que «ya aplicado» signifique
    /// una sola cosa en todo el módulo.
    /// </remarks>
    private async Task<ILookup<int, VacunacionMaterializadorCalculos.ItemCronograma>> CronogramaAsync(
        string linea, IReadOnlyCollection<int> loteIds, CancellationToken ct)
    {
        if (loteIds.Count == 0) return Enumerable.Empty<(int, VacunacionMaterializadorCalculos.ItemCronograma)>()
            .ToLookup(p => p.Item1, p => p.Item2);

        var companyId = _currentUser.CompanyId;
        var ids = loteIds.Select(i => (int?)i).ToList();

        var q = _ctx.VacunacionCronogramaItem.AsNoTracking()
            .Where(x => x.CompanyId == companyId && x.DeletedAt == null && x.LineaProductiva == linea);

        q = linea switch
        {
            "Levante"    => q.Where(x => ids.Contains(x.LotePosturaLevanteId)),
            "Produccion" => q.Where(x => ids.Contains(x.LotePosturaProduccionId)),
            _            => q.Where(x => ids.Contains(x.LoteAveEngordeId)),
        };

        var filas = await q
            .Select(x => new
            {
                LoteId = x.LotePosturaLevanteId ?? x.LotePosturaProduccionId ?? x.LoteAveEngordeId ?? 0,
                Item = new VacunacionMaterializadorCalculos.ItemCronograma(
                    x.Id, x.OrigenPlantillaItemId, x.GeneradoAutomatico,
                    x.RegistroAplicacion != null && x.RegistroAplicacion.Estado != EstadoPendiente,
                    x.ItemInventarioId, x.UnidadObjetivo, x.ValorObjetivo,
                    x.RangoDiasAntes, x.RangoDiasDespues, x.Orden, x.Notas),
            })
            .ToListAsync(ct);

        return filas.ToLookup(f => f.LoteId, f => f.Item);
    }

    /// <summary>Espeja <c>VacunacionCalculos.EstadoPendiente</c>; acá como constante para que EF lo traduzca.</summary>
    private const string EstadoPendiente = "Pendiente";
}
