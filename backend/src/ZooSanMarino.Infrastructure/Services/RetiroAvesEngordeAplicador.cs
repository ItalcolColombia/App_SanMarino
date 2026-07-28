// src/ZooSanMarino.Infrastructure/Services/RetiroAvesEngordeAplicador.cs
// Descuento de AVES del lote pollo engorde por las bajas del seguimiento diario (mortalidad +
// selección + error de sexaje). Vive acá, y no dentro de un service, porque los DOS caminos de
// captura lo necesitan idénticos: el formulario diario (SeguimientoAvesEngordeEcuadorService, que
// atiende /api/SeguimientoAvesEngordeEcuador) y la carga masiva (SeguimientoAvesEngordeService, que
// usa MigracionService). Un lote tiene que quedar igual sin importar por dónde se cargó el día.
//
// Antes de esto el alta/edición de un seguimiento de ENGORDE llamaba a
// MovimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync, que resuelve el lote contra la tabla
// de POSTURA (`lotes`) usando el id del lote de ENGORDE. Como las dos tablas tienen secuencias
// independientes ese id puede existir en ambas, así que el retiro terminaba tocando el inventario de
// un lote de postura ajeno (o, con suerte, no hacía nada). Acá el retiro es nativo de engorde.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using static ZooSanMarino.Application.Calculos.RetiroAvesEngordeCalculos;

namespace ZooSanMarino.Infrastructure.Services;

internal static class RetiroAvesEngordeAplicador
{
    /// <summary>Evento del histórico unificado que representa las bajas de un día de seguimiento.</summary>
    public const string TipoEventoBaja = "BAJA_SEGUIMIENTO";

    /// <summary>Tabla de origen del evento (con el id del seguimiento forma la clave única del histórico).</summary>
    public const string OrigenTabla = "seguimiento_diario_aves_engorde";

    /// <summary>
    /// Sincroniza las aves del lote con las bajas de UN registro de seguimiento. Idempotente por
    /// registro: el maestro <c>lote_ave_engorde</c> se mueve por el DELTA (nuevas − viejas) y en
    /// <c>lote_registro_historico_unificado</c> existe a lo sumo UNA fila por seguimiento (clave única
    /// <c>origen_tabla + origen_id</c>) que siempre refleja el total vigente de ese día.
    /// <list type="bullet">
    /// <item>Alta → viejas = 0.</item>
    /// <item>Edición → delta, que puede ser negativo y devolver aves.</item>
    /// <item>Borrado → nuevas = 0: devuelve todo y anula la fila del histórico.</item>
    /// </list>
    /// El reparto (mixtas vs. por sexo) lo decide <c>RetiroAvesEngordeCalculos</c> según los DATOS del
    /// lote, así que la plantilla mixta de Panamá — que manda el total del día en las columnas "H" —
    /// descuenta de <c>mixtas</c> sin que el importador tenga que saber nada del lote.
    /// </summary>
    public static async Task SincronizarAsync(
        ZooSanMarinoContext ctx, int companyId,
        int loteAveEngordeId, long seguimientoId, DateTime fecha,
        int bajasHembrasViejas, int bajasMachosViejas,
        int bajasHembrasNuevas, int bajasMachosNuevas)
    {
        var deltaH = bajasHembrasNuevas - bajasHembrasViejas;
        var deltaM = bajasMachosNuevas - bajasMachosViejas;

        var lote = await ctx.LoteAveEngorde
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == loteAveEngordeId
                                    && l.CompanyId == companyId
                                    && l.DeletedAt == null);
        if (lote is null) return;

        // Guarda anti DOBLE descuento del saldo mostrado en reportes: fn_seguimiento_diario_engorde
        // calcula las aves iniciales desde `aves_encasetadas`, EXCEPTO cuando esa columna es 0, donde
        // cae al maestro (hembras_l + machos_l + mixtas) y después le resta las bajas del seguimiento.
        // En ese caso mover el maestro haría que las bajas se descuenten dos veces. Hoy no hay ningún
        // lote así (los 134 de la base tienen aves_encasetadas > 0), pero si apareciera preferimos no
        // descontar antes que reportar un saldo mentiroso.
        if ((lote.AvesEncasetadas ?? 0) <= 0) return;

