// file: src/ZooSanMarino.Infrastructure/Services/Funciones/LoteService.AjusteEncasetamiento.cs
// Corrección del encasetamiento de un lote de POSTURA que ya tiene seguimiento cargado: el caso del
// operario que digita mal las aves al crear el lote y lo descubre semanas después.
//
// En postura `lotes.hembras_l` / `machos_l` SÍ son el encasetamiento (al revés que en engorde, donde
// esas columnas son el saldo vivo), así que la edición de esos dos campos ya era correcta. Lo que
// faltaba era la CASCADA: la corrección se quedaba en `lotes` y no llegaba a las otras copias.
//
//   ✅ lote_postura_levante  → lo resuelve el trigger `trg_lotes_sync_lote_postura_levante`, que
//      espeja `aves_*_inicial` y corre `aves_*_actual` por el delta (migración 20260806074742).
//      NO se duplica acá: dos implementaciones del mismo número es lo que este repositorio ya pagó
//      caro con el saldo de alimento.
//   ❌ lote_etapa_levante    → sólo se escribía en el alta, y `GetMortalidadResumenAsync` la prefiere
//      sobre `lotes.hembras_l`: un lote corregido seguía reportando la base vieja.
//   ❌ lote_postura_produccion → nunca se tocaba, así que un lote que ya pasó a producción conservaba
//      para siempre el error de digitación en su base (`fn_seguimiento_diario_produccion` la lee de
//      `aves_h_inicial`).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class LoteService
{
    /// <summary>Discriminador de <c>seguimiento_diario</c> para la etapa de levante.</summary>
    private const string TipoSeguimientoLevante = "levante";

    /// <summary>
    /// Escribe el encasetamiento del lote y propaga la corrección, por DELTA, a las copias que la
    /// edición dejaba desactualizadas.
    ///
    /// <para>
    /// <b>Gate al restar.</b> Bajar la base por debajo de lo que el lote ya consumió dejaría días del
    /// seguimiento en negativo; en ese caso se rechaza entero, nombrando el día y las aves que
    /// faltan. Sumar nunca se bloquea: sólo puede mejorar la serie, y exigirle el diagnóstico dejaría
    /// sin arreglo posible justamente a los lotes que ya cerraron en rojo por otra causa.
    /// </para>
    ///
    /// <para>
    /// Delta cero ⇒ escribe los campos y no toca nada más: editar el técnico o la regional no puede
    /// mover un solo número de aves.
    /// </para>
    /// </summary>
    private async Task AplicarAjusteEncasetamientoAsync(Lote ent, UpdateLoteDto dto)
    {
        var entry = _ctx.Entry(ent);

        // Base VIGENTE = la que está en la BD, no la que ya haya quedado en la entidad: el delta se
        // mide contra lo persistido.
        var vigente = new RetiroAvesEngordeCalculos.MaestroAves(
            entry.Property(l => l.HembrasL).OriginalValue ?? 0,
            entry.Property(l => l.MachosL).OriginalValue ?? 0,
            0);
        var propuesto = new RetiroAvesEngordeCalculos.MaestroAves(dto.HembrasL ?? 0, dto.MachosL ?? 0, 0);

        var delta = AjusteEncasetamientoCalculos.Calcular(vigente, propuesto);

        // El gate va ANTES de escribir, no después: acá los campos se asignan aunque el delta sea
        // cero (a diferencia de engorde, que hace return temprano), así que rechazar más abajo
        // dejaría la base ya pisada en la entidad rastreada.
        if (!CorreccionAvesLoteAutorizacionCalculos.PuedeAplicar(
                !AjusteEncasetamientoCalculos.SinCambio(delta), _current.Permissions))
            throw new UnauthorizedAccessException(CorreccionAvesLoteAutorizacionCalculos.MensajeSinPermiso);

        ent.HembrasL = dto.HembrasL;
        ent.MachosL = dto.MachosL;

        if (AjusteEncasetamientoCalculos.SinCambio(delta)) return;

        var loteId = ent.LoteId ?? 0;
        if (loteId <= 0) return;

        if (delta.Total < 0)
        {
            var diagnostico = AjusteEncasetamientoCalculos.Diagnosticar(
                propuesto.Total,
                (dto.MortCajaH ?? 0) + (dto.MortCajaM ?? 0),
                await CargarSerieCicloAsync(loteId));

            if (!diagnostico.Compatible)
                throw new InvalidOperationException(
                    AjusteEncasetamientoCalculos.MensajeIncompatible(diagnostico, propuesto.Total));
        }

        await PropagarEtapaLevanteAsync(loteId, propuesto);
        await PropagarProduccionAsync(loteId, delta);
    }

    /// <summary>
    /// Bajas por día de TODO el ciclo de vida del lote: levante (<c>seguimiento_diario_levante</c>)
    /// <b>y</b> producción (<c>seguimiento_diario_produccion</c>), en ambos casos mortalidad +
    /// selección + error de sexaje de los dos sexos.
    ///
    /// <para>
    /// <b>Las dos etapas o ninguna.</b> El encasetamiento es la base de la vida entera del lote: un
    /// lote que ya pasó a producción sigue consumiendo de esas mismas aves. Medir sólo el levante
    /// dejaba pasar el caso que motiva el gate — probado en el lote 13 (K345A): levante consumió 739
    /// aves y producción 2.282 más, así que bajar la base a 1.232 pasaba el filtro y hundía
    /// <c>lote_postura_produccion.aves_h_inicial</c> de 7.597 a 0 por clamp, en silencio.
    /// </para>
    ///
    /// <para>
    /// Se suman TODAS las filas de levante, incluidas las marcadas como traslado: una fila puede ser
    /// mixta (traslado en sus columnas dedicadas y mortalidad manual en las suyas), mismo criterio
    /// que <c>GetMortalidadResumenAsync</c>.
    /// </para>
    /// </summary>
    private async Task<List<AjusteEncasetamientoCalculos.MovimientoDia>> CargarSerieCicloAsync(int loteId)
    {
        var loteIdStr = loteId.ToString();

        var levante = await _ctx.SeguimientoDiario.AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoSeguimientoLevante && s.LoteId == loteIdStr)
            .GroupBy(s => s.Fecha.Date)
            .Select(g => new
            {
                Fecha = g.Key,
                Total = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0)
                                 + (s.SelH ?? 0) + (s.SelM ?? 0)
                                 + (s.ErrorSexajeHembras ?? 0) + (s.ErrorSexajeMachos ?? 0))
            })
            .ToListAsync();

        var produccion = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .GroupBy(s => s.Fecha.Date)
            .Select(g => new
            {
                Fecha = g.Key,
                Total = g.Sum(s => s.MortalidadH + s.MortalidadM + s.SelH + s.SelM
                                 + s.ErrorSexajeHembras + s.ErrorSexajeMachos)
            })
            .ToListAsync();

        return levante.Concat(produccion)
            .GroupBy(x => x.Fecha)
            .Select(g => new AjusteEncasetamientoCalculos.MovimientoDia(g.Key, g.Sum(x => x.Total), 0))
            .OrderBy(m => m.Fecha)
            .ToList();
    }

    /// <summary>
    /// Alinea el historial de la etapa levante con el encasetamiento corregido. Es un REEMPLAZO, no
    /// un delta: la fila guarda «aves con que inicia el lote», el mismo número que
    /// <c>lotes.hembras_l</c>, y <c>GetMortalidadResumenAsync</c> la prefiere como base.
    /// </summary>
    private async Task PropagarEtapaLevanteAsync(int loteId, RetiroAvesEngordeCalculos.MaestroAves inicial)
    {
        var etapa = await _ctx.LoteEtapaLevante.FirstOrDefaultAsync(e => e.LoteId == loteId);
        if (etapa is null) return;

        etapa.AvesInicioHembras = inicial.Hembras;
        etapa.AvesInicioMachos = inicial.Machos;
        etapa.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Corre la base de producción por el delta del encasetamiento. Es DELTA y no reemplazo porque
    /// producción arranca con las aves que sobrevivieron al levante, no con las encasetadas: el lote
    /// 13 entra a producción con 7.597 hembras sobre 7.999 encasetadas. Corregir el encasetamiento en
    /// +500 significa que también habrían llegado 500 más a producción; pisar la base con 8.499
    /// borraría las 402 bajas del levante.
    /// <para>
    /// <c>aves_h_actual</c> se corre igual aunque sea una caché que
    /// <c>ProduccionService.Consultas</c> reescribe desde <c>fn_seguimiento_diario_produccion</c>:
    /// dejarla desalineada mostraría el número viejo hasta la próxima consulta del módulo.
    /// </para>
    /// </summary>
    private async Task PropagarProduccionAsync(int loteId, AjusteEncasetamientoCalculos.Delta delta)
    {
        var producciones = await _ctx.LotePosturaProduccion
            .Where(p => p.LoteId == loteId && p.DeletedAt == null)
            .ToListAsync();

        foreach (var lpp in producciones)
        {
            lpp.AvesHInicial = CorrerPorDelta(lpp.AvesHInicial, delta.Hembras);
            lpp.AvesMInicial = CorrerPorDelta(lpp.AvesMInicial, delta.Machos);

            lpp.HembrasInicialesProd = CorrerPorDelta(lpp.HembrasInicialesProd, delta.Hembras);
            lpp.MachosInicialesProd = CorrerPorDelta(lpp.MachosInicialesProd, delta.Machos);

            lpp.AvesHActual = CorrerPorDelta(lpp.AvesHActual, delta.Hembras);
            lpp.AvesMActual = CorrerPorDelta(lpp.AvesMActual, delta.Machos);

            lpp.UpdatedByUserId = _current.UserId;
            lpp.UpdatedAt = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Corre un contador por el delta, con clamp a 0, <b>preservando el NULL</b>.
    /// <para>
    /// El NULL no es un cero: <c>fn_seguimiento_diario_produccion</c> resuelve la base con
    /// <c>COALESCE(aves_h_inicial, hembras_iniciales_prod, 0)</c>, así que materializar un NULL en un
    /// número cambia CUÁL de las dos columnas gana. Un lote con <c>aves_h_inicial</c> nulo y
    /// <c>hembras_iniciales_prod = 7.597</c> pasaría a reportar 500 en vez de 8.097.
    /// </para>
    /// </summary>
    private static int? CorrerPorDelta(int? valor, int delta) =>
        valor is null ? null : Math.Max(0, valor.Value + delta);
}
