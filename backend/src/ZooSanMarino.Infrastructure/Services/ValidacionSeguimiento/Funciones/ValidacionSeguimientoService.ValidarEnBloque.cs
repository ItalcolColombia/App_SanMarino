// Validar en bloque: los pendientes de un lote, en orden cronológico y con corte a la primera falla.
// Partial de ValidacionSeguimientoService (namespace plano).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService
{
    /// <summary>
    /// Valida los pendientes del lote en <b>orden cronológico</b>, cada uno en su propia transacción,
    /// y <b>corta en la primera falla</b>. Devuelve el detalle registro por registro.
    ///
    /// <para>
    /// <b>Una transacción por registro, no una para todo.</b> El éxito parcial es el punto del
    /// feature: hoy 34 POST son 34 transacciones y si el 20 falla quedan los 19 buenos. Un botón que
    /// devuelva cero donde el de a uno devolvía 19 sería peor que no tenerlo. Y la falla no es
    /// excepcional: un backlog de 34 días es alguien que cargó 34 días de consumo, y que a la mitad le
    /// falte el ingreso de alimento es lo esperable.
    /// </para>
    ///
    /// <para>
    /// 🔴 <b>Se niega a correr dentro de una transacción abierta.</b> <c>ValidarAsync</c> abre la suya
    /// <i>sólo</i> si no hay una ambiente; con una envolvente, ninguno de los N commitearía y el
    /// bloque se volvería todo-o-nada <b>en silencio</b>, sin que ningún test unitario lo note.
    /// Preferimos el error explícito a la sorpresa.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> reintentar el bloque después de corregir el registro que cortó retoma
    /// exactamente donde paró — los ya validados salen <c>YA_VALIDADO</c> con efecto cero.
    /// </para>
    /// </summary>
    /// <param name="modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
    /// <param name="loteId">Clave numérica del lote en ese módulo.</param>
    public async Task<ResultadoValidacionEnBloqueDto> ValidarPendientesDelLoteAsync(
        string modulo, int loteId, CancellationToken ct = default)
    {
        if (!ModuloSeguimiento.EsValido(modulo))
            throw new InvalidOperationException($"Módulo de seguimiento desconocido: '{modulo}'.");

        modulo = ModuloSeguimiento.Canonico(modulo);

        // El permiso se chequea UNA vez, antes del bucle: no depende del registro, así que si falta no
        // se aplicó nada y corresponde un 403 para la request entera, no un ítem FALLO que sugiera que
        // el problema es del día. `ValidarAsync` lo re-chequea igual; la duplicación es inocua.
        var permiso = PermisoValidar(modulo);
        if (!_current.Permissions.Contains(permiso))
            throw new UnauthorizedAccessException(
                $"No tiene el permiso '{permiso}' para validar este registro.");

        if (_ctx.Database.CurrentTransaction is not null)
            throw new InvalidOperationException(
                "No se puede validar en bloque dentro de una transacción abierta: las validaciones no " +
                "se confirmarían y el bloque quedaría en todo-o-nada sin decirlo.");

        // El conjunto lo resuelve el SERVIDOR, fail-closed por empresa en los cuatro módulos. Aceptar
        // una lista de ids del cliente reabriría el agujero que cerró `EsDeLaEmpresaActiva`: un lote
        // de otra empresa devuelve vacío y el bloque responde «no hay pendientes».
        var pendientes = (await LeerPendientesDelLoteAsync(modulo, loteId, ct))
            .Select(p => new PendienteValidacion(p.Id, p.Fecha))
            .ToList();

        var (seleccionados, fueraDeTope) = ValidacionEnBloqueCalculos.OrdenDeValidacion(pendientes);
        var items = new List<ItemValidacionEnBloque>(seleccionados.Count + fueraDeTope.Count);

        for (var i = 0; i < seleccionados.Count; i++)
        {
            var p = seleccionados[i];
            try
            {
                var r = await ValidarAsync(modulo, p.SeguimientoId, ct);
                items.Add(ValidacionEnBloqueCalculos.ItemAplicado(
                    p, r.ItemsAplicados, r.KgAplicados, r.AvesDescontadas, r.YaEstabaValidado));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // 🔴 La transacción de `ValidarAsync` ya revirtió la BD al salir por excepción, pero el
                // ChangeTracker conserva las entidades con el valor NUEVO y marcadas Unchanged. El
                // registro siguiente las reusaría por identity map y descontaría desde un saldo que en
                // la base nunca existió. Y no hay forma de que se note sola: la guarda de aves lee con
                // AsNoTracking —ve el valor real revertido y pasa— mientras los aplicadores leen
                // rastreado y reciben la instancia envenenada.
                _ctx.ChangeTracker.Clear();

                var motivo = ex is DbUpdateException
                    ? ValidacionEnBloqueCalculos.MotivoConflictoConcurrente()
                    : ex.Message;

                _logger?.LogWarning(ex,
                    "Validación en bloque cortada en el seguimiento {SeguimientoId} del lote {LoteId} ({Modulo})",
                    p.SeguimientoId, loteId, modulo);

                items.Add(ValidacionEnBloqueCalculos.ItemFallido(p, motivo));

                // Corte: los días siguientes consumen del MISMO stock y del MISMO saldo que acaba de
                // rechazar a éste, así que seguir daría una cascada de mensajes derivados del primero.
                // Y en el caso menos probable —que alguno use otro ítem con stock— sería peor: dejaría
                // un pendiente rodeado de validados, que es justo lo que vuelve a bloquear el alta.
                for (var j = i + 1; j < seleccionados.Count; j++)
                    items.Add(ValidacionEnBloqueCalculos.ItemNoIntentado(seleccionados[j]));

                break;
            }
        }

        foreach (var p in fueraDeTope)
            items.Add(ValidacionEnBloqueCalculos.ItemNoIntentado(p));

        var resumen = ValidacionEnBloqueCalculos.Resumir(items);

        return new ResultadoValidacionEnBloqueDto(
            Modulo: modulo,
            LoteId: loteId,
            Solicitados: resumen.Solicitados,
            Validados: resumen.Validados,
            YaValidados: resumen.YaValidados,
            Fallidos: resumen.Fallidos,
            NoIntentados: resumen.NoIntentados,
            KgAplicados: resumen.KgAplicados,
            AvesDescontadas: resumen.AvesDescontadas,
            SeguimientoCorte: resumen.SeguimientoCorte,
            FechaCorte: resumen.FechaCorte,
            MotivoCorte: resumen.MotivoCorte,
            Mensaje: resumen.Mensaje,
            Detalle: items.Select(x => new ResultadoValidacionEnBloqueItemDto(
                x.SeguimientoId, x.Fecha, x.Resultado,
                x.ItemsAplicados, x.KgAplicados, x.AvesDescontadas, x.Motivo)).ToList());
    }
}
