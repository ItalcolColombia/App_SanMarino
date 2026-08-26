// Validar y des-validar: convertir la separación en descuento real, y deshacerlo.
// Partial de ValidacionSeguimientoService (namespace plano).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService
{
    /// <summary>
    /// Aplica el efecto real del registro: descuenta el alimento del inventario y las aves del maestro
    /// del lote, marca las reservas como aplicadas y el registro como validado.
    ///
    /// <para>
    /// <b>Todo en una transacción.</b> Si el descuento de alimento se commitea y el de aves falla, el
    /// registro queda mintiendo en las dos direcciones y nadie se entera hasta el cuadre. Con la
    /// transacción, o pasa entero o no pasa.
    /// </para>
    ///
    /// <para>
    /// <b>Idempotente:</b> validar dos veces no descuenta dos veces — el segundo intento encuentra el
    /// registro ya validado y no tiene reservas activas que aplicar.
    /// </para>
    /// </summary>
    public async Task<ResultadoValidacionDto> ValidarAsync(string modulo, long seguimientoId, CancellationToken ct = default)
    {
        if (!ModuloSeguimiento.EsValido(modulo))
            throw new InvalidOperationException($"Módulo de seguimiento desconocido: '{modulo}'.");

        // Canónico: ENGORDE y ENGORDE_EC son el mismo registro y la misma reserva.
        modulo = ModuloSeguimiento.Canonico(modulo);

        var permiso = PermisoValidar(modulo);
        if (!_current.Permissions.Contains(permiso))
            throw new UnauthorizedAccessException($"No tiene el permiso '{permiso}' para validar este registro.");

        var estado = await LeerEstadoAsync(modulo, seguimientoId, ct);
        if (!estado.Existe || !EsDeLaEmpresaActiva(estado.CompanyId))
            throw new InvalidOperationException("El registro de seguimiento no existe o no pertenece a la compañía.");

        // `YaEstabaValidado: true` distingue este caso del registro que se valida ahora y no aplica
        // nada (un día sin consumo ni bajas). Los dos devuelven ceros; el bloque necesita saber cuál
        // es cuál para no contar como «validado ahora» algo que ya estaba.
        if (estado.Validado)
            return new ResultadoValidacionDto(modulo, seguimientoId, 0, 0m, 0, YaEstabaValidado: true);

        var reservasAlimento = await _ctx.SeguimientoReservaAlimento
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Activa)
            .ToListAsync(ct);

        var reservasAves = await _ctx.SeguimientoReservaAves
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Activa)
            .ToListAsync(ct);

        // Transacción CONDICIONAL: `null` cuando ya hay una ambiente (push offline de la PWA), porque
        // EF lanza si se abre una segunda sobre el mismo contexto. Mismo patrón que los Crud.
        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct)
            : null;

        var kg = await AplicarAlimentoAsync(modulo, seguimientoId, reservasAlimento, devolver: false, ct);
        var aves = await AplicarAvesAsync(modulo, seguimientoId, estado.Fecha, reservasAves, devolver: false, ct);

        var ahora = DateTimeOffset.UtcNow;
        foreach (var r in reservasAlimento) { r.Estado = EstadoReservaSeguimiento.Aplicada; r.AplicadaAt = ahora; }
        foreach (var r in reservasAves) { r.Estado = EstadoReservaSeguimiento.Aplicada; r.AplicadaAt = ahora; }
        await _ctx.SaveChangesAsync(ct);

        await MarcarValidadoAsync(modulo, seguimientoId, validado: true, ct);

        // Reproductora: escribir `confirmado` acaba de disparar el cruce, que rehízo por SQL los días
        // 1-7 del lote de engorde. Sus bajas tienen que llegar al maestro DENTRO de esta transacción;
        // para el resto de los módulos es un no-op.
        await SincronizarCruceReproductoraAsync(modulo, estado.LoteRefInt, ct);

        if (tx is not null) await tx.CommitAsync(ct);

        return new ResultadoValidacionDto(modulo, seguimientoId, reservasAlimento.Count, kg, aves);
    }

    /// <summary>
    /// Deshace una validación: devuelve el alimento y las aves, vuelve a dejar las reservas activas y
    /// el registro editable.
    ///
    /// <para>
    /// Es la única vía para corregir un registro ya validado, y por eso pide un permiso propio: acá sí
    /// se están moviendo unidades que ya se descontaron. Al terminar, el registro queda exactamente
    /// como estaba antes de validar — separado, no descontado.
    /// </para>
    /// </summary>
    public async Task<ResultadoValidacionDto> DesvalidarAsync(string modulo, long seguimientoId, CancellationToken ct = default)
    {
        if (!ModuloSeguimiento.EsValido(modulo))
            throw new InvalidOperationException($"Módulo de seguimiento desconocido: '{modulo}'.");

        modulo = ModuloSeguimiento.Canonico(modulo);

        var permiso = PermisoDesvalidar(modulo);
        if (!_current.Permissions.Contains(permiso))
            throw new UnauthorizedAccessException(
                $"No tiene el permiso '{permiso}' para quitarle la validación a este registro.");

        var estado = await LeerEstadoAsync(modulo, seguimientoId, ct);
        if (!estado.Existe || !EsDeLaEmpresaActiva(estado.CompanyId))
            throw new InvalidOperationException("El registro de seguimiento no existe o no pertenece a la compañía.");

        if (!estado.Validado)
            return new ResultadoValidacionDto(modulo, seguimientoId, 0, 0m, 0);

        var reservasAlimento = await _ctx.SeguimientoReservaAlimento
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Aplicada)
            .ToListAsync(ct);

        var reservasAves = await _ctx.SeguimientoReservaAves
            .Where(r => r.OrigenModulo == modulo && r.OrigenSeguimientoId == seguimientoId
                     && r.Estado == EstadoReservaSeguimiento.Aplicada)
            .ToListAsync(ct);

        // Un registro ANTERIOR al encendido del flag está validado porque descontó al guardar, no
        // porque haya pasado por acá: no tiene ni una fila de reserva. Des-validarlo devolvería CERO
        // —no hay reserva que revertir— y lo dejaría marcado como pendiente con su efecto aplicado.
        // A partir de ahí, editarlo separa de nuevo y al validar el consumo se descuenta DOS veces.
        // Preferimos negarnos: lo que hay que corregir es el registro, no la marca.
        if (reservasAlimento.Count == 0 && reservasAves.Count == 0
            && !await TieneAlgunaReservaAsync(modulo, seguimientoId, ct))
        {
            throw new InvalidOperationException(
                "Este registro es anterior a la doble validación: su consumo y sus bajas ya se " +
                "aplicaron al guardarlo, así que no hay nada que devolver. Para corregirlo hay que " +
                "editar el registro, no quitarle la validación.");
        }

        await using var tx = _ctx.Database.CurrentTransaction is null
            ? await _ctx.Database.BeginTransactionAsync(ct)
            : null;

        var kg = await AplicarAlimentoAsync(modulo, seguimientoId, reservasAlimento, devolver: true, ct);
        var aves = await AplicarAvesAsync(modulo, seguimientoId, estado.Fecha, reservasAves, devolver: true, ct);

        foreach (var r in reservasAlimento) { r.Estado = EstadoReservaSeguimiento.Activa; r.AplicadaAt = null; }
        foreach (var r in reservasAves) { r.Estado = EstadoReservaSeguimiento.Activa; r.AplicadaAt = null; }
        await _ctx.SaveChangesAsync(ct);

        await MarcarValidadoAsync(modulo, seguimientoId, validado: false, ct);

        // Reproductora: quitar `confirmado` re-disparó el cruce, y la fn sólo cuenta días confirmados
        // ⇒ BORRÓ los días 1-7. Sin esto sus filas del histórico unificado quedan apuntando a
        // seguimientos que ya no existen y las aves no vuelven nunca al maestro.
        await SincronizarCruceReproductoraAsync(modulo, estado.LoteRefInt, ct);

        if (tx is not null) await tx.CommitAsync(ct);

        return new ResultadoValidacionDto(modulo, seguimientoId, reservasAlimento.Count, kg, aves);
    }

    /// <summary>
    /// Lleva al maestro de aves del lote de engorde las bajas de los días que generó (o borró) el
    /// cruce, después de tocar la marca <c>confirmado</c> de un seguimiento de reproductora.
    ///
    /// <para>
    /// ⚠️ <b>El id no es el mismo.</b> Para reproductora, <c>LeerEstadoAsync</c> devuelve en
    /// <c>LoteRefInt</c> el id del <b>lote de reproductora</b>, mientras que
    /// <c>SincronizarCruceAsync</c> espera el del <b>lote de engorde</b>. Pasarlo directo no lanza
    /// ninguna excepción: no encuentra el lote y no hace nada —o peor, sincroniza el lote de engorde
    /// que por casualidad tenga ese id—. Por eso se resuelve el puente primero, igual que
    /// <c>SeguimientoDiarioLoteReproductoraService.SincronizarBajasCruceAsync</c>.
    /// </para>
    ///
    /// <para>
    /// Idempotente (el aplicador saltea los orígenes ya aplicados). Los fallos se registran sin tumbar
    /// la validación: el cruce ya ocurrió y el reporte diario, que calcula desde
    /// <c>aves_encasetadas</c>, sigue mostrando el saldo correcto.
    /// </para>
    /// </summary>
    private async Task SincronizarCruceReproductoraAsync(
        string modulo, int loteReproductoraId, CancellationToken ct)
    {
        if (!ModuloSeguimiento.RequiereSincronizarCruce(modulo)) return;
        if (loteReproductoraId <= 0) return;

        try
        {
            var loteEngordeId = await _ctx.LoteReproductoraAveEngorde.AsNoTracking()
                .Where(l => l.Id == loteReproductoraId)
                .Select(l => l.LoteAveEngordeId)
                .FirstOrDefaultAsync(ct);
            if (loteEngordeId <= 0) return;

            await RetiroAvesEngordeAplicador.SincronizarCruceAsync(_ctx, _current.CompanyId, loteEngordeId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex,
                "Error al sincronizar las bajas del cruce con el maestro de aves, lote reproductora {LoteReproductoraId}",
                loteReproductoraId);
        }
    }

    // ─── Aplicación del efecto ────────────────────────────────────────────────

    /// <summary>
    /// Aves que el maestro del lote tiene HOY, sumando los tres buckets. <c>null</c> cuando el módulo
    /// no escribe un maestro de aves (producción, cuyo saldo lo manda la fn SQL, y reproductora, cuyas
    /// bajas las produce el cruce) — ahí no hay nada que comprobar.
    /// </summary>
    private async Task<int?> LeerSaldoAvesDelLoteAsync(
        string modulo, SeguimientoReservaAves r, int? lotePosturaLevanteId, CancellationToken ct)
    {
        switch (modulo)
        {
            case ModuloSeguimiento.Levante:
            {
                var lplId = lotePosturaLevanteId ?? r.LoteRefInt;
                var saldo = await _ctx.LotePosturaLevante.AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId == lplId && l.DeletedAt == null)
                    .Select(l => new { l.AvesHActual, l.AvesMActual })
                    .FirstOrDefaultAsync(ct);
                if (saldo is null) return null;
                return (saldo.AvesHActual ?? 0) + (saldo.AvesMActual ?? 0);
            }
            case ModuloSeguimiento.Engorde:
            case ModuloSeguimiento.EngordeEcuador:
            {
                var saldo = await _ctx.LoteAveEngorde.AsNoTracking()
                    .Where(l => l.LoteAveEngordeId == r.LoteRefInt && l.CompanyId == r.CompanyId
                             && l.DeletedAt == null)
                    .Select(l => new { l.HembrasL, l.MachosL, l.Mixtas })
                    .FirstOrDefaultAsync(ct);
                if (saldo is null) return null;
                return (saldo.HembrasL ?? 0) + (saldo.MachosL ?? 0) + (saldo.Mixtas ?? 0);
            }
            default:
                return null;
        }
    }

    /// <summary>
    /// Descuenta (o devuelve) el alimento de las reservas, por el mismo camino de inventario que
    /// habría usado el Crud: Colombia por el servicio atómico a nivel granja, Ecuador y Panamá por
    /// inventario_gestion con núcleo y galpón. El modelo se resuelve con el <c>pais_id</c> que la
    /// reserva guardó del LOTE, no del usuario.
    /// </summary>
    private async Task<decimal> AplicarAlimentoAsync(
        string modulo, long seguimientoId,
        IReadOnlyList<SeguimientoReservaAlimento> reservas, bool devolver, CancellationToken ct)
    {
        if (reservas.Count == 0) return 0m;

        // El total se acumula con lo REALMENTE aplicado, no con lo separado: devolver la suma de las
        // reservas hacía que un descuento que no ocurrió se reportara como ocurrido.
        var total = 0m;

        // La referencia la arma el calculo puro porque NO es texto libre: es la clave por la que el
        // saldo de alimento, el cuadre y las conciliaciones distinguen «alimento que entró al galpón»
        // de «reversión contable de un consumo». Armarla acá con `modulo.ToLowerInvariant()` producía
        // literales que no lee nadie (`Seguimiento engorde #…` en vez del `Seguimiento aves engorde #…`
        // que escribe el Crud), y la devolución de una desvalidación se contaba como alimento nuevo.
        var refStr = ReservaSeguimientoCalculos.ReferenciaInventario(
            modulo, seguimientoId, reservas[0].FechaSeguimiento, devolver);

        foreach (var grupo in reservas.GroupBy(r => new { r.PaisId, r.FarmId, r.NucleoId, r.GalponId }))
        {
            var modelo = InventarioConsumoGate.ResolverModelo(grupo.Key.PaisId);
            var kgGrupo = grupo.Sum(r => r.CantidadKg);

            // País sin resolver con kilos separados ⇒ se cae la transacción entera. Antes era un
            // `continue` mudo que dejaba el registro validado y el inventario sin tocar.
            var motivo = ReservaSeguimientoCalculos.MotivoAlimentoNoAplicable(
                modelo, kgGrupo, grupo.Key.PaisId, reservas[0].LoteRef);
            if (motivo is not null) throw new InvalidOperationException(motivo);

            if (modelo == ModeloInventarioConsumo.Ninguno) continue;

            if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null)
            {
                var porItem = AItemConsumo(grupo);
                if (devolver)
                {
                    // Misma fecha que la confirmación que se está deshaciendo: el seguimiento original,
                    // no el día en que alguien quita la validación.
                    await _colombiaConsumoB.AplicarDevolucionAsync(
                        grupo.Key.FarmId, porItem, refStr, "Devolución por quitar la validación del seguimiento", ct,
                        fechaMovimiento: reservas[0].FechaSeguimiento.ToDateTime(TimeOnly.MinValue));
                }
                else
                {
                    // Se valida el stock ANTES de aplicar: si falta, la transacción entera se cae y el
                    // registro queda sin validar, que es el estado honesto.
                    await _colombiaConsumoB.ValidarStockConsumoAsync(grupo.Key.FarmId, porItem, null, ct);
                    // El movimiento se fecha en el DÍA DEL SEGUIMIENTO, no en el de la validación (mismo
                    // criterio que el modelo B más abajo).
                    await _colombiaConsumoB.AplicarConsumoAsync(grupo.Key.FarmId, porItem, refStr, ct,
                        fechaMovimiento: reservas[0].FechaSeguimiento.ToDateTime(TimeOnly.MinValue));
                }
                total += kgGrupo;
                continue;
            }

            // Modelo B sin el servicio de inventario en el contenedor: preferimos caernos a marcar el
            // registro como validado sobre un inventario que nadie tocó.
            if (modelo == ModeloInventarioConsumo.ModeloB && _inventarioGestion is null)
                throw new InvalidOperationException(
                    $"No se puede validar: hay {kgGrupo:0.###} kg separados y el servicio de inventario " +
                    "no está disponible para aplicarlos.");

            if (modelo != ModeloInventarioConsumo.ModeloB) continue;

            foreach (var r in grupo)
            {
                if (r.CantidadKg <= 0) continue;
                if (devolver)
                {
                    // Misma fecha que la confirmación que se está deshaciendo: el seguimiento original,
                    // no el día en que alguien quita la validación.
                    await _inventarioGestion!.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                        r.FarmId, r.NucleoId?.Trim(), r.GalponId?.Trim(), r.ItemInventarioEcuadorId,
                        r.CantidadKg, r.Unit, refStr, "Devolución por quitar la validación del seguimiento",
                        FechaMovimiento: r.FechaSeguimiento.ToDateTime(TimeOnly.MinValue)));
                }
                else
                {
                    // El movimiento se fecha en el DÍA DEL SEGUIMIENTO, no en el de la validación: si
                    // se validan cinco días juntos, el kardex del galpón tiene que seguir mostrando un
                    // consumo por día y no cinco el mismo día.
                    await _inventarioGestion!.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                        r.FarmId, r.NucleoId?.Trim(), r.GalponId?.Trim(), r.ItemInventarioEcuadorId,
                        r.CantidadKg, r.Unit, refStr, null,
                        FechaMovimiento: r.FechaSeguimiento.ToDateTime(TimeOnly.MinValue)));
                }
                total += r.CantidadKg;
            }
        }

        return total;
    }

    /// <summary>
    /// Descuenta (o devuelve) las aves separadas, delegando en el MISMO aplicador que usa el descuento
    /// al guardar: postura en <see cref="DescuentoAvesPosturaAplicador"/> y engorde en
    /// <c>RetiroAvesEngordeAplicador</c>. Reproductora no pasa por acá — sus bajas las produce el cruce
    /// que dispara la propia marca de confirmación.
    /// </summary>
    private async Task<int> AplicarAvesAsync(
        string modulo, long seguimientoId, DateOnly fecha,
        IReadOnlyList<SeguimientoReservaAves> reservas, bool devolver, CancellationToken ct)
    {
        if (reservas.Count == 0) return 0;

        // El par (lote_postura_levante_id, lote_id) sale del REGISTRO, no de la reserva.
        //
        // `SeguimientoReservaAves` guarda un solo entero (`lote_ref_int`) que en levante vale
        // `LotePosturaLevanteId ?? LoteId` — dos espacios de ids distintos colapsados en una columna.
        // El aplicador prueba primero `lote_postura_levante_id == <ese entero>`, así que un valor que
        // en realidad era un `lote_id` podía casar con el LPL de OTRO lote (y de otra empresa: esa
        // consulta no filtra por compañía) y descontarle la mortalidad al lote equivocado.
        var loteLevante = ModuloSeguimiento.Levante.Equals(modulo, StringComparison.OrdinalIgnoreCase)
            ? await _ctx.SeguimientoDiario.AsNoTracking()
                .Where(s => s.Id == seguimientoId)
                .Select(s => new { s.LotePosturaLevanteId, s.LoteId })
                .FirstOrDefaultAsync(ct)
            : null;

        var total = 0;
        foreach (var r in reservas)
        {
            // Los tres campos se suman porque el aplicador recibe el desglose mort/sel/err y solo los
            // totaliza: mandar el total en el primero es aritméticamente idéntico.
            var hembras = r.Hembras + r.Mixtas;
            var machos = r.Machos;
            if (hembras <= 0 && machos <= 0) continue;
            total += hembras + machos;

            // Al VALIDAR se exige saldo suficiente. Sin esto el descuento recorta en cero y
            // des-validar suma de vuelta el número entero: el lote termina con más aves de las que
            // tenía. Al DEVOLVER no se comprueba nada — devolver siempre puede.
            if (!devolver)
            {
                var disponible = await LeerSaldoAvesDelLoteAsync(modulo, r, loteLevante?.LotePosturaLevanteId, ct);
                if (disponible is int saldo)
                {
                    var motivo = ReservaSeguimientoCalculos.MotivoAvesNoAplicable(
                        saldo, hembras + machos, r.LoteRef);
                    if (motivo is not null) throw new InvalidOperationException(motivo);
                }
            }

            switch (modulo)
            {
                case ModuloSeguimiento.Levante:
                    await DescuentoAvesPosturaAplicador.AplicarLevanteAsync(
                        _ctx,
                        loteLevante?.LotePosturaLevanteId,
                        loteLevante?.LoteId ?? r.LoteRefInt.ToString(),
                        hembras, machos, 0, 0, 0, 0, resta: !devolver, ct);
                    break;

                case ModuloSeguimiento.Produccion:
                    // NO se mueve `lote_postura_produccion.aves_h_actual`: esa columna no es el maestro
                    // del saldo, es una CACHÉ que `ProduccionService.Consultas` reescribe con el
                    // resultado de `fn_seguimiento_diario_produccion`. Esa fn suma las bajas de todas
                    // las filas sin mirar `validado` —ninguna fn del esquema lo mira—, así que las bajas
                    // ya están descontadas desde que se guarda el registro. Restarlas otra vez acá
                    // dejaba la caché con el doble hasta la siguiente consulta del módulo, y en esa
                    // ventana el traslado veía menos aves de las que hay.
                    //
                    // Consecuencia asumida y documentada: en producción la doble validación difiere el
                    // ALIMENTO, no el saldo de aves. Diferirlo también exigiría que la fn filtre por
                    // `validado`, y eso cambia el número de TODAS las empresas — cambio que pide el gate
                    // de paridad multipaís, no este arreglo.
                    break;

                case ModuloSeguimiento.Engorde:
                case ModuloSeguimiento.EngordeEcuador:
                    // El aplicador de engorde es idempotente por (lote, origen): lleva el baseline ya
                    // aplicado, así que devolver es sincronizar contra CERO, no restar al revés.
                    // La empresa sale de la RESERVA (es la del lote): `SincronizarAsync` filtra el lote
                    // por company_id y RETORNA EN SILENCIO si no lo encuentra, así que con la del
                    // usuario un desfase dejaba el registro validado sin descontar un ave.
                    await RetiroAvesEngordeAplicador.SincronizarAsync(
                        _ctx, r.CompanyId, r.LoteRefInt, seguimientoId,
                        fecha.ToDateTime(TimeOnly.MinValue),
                        bajasHembrasNuevas: devolver ? 0 : hembras,
                        bajasMachosNuevas: devolver ? 0 : machos);
                    break;

                case ModuloSeguimiento.Reproductora:
                    // Lo resuelve el cruce: al escribir `confirmado` el trigger rehace los días 1-7 del
                    // lote de engorde, y la sincronización posterior lleva esas bajas al maestro.
                    break;
            }
        }

        if (ModuloSeguimiento.Reproductora.Equals(modulo, StringComparison.OrdinalIgnoreCase))
            total = reservas.Sum(r => r.Hembras + r.Machos + r.Mixtas);

        return total;
    }
}