        var maestro = new MaestroAves(lote.HembrasL ?? 0, lote.MachosL ?? 0, lote.Mixtas ?? 0);
        var res = AplicarDelta(maestro, deltaH, deltaM);

        if (!res.SinEfecto)
        {
            lote.HembrasL = res.Maestro.Hembras;
            lote.MachosL = res.Maestro.Machos;
            lote.Mixtas = res.Maestro.Mixtas;
        }

        // La fila del histórico refleja el TOTAL vigente del día, repartido con el mismo criterio con
        // el que se movió el maestro (por eso se usa el maestro PREVIO al delta).
        var totalVigente = Repartir(maestro, bajasHembrasNuevas, bajasMachosNuevas);
        await UpsertHistoricoAsync(ctx, lote, seguimientoId, fecha, totalVigente, res.Insuficiente);

        await ctx.SaveChangesAsync();
    }

    /// <summary>
    /// Sincroniza con el maestro las bajas de los días 1-7, los que genera el TRIGGER del cruce de
    /// reproductora.
    /// <para>
    /// Esos días entran por SQL directo (<c>fn_cruce_reproductora_a_engorde</c>), sin pasar por el
    /// service, así que su mortalidad nunca llegaba a <c>hembras_l/machos_l</c>: el maestro quedaba por
    /// encima del real —en el lote testigo, 381 aves— y el sistema habría dejado despachar aves ya
    /// muertas. El reporte diario nunca tuvo el problema porque calcula
    /// <c>aves_encasetadas − bajas</c> en vez de leer el maestro.
    /// </para>
    /// <para>
    /// Idempotente por la fila del histórico unificado: un día ya sincronizado tiene su
    /// <c>BAJA_SEGUIMIENTO</c> viva y se saltea. El cruce BORRA y RECREA sus registros cada vez que
    /// cambia la reproductora, así que las filas que quedan apuntando a un seguimiento inexistente se
    /// revierten (devuelven las aves) antes de aplicar las nuevas.
    /// </para>
    /// </summary>
    public static async Task SincronizarCruceAsync(ZooSanMarinoContext ctx, int companyId, int loteAveEngordeId)
    {
        var lote = await ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == loteAveEngordeId
                                    && l.CompanyId == companyId
                                    && l.DeletedAt == null);
        if (lote is null) return;

        var vivos = await ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId && s.OrigenCruce)
            .Select(s => new
            {
                s.Id, s.Fecha,
                s.MortalidadHembras, s.MortalidadMachos,
                s.SelH, s.SelM, s.ErrorSexajeHembras, s.ErrorSexajeMachos
            })
            .ToListAsync();
        var idsVivos = vivos.Select(s => (int)s.Id).ToHashSet();

        var aplicadas = await ctx.LoteRegistroHistoricoUnificados
            .Where(h => h.OrigenTabla == OrigenTabla
                     && h.TipoEvento == TipoEventoBaja
                     && !h.Anulado
                     && h.LoteAveEngordeId == loteAveEngordeId)
            .ToListAsync();

        // 1) Filas cuyo seguimiento ya no existe (el cruce lo borró al regenerarse): devolver las aves.
        var idsSeguimientosDelLote = await ctx.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId)
            .Select(s => (int)s.Id)
            .ToListAsync();
        var existentes = idsSeguimientosDelLote.ToHashSet();

        foreach (var h in aplicadas.Where(x => !existentes.Contains(x.OrigenId)))
        {
            await SincronizarAsync(
                ctx, companyId, loteAveEngordeId, h.OrigenId, h.FechaOperacion,
                bajasHembrasViejas: h.CantidadHembras ?? 0,
                bajasMachosViejas: (h.CantidadMachos ?? 0) + (h.CantidadMixtas ?? 0),
                bajasHembrasNuevas: 0, bajasMachosNuevas: 0);
        }

        // 2) Días de cruce todavía sin aplicar: descontarlos.
        var yaAplicados = aplicadas
            .Where(x => idsVivos.Contains(x.OrigenId))
            .Select(x => x.OrigenId)
            .ToHashSet();

        foreach (var s in vivos.Where(x => !yaAplicados.Contains((int)x.Id)))
        {
            var (bajasH, bajasM) = BajasDelDia(
                s.MortalidadHembras ?? 0, s.SelH ?? 0, s.ErrorSexajeHembras ?? 0,
                s.MortalidadMachos ?? 0, s.SelM ?? 0, s.ErrorSexajeMachos ?? 0);
            if (bajasH == 0 && bajasM == 0) continue;

            await SincronizarAsync(
                ctx, companyId, loteAveEngordeId, s.Id, s.Fecha,
                bajasHembrasViejas: 0, bajasMachosViejas: 0,
                bajasHembrasNuevas: bajasH, bajasMachosNuevas: bajasM);
        }
    }

    /// <summary>
    /// Crea, actualiza o anula la fila del histórico unificado del seguimiento. No hace SaveChanges:
    /// lo hace <see cref="SincronizarAsync"/> junto con el maestro, para que ambos efectos viajen en
    /// la misma unidad de trabajo.
    /// </summary>
    private static async Task UpsertHistoricoAsync(
        ZooSanMarinoContext ctx, LoteAveEngorde lote, long seguimientoId, DateTime fecha,
        RetiroAves total, bool insuficiente)
    {
        // origen_id de la tabla es INTEGER; los ids de seguimiento entran de sobra, pero si algún día
        // no entraran preferimos quedarnos sin la traza antes que reventar el alta del registro.
        if (seguimientoId is < int.MinValue or > int.MaxValue) return;
        var origenId = (int)seguimientoId;

        var fila = await ctx.LoteRegistroHistoricoUnificados
            .SingleOrDefaultAsync(h => h.OrigenTabla == OrigenTabla && h.OrigenId == origenId);

        if (total.EsVacio)
        {
            if (fila is not null) fila.Anulado = true; // el día quedó sin bajas (o se borró el registro)
            return;
        }

        var referencia = $"Bajas seguimiento aves engorde #{origenId} {fecha:yyyy-MM-dd}"
                       + (insuficiente ? " (aves insuficientes: el maestro quedó en 0)" : string.Empty);

        if (fila is null)
        {
            ctx.LoteRegistroHistoricoUnificados.Add(new LoteRegistroHistoricoUnificado
            {
                CompanyId = lote.CompanyId,
                LoteAveEngordeId = lote.LoteAveEngordeId,
                FarmId = lote.GranjaId,
                NucleoId = lote.NucleoId,
                GalponId = lote.GalponId,
                FechaOperacion = fecha.Date,
                TipoEvento = TipoEventoBaja,
                OrigenTabla = OrigenTabla,
                OrigenId = origenId,
                CantidadHembras = total.Hembras,
                CantidadMachos = total.Machos,
                CantidadMixtas = total.Mixtas,
                Referencia = referencia,
                Anulado = false
            });
            return;
        }

        fila.LoteAveEngordeId = lote.LoteAveEngordeId;
        fila.FarmId = lote.GranjaId;
        fila.NucleoId = lote.NucleoId;
        fila.GalponId = lote.GalponId;
        fila.FechaOperacion = fecha.Date;
        fila.CantidadHembras = total.Hembras;
        fila.CantidadMachos = total.Machos;
        fila.CantidadMixtas = total.Mixtas;
        fila.Referencia = referencia;
        fila.Anulado = false;
    }
}
