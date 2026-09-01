// src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.StockMutacion.cs
// Mutacion directa de una fila de stock: actualizar, eliminar, anular un movimiento del historico
// (con su aviso de fecha fuera de ciclo).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Shared;
using ZooSanMarino.Application.DTOs.Galpones;
using ZooSanMarino.Application.Exceptions;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class InventarioGestionService
{
    /// <summary>Valida empresa, país y granjas asignadas; carga ítem de catálogo.</summary>
    private async Task<InventarioGestionStock> GetStockForMutationAsync(int stockId, CancellationToken ct)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var stock = await _db.InventarioGestionStock
            .Include(x => x.ItemInventario)
            .FirstOrDefaultAsync(x => x.Id == stockId, ct);
        if (stock == null)
            throw new InvalidOperationException("El registro de stock no existe.");
        if (stock.CompanyId != companyId.Value)
            throw new InvalidOperationException("No autorizado.");
        var effectivePais = await GetEffectivePaisIdAsync(stock.FarmId, ct);
        if (effectivePais > 0 && stock.PaisId != effectivePais)
            throw new InvalidOperationException("El registro no corresponde al país activo.");
        if (!allowedFarmIds.Contains(stock.FarmId))
            throw new InvalidOperationException("No tiene acceso a esta granja.");
        return stock;
    }

    public async Task<InventarioGestionStockDto> ActualizarStockAsync(int stockId, InventarioGestionStockUpdateRequest req, CancellationToken ct = default)
    {
        if (req.Quantity < 0)
            throw new InvalidOperationException("La cantidad no puede ser negativa.");

        var stock = await GetStockForMutationAsync(stockId, ct);
        var item = stock.ItemInventario;
        var oldQty = stock.Quantity;
        var oldUnit = stock.Unit;
        // TK-2026-000019 — la unidad DEJA de ser editable acá: la manda el catálogo del ítem. Este
        // campo era texto libre y es el que llenó la base de `LT`, `UND`, `GALONES` y `DOSIS`,
        // porque operación lo usaba para tapar el `kg` que mostraba el stock. `req.Unit` se sigue
        // aceptando en el contrato (no rompe clientes viejos) pero ya no decide nada; si la fila
        // venía torcida, este ajuste la realinea y queda escrito en el motivo.
        var newUnit = UnidadInventarioCalculos.Resolver(item.Unidad, stock.Unit);

        DateTimeOffset? newCreated = null;
        if (req.FechaIngreso.HasValue)
        {
            var d = req.FechaIngreso.Value.Date;
            newCreated = new DateTimeOffset(d.Year, d.Month, d.Day, 12, 0, 0, TimeSpan.Zero);
        }

        var qtyChanged = oldQty != req.Quantity;
        var unitChanged = !string.Equals(oldUnit.Trim(), newUnit.Trim(), StringComparison.OrdinalIgnoreCase);
        var fechaChanged = newCreated.HasValue && stock.CreatedAt.Date != newCreated.Value.Date;

        if (!qtyChanged && !unitChanged && !fechaChanged)
            throw new InvalidOperationException("No hay cambios.");

        if (newCreated.HasValue)
            stock.CreatedAt = newCreated.Value;

        stock.UpdatedAt = DateTimeOffset.UtcNow;

        if (qtyChanged || unitChanged)
        {
            var delta = req.Quantity - oldQty;
            stock.Quantity = req.Quantity;
            stock.Unit = newUnit;

            var extra = string.IsNullOrWhiteSpace(req.Reason) ? null : req.Reason.Trim();
            var reasonFull = $"Ajuste manual. Anterior: {oldQty} {oldUnit}. Nuevo: {req.Quantity} {newUnit}.";
            if (fechaChanged && newCreated.HasValue)
                reasonFull += $" Fecha ingreso: {newCreated.Value:yyyy-MM-dd}.";
            if (extra != null)
                reasonFull += $" Motivo: {extra}";

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = delta != 0 ? Math.Abs(delta) : 0m,
                Unit = newUnit,
                MovementType = "AjusteStock",
                Estado = "Ajuste manual",
                Reference = null,
                Reason = reasonFull,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        await _db.SaveChangesAsync(ct);

        var list = await GetStockAsync(stock.FarmId, stock.NucleoId, stock.GalponId, null, null, ct);
        return list.FirstOrDefault(x => x.Id == stockId)
            ?? new InventarioGestionStockDto(
                stock.Id, stock.FarmId, stock.NucleoId, stock.GalponId, stock.ItemInventarioEcuadorId,
                item.Codigo, item.Nombre, item.Concepto ?? item.TipoItem ?? "alimento",
                stock.Quantity, stock.Unit, null, null, null, stock.CreatedAt);
    }

    /// <summary>Tipo que baja la TABLA DIARIA sin tocar stock (el mismo que escribe «Cuadrar galpón»).</summary>
    private const string MovimientoAjusteTablaSalidaPorEliminacion = "AjusteCuadreTablaSalida";

    /// <summary>
    /// Elimina un registro de stock: el stock se va y la TABLA DIARIA baja los mismos kilos.
    ///
    /// <para>
    /// 🔴 <b>El defecto que cierra (TK-2026-000183, CAROLINA / GALPON 1 / lote 2602).</b> Hasta el
    /// 1-sep-2026 esto borraba la fila de stock y escribía únicamente el <c>EliminacionStock</c>, que
    /// se espeja como <c>INV_OTRO</c> — un <c>tipo_evento</c> que <c>fn_seguimiento_diario_engorde</c>
    /// no lee. Resultado: <b>el stock quedaba bien y la tabla diaria quedaba alta para siempre</b>.
    /// Es el espejo exacto del defecto de <c>EliminarIngresoAsync</c> (ahí se anulaba el histórico y
    /// no se devolvía el stock); acá el stock se descuenta y el histórico sigue contando.
    /// Medido: un ingreso duplicado de 2.880 kg eliminado así dejó el día 1 del lote en
    /// <c>5.600 = 2.880 (apertura) + 2.880 (duplicado) − 160 (consumo)</c>, y el lote cerró con
    /// 2.880 kg de residuo contra los 0 de su galpón gemelo.
    /// </para>
    ///
    /// <para>
    /// <b>Por qué un segundo movimiento y no cambiar el trato del <c>EliminacionStock</c>.</b> Hacer
    /// que la fn leyera <c>INV_OTRO</c> cambiaría el trato de filas que YA EXISTEN —el naufragio de
    /// v15/v16, que el gate multipaís revirtió dos veces—. Un <c>AjusteCuadreTablaSalida</c> nuevo no
    /// mueve una sola fila vieja: el tipo ya está en producción, la fn v17 ya lo lee y no toca stock,
    /// que es justo lo que hace falta acá (el stock ya lo descontó la eliminación).
    /// </para>
    ///
    /// <para>
    /// Los dos movimientos comparten <b>el mismo timestamp</b>, así que el histórico los fecha el
    /// mismo día (<c>fecha_operacion = (created_at AT TIME ZONE 'UTC')::DATE</c>) y se leen como el
    /// par que son. El invariante <c>saldo == stock − movimientos posteriores</c> cierra en los dos
    /// casos: si la fecha cae dentro de la grilla baja el saldo, y si cae después del último
    /// seguimiento es un «movimiento posterior» que baja el esperado en la misma cantidad.
    /// </para>
    /// </summary>
    public async Task EliminarStockAsync(int stockId, CancellationToken ct = default)
    {
        var stock = await GetStockForMutationAsync(stockId, ct);
        // La unidad del catálogo (TK-2026-000019): `GetStockForMutationAsync` trae el ítem.
        var unidad = UnidadInventarioCalculos.Resolver(stock.ItemInventario?.Unidad, stock.Unit);
        var ahora = DateTimeOffset.UtcNow;
        var kilos = stock.Quantity;

        if (kilos > 0)
        {
            // 1) La auditoría de la baja de stock. Se conserva tal cual: es el registro de que
            //    alguien eliminó el registro, y sigue siendo INV_OTRO a propósito.
            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = kilos,
                Unit = unidad,
                MovementType = "EliminacionStock",
                Estado = "Eliminación registro",
                Reference = null,
                Reason = "Eliminación del registro de stock desde gestión de inventario.",
                CreatedAt = ahora,
                CreatedByUserId = _current?.UserId.ToString()
            });

            // 2) El espejo del lado de la TABLA DIARIA, que es lo que faltaba. No toca stock —ya lo
            //    descontó la eliminación—, y la cantidad va en valor absoluto porque el signo lo
            //    lleva el tipo, igual que en TrasladoEntrada/TrasladoSalida.
            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = stock.CompanyId,
                PaisId = stock.PaisId,
                FarmId = stock.FarmId,
                NucleoId = stock.NucleoId,
                GalponId = stock.GalponId,
                ItemInventarioEcuadorId = stock.ItemInventarioEcuadorId,
                Quantity = kilos,
                Unit = unidad,
                MovementType = MovimientoAjusteTablaSalidaPorEliminacion,
                Estado = "Ajuste por eliminación",
                // La referencia es lo que la tabla diaria muestra en la columna Documento del día.
                Reference = "Eliminación de stock",
                Reason =
                    $"Baja de la tabla diaria por la eliminación del registro de stock ({kilos:N3} " +
                    $"{unidad}). No toca el stock: esos kilos ya salieron con la eliminación.",
                CreatedAt = ahora,
                CreatedByUserId = _current?.UserId.ToString()
            });
        }

        _db.InventarioGestionStock.Remove(stock);
        // Un solo SaveChanges: los dos movimientos y la baja del stock van en la misma transacción
        // implícita de EF. Guardar el ajuste sin la baja —o al revés— dejaría el galpón MÁS
        // descuadrado que antes.
        await _db.SaveChangesAsync(ct);

        // Después del SaveChanges: la fila del histórico que lee el saldo la escribe el trigger
        // AFTER INSERT del movimiento.
        if (kilos > 0)
            await RefrescarSaldoAlimentoEngordeAsync(
                stock.CompanyId, stock.FarmId, stock.NucleoId, stock.GalponId,
                MovimientoAjusteTablaSalidaPorEliminacion, ct);
    }

    public async Task AnularMovimientoHistoricoAsync(int movimientoId, string? motivo, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId is null or <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);
        if (mov == null)
            throw new InvalidOperationException("El movimiento no existe o no pertenece a su empresa.");
        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a la granja de este movimiento.");

        var mt = (mov.MovementType ?? "").Trim();
        if (!string.Equals(mt, "Consumo", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(mt, "Ingreso", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Solo se pueden anular movimientos de tipo Consumo o Ingreso. Use los flujos de traslado/tránsito para corregir otros casos.");

        // A1/A2 — la reversión del stock, la anulación del histórico y el borrado del movimiento
        // van juntos. Revertir el stock y fallar al anular el histórico dejaría contado un ingreso
        // que ya salió del stock: kilos que la tabla diaria muestra y que no existen.
        await EnTransaccionAsync(async () =>
        {
            if (string.Equals(mt, "Consumo", StringComparison.OrdinalIgnoreCase))
            {
                // Anular un consumo DEVUELVE stock: es una suma, con la misma carrera que un ingreso.
                var (cId, pId) = await GetFarmCompanyAndPaisAsync(mov.FarmId, ct);
                // TK-2026-000019 — al devolver el stock, la unidad la fija el catálogo del ítem;
                // el movimiento anulado puede traer una unidad vieja.
                var unidadCatalogo = await _db.ItemInventario.AsNoTracking()
                    .Where(i => i.Id == mov.ItemInventarioEcuadorId)
                    .Select(i => i.Unidad)
                    .FirstOrDefaultAsync(ct);
                await SumarStockAtomicoAsync(
                    cId, pId, mov.FarmId, mov.NucleoId, mov.GalponId,
                    mov.ItemInventarioEcuadorId, mov.Quantity,
                    UnidadInventarioCalculos.Resolver(unidadCatalogo, mov.Unit), mov.SiloId, ct);
            }
            else
            {
                // Anular un ingreso RESTA stock: si otro movimiento ya lo consumió, no se puede anular.
                var stock = await BuscarStockSinRastreoAsync(mov.FarmId, mov.ItemInventarioEcuadorId, mov.NucleoId, mov.GalponId, mov.SiloId, ct);
                if (stock == null || !await DescontarStockAtomicoAsync(stock.Id, mov.Quantity, ct))
                    throw new InvalidOperationException(
                        "No se puede anular este ingreso: no hay stock suficiente en la ubicación para revertir la cantidad.");
            }

            // El movimiento se borra, así que su fila del histórico tiene que quedar ANULADA o se
            // convierte en huérfana: el saldo de alimento seguiría contando un ingreso que ya salió del
            // stock, y la tabla diaria mostraría kilos que no existen. Misma convención de auditoría que
            // EliminarIngresoAsync y EliminarTrasladoAsync (marcar, no borrar).
            await AnularHistoricoDelMovimientoAsync(mov, ct);

            _db.InventarioGestionMovimientos.Remove(mov);
            await _db.SaveChangesAsync(ct);
        }, ct);
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);
    }

    /// <summary>
    /// Marca como anulada la fila del histórico unificado que refleja un movimiento de inventario.
    /// <para>
    /// El histórico lo escribe el trigger <c>trg_inventario_gestion_movimiento_lote_hist</c>, que es
    /// <b>solo AFTER INSERT</b>: nada propaga los UPDATE ni los DELETE del movimiento. Cada camino que
    /// deshace un movimiento tiene que anular su fila a mano o el saldo de alimento se separa del stock.
    /// </para>
    /// <para>
    /// Busca por la clave del histórico (<c>origen_tabla</c> + <c>origen_id</c>, única) y cae a un
    /// fallback por ubicación + ítem + cantidad, igual que <c>EliminarIngresoAsync</c>: hay filas
    /// antiguas cargadas antes de que existiera esa clave.
    /// </para>
    /// </summary>
    /// <summary>
    /// Evalúa si el movimiento quedó fechado FUERA del ciclo vigente del galpón y devuelve el aviso a
    /// mostrar, o <c>null</c> si la fecha es normal. <b>Avisa, no bloquea:</b> retrofechar es legítimo
    /// —la operación a veces registra el lunes lo que llegó el viernes— y bloquearlo tendría un costo
    /// real. Lo que no puede pasar es que lo haga sin enterarse.
    /// <para>
    /// Ver <see cref="AvisoFechaFueraDeCicloCalculos"/> para el caso que lo originó.
    /// </para>
    /// </summary>
    private async Task<string?> EvaluarAvisoFechaFueraDeCicloAsync(
        int companyId, int farmId, string? nucleoId, string? galponId, DateTimeOffset fechaMovimiento,
        CancellationToken ct)
    {
        var nucleo = (nucleoId ?? "").Trim();
        var galpon = (galponId ?? "").Trim();
        if (galpon.Length == 0)
            return null;   // nivel granja: no pertenece al ciclo de ningún galpón

        var ciclos = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == companyId
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.LoteAveEngordeId != null)
            .Select(l => new
            {
                l.LoteAveEngordeId,
                l.LoteNombre,
                SegMin = _db.SeguimientoDiarioAvesEngorde
                    .Where(s => s.LoteAveEngordeId == l.LoteAveEngordeId).Min(s => (DateTime?)s.Fecha),
                SegMax = _db.SeguimientoDiarioAvesEngorde
                    .Where(s => s.LoteAveEngordeId == l.LoteAveEngordeId).Max(s => (DateTime?)s.Fecha)
            })
            .ToListAsync(ct);

        var diasPrevios = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct);

        return AvisoFechaFueraDeCicloCalculos.Evaluar(
            fechaMovimiento.UtcDateTime.Date,
            ciclos.Where(c => c.SegMin.HasValue && c.SegMax.HasValue)
                  .Select(c => new CicloGalpon(
                      c.LoteAveEngordeId!.Value,
                      c.LoteNombre ?? $"#{c.LoteAveEngordeId}",
                      c.SegMin!.Value,
                      c.SegMax!.Value)),
            diasPrevios ?? 10);
    }

    private async Task AnularHistoricoDelMovimientoAsync(InventarioGestionMovimiento mov, CancellationToken ct)
    {
        var hist = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento"
                                   && h.OrigenId == mov.Id, ct);
        hist ??= await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h =>
                h.FarmId == mov.FarmId &&
                h.NucleoId == mov.NucleoId &&
                h.GalponId == mov.GalponId &&
                h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                h.CantidadKg == mov.Quantity &&
                !h.Anulado, ct);

        if (hist != null)
            hist.Anulado = true;
    }

    /// <summary>
    /// Guarda de servidor de la marca «para el próximo ciclo» (v16a, 18-ago-2026, FASE A del plan
    /// <c>fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md</c>).
    /// <para>
    /// La feature está APAGADA hasta que entre la atribución persistida (Fase B). Hasta hoy el apagado
    /// era sólo del front (<c>mostrarParaProximoCicloIngreso</c> devuelve <c>false</c> y
    /// <c>puedeMarcarDestinoCiclo</c> exige que la marca ya esté puesta), así que Swagger, la PWA, la
    /// carga masiva o un script podían volver a ponerla. Medido sobre el dump local con la v15 que
    /// corre en producción: marcar los 2.371 movimientos de alimento reales deja 24 filas de la tabla
    /// diaria SIN NINGUNA pantalla, 1.733 filas con saldo distinto (peor caso 193.701,7 kg), lleva las
    /// filas en negativo de 97 a 1.160 y el cuadre de 8 a 58 galpones descuadrados.
    /// </para>
    /// <para>
    /// QUITAR una marca existente sigue permitido a propósito: R3 dice que los kilos nunca pueden
    /// quedar sin poder corregirse. Por eso la guarda mira sólo el valor que se quiere ESCRIBIR.
    /// </para>
    /// </summary>
    private static void GuardarMarcaProximoCicloApagada(bool paraProximoCiclo)
    {
        if (!paraProximoCiclo) return;
        throw new InvalidOperationException(
            "La marca «para el próximo ciclo» está deshabilitada mientras se rediseña la atribución "
            + "del alimento entre ciclos: hoy dejaría kilos reales fuera de toda tabla diaria. "
            + "Registre el ingreso con su fecha real; quitar una marca ya existente sigue permitido.");
    }
}
