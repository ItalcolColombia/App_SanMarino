// Alta/edición/baja del Seguimiento Diario Levante, incluyendo el gate de inventario por país
// (Colombia modelo B nivel granja — bloqueo atómico / Ecuador-Panamá modelo B — flujo tolerante),
// el cálculo de consumo por gramaje y el ajuste de aves en lote_postura_levante en edición/baja.
// Partial de SeguimientoLoteLevanteService (namespace plano).
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoLoteLevanteService
{
    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // REQ-006: bloqueo backend — el guard antes era solo UI; un request directo editaba lotes cerrados.
        await EnsureLoteLevanteAbiertoAsync(dto.LoteId, dto.LotePosturaLevanteId);

        // Corte de etapa: ese día no puede aportar consumo/bajas también desde producción (K345).
        await EnsureDiaSinAporteDeProduccionAsync(dto);

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // Con la empresa en doble validación no se descuenta al guardar: se separa. Con el flag
        // apagado `separa` queda en false y el método corre exactamente como antes.
        var separa = _validacion is not null
                  && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separa)
        {
            await _validacion!.AsegurarPuedeRegistrarDiaAsync(
                ModuloSeguimiento.Levante, dto.LotePosturaLevanteId ?? dto.LoteId);
            // Postura nunca es mixta: el alimento va por sexo (hembras y/o machos).
            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Levante, loteEsMixto: false, dto.Metadata, dto.FechaRegistro,
                (decimal)dto.ConsumoKgHembras, (decimal)(dto.ConsumoKgMachos ?? 0));
        }

        // Huevos en levante (semana 14+): gate por flag de empresa + edad del lote. Neutraliza o
        // lanza ANTES de tocar inventario/consumo, para no dejar efectos a medias.
        dto = await AplicarGateHuevosLevanteAsync(dto, lote);

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        // REQ-011b (soft-check, no bloquea): advierte si hay consumo/mortalidad de un sexo sin saldo a esa fecha.
        await ValidarConsumoVsSaldoPorSexoAsync(dto, consumoKgH);

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var createDto = MapToCreateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);

        // El país va RESUELTO una sola vez y se reusa: lo consume el gate del descuento Y la reserva de
        // la doble validación. `lote.PaisId` crudo puede venir NULL (K345A/K345B de Sanmarino lo están)
        // y una reserva con país 0 se aplica contra el modelo `Ninguno`: el registro queda validado sin
        // que se descuente un kilo.
        var paisIdLote = await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId);
        var modelo = InventarioConsumoGate.ResolverModelo(paisIdLote);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (Fase 3 paso 2) ────────────
        // Colombia unifica con Ecuador/Panamá sobre el modelo B, pero a NIVEL GRANJA (id-mapping
        // catalogItemId→item_inventario_ecuador por código). Validación previa de stock B de TODOS
        // los ítems ANTES de persistir; guardado del seguimiento (+ ajuste de aves dentro de
        // CreateAsync) + descuento en UNA IDbContextTransaction. Si falta stock/ítem → throw por
        // ítem → rollback → NO se guarda. (Antes Fase 2: modelo A vía _farmInventoryConsumo.)
        if (!separa && modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && dto.Metadata != null)
        {
            var byItem = ParseMetadataItemsToKgPorOrigen(dto.Metadata.RootElement);
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, positivos, lote.LoteId); // lanza si falta (antes de persistir)

            // Transacción CONDICIONAL: `null` cuando ya hay una ambiente (push offline de la PWA),
            // porque EF lanza si se abre una segunda sobre el mismo contexto. Llamado desde el
            // controller no hay ambiente ⇒ abre la suya y el comportamiento es idéntico al de antes.
            // Lo que esto habilita: que el registro de idempotencia del push y este efecto commiteen
            // juntos. Si no comparten transacción queda una ventana en la que el efecto se aplicó y
            // la marca no, y el reintento vuelve a aplicar.
            await using var tx = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;
            var createdCo = await _seguimientoDiarioService.CreateAsync(createDto);
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento lote levante #{createdCo.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                await _colombiaConsumoB.AplicarConsumoAsync(lote.GranjaId, positivos, refStr, fechaMovimiento: dto.FechaRegistro);
            }
            await _ctx.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();
            return MapToLevanteDto(createdCo);
        }

        // Ecuador/Panamá: consumo por ítems en metadata (item_inventario_ecuador) → inventario_gestion.
        // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá descuentan del modelo B. Para lotes Colombia
        // se usó el bloque modelo A de arriba.
        var consumeModeloB = !separa && _inventarioGestionService != null && dto.Metadata != null
            && modelo == ModeloInventarioConsumo.ModeloB;

        // El stock se comprueba ANTES de persistir, igual que Colombia. Lanza con el ítem y el
        // faltante; el controller lo devuelve como 400.
        if (consumeModeloB)
            await _inventarioGestionService!.ValidarStockConsumoAsync(
                lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(),
                ParseMetadataItemsToKg(dto.Metadata!.RootElement));

        SeguimientoDiarioDto created;
        if (consumeModeloB)
        {
            // F3 (22-ago-2026): antes el registro se guardaba primero y el consumo iba después dentro
            // de un catch que sólo logueaba — día guardado, inventario intacto, 200 OK. En el móvil eso
            // es PERMANENTE: el push de sync commitea el efecto y la marca de idempotencia juntos, saca
            // la operación del outbox y no reintenta nunca; el faltante queda invisible para siempre.
            // Ahora adopta la forma que Colombia ya tiene arriba: transacción CONDICIONAL (null si ya
            // hay una ambiente — el push offline) envolviendo el guardado del seguimiento Y el consumo,
            // sin try/catch: si algo falla, la excepción sube, el controller la traduce a 400, y el
            // `await using` deshace TODO —incluido el seguimiento— porque nunca llega al Commit.
            //
            // Sólo se abre cuando realmente hay consumo ModeloB que proteger: envolver TODO alta en una
            // transacción —incluidas las que no tocan inventario— sería alcance más ancho del que F3
            // pide, aunque inofensivo en la práctica.
            await using var txEcPa = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;
            created = await _seguimientoDiarioService.CreateAsync(createDto);

            var byItem = ParseMetadataItemsToKg(dto.Metadata!.RootElement);
            var refStr = $"Seguimiento lote levante #{created.Id} {dto.FechaRegistro:yyyy-MM-dd}";
            foreach (var kv in byItem)
                if (kv.Value > 0)
                    await _inventarioGestionService!.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                        lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null, FechaMovimiento: dto.FechaRegistro));

            if (txEcPa is not null) await txEcPa.CommitAsync();
        }
        else
        {
            created = await _seguimientoDiarioService.CreateAsync(createDto);
        }

        // Feature 13 (refinamiento): el descuento de aves manual (mort+sel+err) sobre
        // LotePosturaLevante ahora está centralizado dentro de SeguimientoDiarioService.CreateAsync
        // — se aplica tanto en alta nueva como en merge sobre traslado. Ya no se repite aquí.

        // Con separación, ese descuento centralizado ya se saltó (SeguimientoDiarioService consulta el
        // mismo flag) y acá solo queda registrar la reserva.
        if (separa)
        {
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Levante, created.Id, paisIdLote,
                lote.GranjaId, lote.NucleoId, lote.GalponId,
                dto.LotePosturaLevanteId ?? dto.LoteId, lote.LoteNombre, dto.FechaRegistro, dto.Metadata,
                dto.MortalidadHembras, dto.SelH, dto.ErrorSexajeHembras,
                dto.MortalidadMachos, dto.SelM, dto.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        return MapToLevanteDto(created);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

        // REQ-006: bloqueo backend — el guard antes era solo UI; un request directo editaba lotes cerrados.
        await EnsureLoteLevanteAbiertoAsync(dto.LoteId, dto.LotePosturaLevanteId);

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        var separa = _validacion is not null
                  && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separa)
        {
            var yaValidado = await _ctx.SeguimientoDiario.AsNoTracking()
                .Where(sd => sd.Id == dto.Id).Select(sd => sd.Validado).FirstOrDefaultAsync();
            if (!ValidacionSeguimientoCalculos.EsEditable(true, yaValidado))
                throw new InvalidOperationException(
                    ValidacionSeguimientoCalculos.MensajeRegistroValidado("editar"));

            SeparacionSeguimientoHelper.ValidarAlimentoObligatorio(
                ModuloSeguimiento.Levante, loteEsMixto: false, dto.Metadata, dto.FechaRegistro,
                (decimal)dto.ConsumoKgHembras, (decimal)(dto.ConsumoKgMachos ?? 0));
        }

        // Huevos en levante (semana 14+): mismo gate que en el alta.
        dto = await AplicarGateHuevosLevanteAsync(dto, lote);

        double? kcalAlH = dto.KcalAlH, protAlH = dto.ProtAlH;
        if (kcalAlH is null || protAlH is null)
        {
            var np = await _alimentos.GetNutrientesAsync(dto.TipoAlimento);
            if (np.HasValue) { kcalAlH ??= np.Value.kcal; protAlH ??= np.Value.prot; }
        }

        double consumoKgH = dto.ConsumoKgHembras;
        if (consumoKgH <= 0 && !string.IsNullOrWhiteSpace(lote.GalponId) && lote.FechaEncaset.HasValue)
        {
            int semana = CalcularSemana(lote.FechaEncaset.Value, dto.FechaRegistro);
            double? gramajeGrAve = null;
            if (int.TryParse(lote.GalponId, out var galponIdInt))
                gramajeGrAve = await _gramaje.GetGramajeGrPorAveAsync(galponIdInt, semana, dto.TipoAlimento);
            else if (_gramaje is IGramajeProviderV2 v2)
                gramajeGrAve = await v2.GetGramajeGrPorAveAsync(lote.GalponId, semana, dto.TipoAlimento);
            if (gramajeGrAve.HasValue && gramajeGrAve.Value > 0)
            {
                int hembrasVivas = await CalcularHembrasVivasAsync(dto.LoteId);
                consumoKgH = Math.Round((gramajeGrAve.Value * hembrasVivas) / 1000.0, 3);
            }
        }

        var oldRec = await _seguimientoDiarioService.GetByIdAsync((long)dto.Id);
        var oldH = (oldRec?.MortalidadHembras ?? 0) + (oldRec?.SelH ?? 0) + (oldRec?.ErrorSexajeHembras ?? 0);
        var oldM = (oldRec?.MortalidadMachos ?? 0) + (oldRec?.SelM ?? 0) + (oldRec?.ErrorSexajeMachos ?? 0);
        var oldByItemId = oldRec?.Metadata != null ? ParseMetadataItemsToKg(oldRec.Metadata.RootElement) : new Dictionary<int, decimal>();

        // REQ-011b (soft-check, no bloquea): advierte si hay consumo/mortalidad de un sexo sin saldo a esa
        // fecha; excluye el propio registro (edición) para no auto-justificarse.
        await ValidarConsumoVsSaldoPorSexoAsync(dto, consumoKgH, excludeRegistroId: (long)dto.Id);

        // Los huevos que el request no trae se CONSERVAN: SeguimientoDiarioService.UpdateAsync
        // asigna las 13 columnas sin condición, así que mandar null equivale a borrarlas. Sin esto,
        // editar un registro desde un cliente que no manda el tab «Huevos» (o con el flag apagado
        // después de haber capturado datos) borraría los huevos en silencio.
        dto = ConservarHuevosPrevios(dto, oldRec);

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var updateDto = MapToUpdateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);

        // Ídem el alta: país resuelto una sola vez, para el gate y para la reserva.
        var paisIdLote = await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId);
        var modelo = InventarioConsumoGate.ResolverModelo(paisIdLote);

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (Fase 3 paso 2) ──
        // diff old/new por catalogItemId (id-mapping A→B): diff>0 = consumo adicional; diff<0 = devolución.
        // Validación previa del stock B de los diff POSITIVOS ANTES de persistir; update + diff +
        // ajuste de aves envueltos en UNA tx (todo-o-nada). Si falta stock → rollback, NO se guarda.
        if (!separa && modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null)
        {
            // Parseo TIPADO (conserva el origen del id, camino 1/2) — el diff plano de arriba
            // (oldByItemId) sigue siendo el de la rama Ecuador/Panamá.
            var oldByItemCo = oldRec?.Metadata != null ? ParseMetadataItemsToKgPorOrigen(oldRec.Metadata.RootElement) : new Dictionary<ItemConsumoKey, decimal>();
            var newByItemCo = dto.Metadata != null ? ParseMetadataItemsToKgPorOrigen(dto.Metadata.RootElement) : new Dictionary<ItemConsumoKey, decimal>();
            var incrementos = new Dictionary<ItemConsumoKey, decimal>();
            var allKeys = new HashSet<ItemConsumoKey>(oldByItemCo.Keys);
            foreach (var k in newByItemCo.Keys) allKeys.Add(k);
            foreach (var key in allKeys)
            {
                var diff = newByItemCo.GetValueOrDefault(key) - oldByItemCo.GetValueOrDefault(key);
                if (diff > 0) incrementos[key] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, incrementos, lote.LoteId); // lanza si falta (antes de persistir)

            // Transacción condicional — ver la nota en CreateAsync.
            await using var tx = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;
            var updatedCo = await _seguimientoDiarioService.UpdateAsync(updateDto);
            // Con transacción ambiente el rollback es del llamador: revertirla acá abortaría también
            // las operaciones sanas del mismo lote.
            if (updatedCo is null) { if (tx is not null) await tx.RollbackAsync(); return null; }

            var refCo = $"Seguimiento lote levante #{dto.Id} {dto.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(lote.GranjaId, oldByItemCo, newByItemCo, refCo, fechaMovimiento: dto.FechaRegistro);

            // A7 — el ajuste del saldo de aves ya NO se hace acá: lo aplica
            // SeguimientoDiarioService.UpdateAsync (llamado arriba), igual que para producción.
            // Antes vivía en este módulo, y por eso editar el mismo registro desde
            // PUT /api/SeguimientoDiario o desde LoteSeguimiento dejaba el saldo intacto.
            // Dejarlo acá además lo descontaría DOS veces.

            await _ctx.SaveChangesAsync();
            // `tx` es null cuando ya había una transacción ambiente (push offline de la PWA): commitear
            // sin preguntar tiraba NullReference justo en ese camino. Los otros dos commits de este
            // archivo ya tenían la guarda; a este se le había pasado.
            if (tx is not null) await tx.CommitAsync();
            return MapToLevanteDto(updatedCo);
        }

        // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá ajustan el modelo B.
        var ajustaModeloB = !separa && _inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0) &&
            modelo == ModeloInventarioConsumo.ModeloB;

        // Igual que en el alta: los INCREMENTOS de consumo se comprueban contra el stock ANTES de
        // persistir. Solo los diff positivos consumen; una edición a la baja devuelve y nunca puede
        // faltar stock para devolver.
        if (ajustaModeloB)
        {
            var nuevosPre = dto.Metadata != null ? ParseMetadataItemsToKg(dto.Metadata.RootElement) : new Dictionary<int, decimal>();
            var incrementos = new Dictionary<int, decimal>();
            foreach (var itemId in new HashSet<int>(oldByItemId.Keys.Concat(nuevosPre.Keys)))
            {
                var diff = nuevosPre.GetValueOrDefault(itemId) - oldByItemId.GetValueOrDefault(itemId);
                if (diff > 0) incrementos[itemId] = diff;
            }
            await _inventarioGestionService!.ValidarStockConsumoAsync(
                lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(), incrementos);
        }

        SeguimientoDiarioDto? updated;
        if (ajustaModeloB)
        {
            // F3 (22-ago-2026): mismo cambio que en CreateAsync — antes el ajuste de inventario iba
            // dentro de un catch que sólo logueaba, así que una edición podía dejar el día actualizado
            // con el inventario a medio ajustar. Transacción condicional envolviendo la edición Y el
            // ajuste, sin try/catch: si algo falla, sube y deshace los dos.
            await using var txEcPaUpd = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;

            updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
            if (updated is not null)
            {
                var newByItemId = dto.Metadata != null ? ParseMetadataItemsToKg(dto.Metadata.RootElement) : new Dictionary<int, decimal>();
                var allItemIds = new HashSet<int>(oldByItemId.Keys);
                foreach (var k in newByItemId.Keys) allItemIds.Add(k);
                var refStr = $"Seguimiento lote levante #{dto.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                var farmId = lote.GranjaId;
                var nucleoId = lote.NucleoId?.Trim();
                var galponId = lote.GalponId?.Trim();
                foreach (var itemId in allItemIds)
                {
                    var newQty = newByItemId.GetValueOrDefault(itemId);
                    var oldQty = oldByItemId.GetValueOrDefault(itemId);
                    var diff = newQty - oldQty;
                    if (diff > 0)
                        await _inventarioGestionService!.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            farmId, nucleoId, galponId, itemId, diff, "kg", refStr + " (ajuste)", null, FechaMovimiento: dto.FechaRegistro));
                    else if (diff < 0)
                        await _inventarioGestionService!.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            farmId, nucleoId, galponId, itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento lote levante", FechaMovimiento: dto.FechaRegistro));
                }
            }
            if (txEcPaUpd is not null) await txEcPaUpd.CommitAsync();
        }
        else
        {
            updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
        }
        if (updated is null) return null;

        // Editar un pendiente REESCRIBE la separación: nada que devolver, porque nunca se descontó.
        if (separa)
        {
            await _validacion!.SepararAsync(SeparacionSeguimientoHelper.Contexto(
                ModuloSeguimiento.Levante, dto.Id, paisIdLote,
                lote.GranjaId, lote.NucleoId, lote.GalponId,
                dto.LotePosturaLevanteId ?? dto.LoteId, lote.LoteNombre, dto.FechaRegistro, dto.Metadata,
                dto.MortalidadHembras, dto.SelH, dto.ErrorSexajeHembras,
                dto.MortalidadMachos, dto.SelM, dto.ErrorSexajeMachos,
                poblacionEsMixta: false));
        }

        // A7 — el ajuste del saldo lo hace SeguimientoDiarioService.UpdateAsync (ver arriba).
        return MapToLevanteDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var rec = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (rec == null || rec.TipoSeguimiento != TipoLevante)
            return await _seguimientoDiarioService.DeleteAsync((long)id);

        int? loteIdInt = int.TryParse(rec.LoteId, out var lid) ? lid : null;

        // ── Doble validación ───────────────────────────────────────────────────────────────────
        // Borrar un pendiente solo libera la separación: el inventario y el saldo nunca se movieron.
        var separaDel = _validacion is not null
                     && ValidacionSeguimientoCalculos.SeparaAlGuardar(await _validacion.RequiereValidacionAsync());
        if (separaDel)
        {
            var yaValidado = await _ctx.SeguimientoDiario.AsNoTracking()
                .Where(sd => sd.Id == id).Select(sd => sd.Validado).FirstOrDefaultAsync();
            if (!ValidacionSeguimientoCalculos.EsEditable(true, yaValidado))
                throw new InvalidOperationException(
                    ValidacionSeguimientoCalculos.MensajeRegistroValidado("eliminar"));

            await _validacion!.LiberarAsync(ModuloSeguimiento.Levante, id);
        }

        // REQ-006: bloqueo backend — no permitir eliminar seguimiento de un lote de levante cerrado.
        if (loteIdInt.HasValue)
            await EnsureLoteLevanteAbiertoAsync(loteIdInt.Value, rec.LotePosturaLevanteId);

        var loteRow = loteIdInt.HasValue
            ? await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteIdInt.Value && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId, l.PaisId })
                .FirstOrDefaultAsync()
            : null;
        var modelo = loteRow != null && !separaDel
            ? InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(loteRow.GranjaId, loteRow.PaisId))
            : ModeloInventarioConsumo.Ninguno;

        var hembras = (rec.MortalidadHembras ?? 0) + (rec.SelH ?? 0) + (rec.ErrorSexajeHembras ?? 0);
        var machos = (rec.MortalidadMachos ?? 0) + (rec.SelM ?? 0) + (rec.ErrorSexajeMachos ?? 0);

        // ── Colombia (modelo B nivel granja) — devolución total + restauración de aves + borrado, ATÓMICO ──
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && loteRow != null)
        {
            var byItem = rec.Metadata != null ? ParseMetadataItemsToKgPorOrigen(rec.Metadata.RootElement) : new Dictionary<ItemConsumoKey, decimal>();
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            // Transacción condicional — ver la nota en CreateAsync.
            await using var tx = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento lote levante #{id} (devolución por eliminación)";
                // Devolución por ELIMINACIÓN: se fecha con el día del borrado (hecho de HOY), no con la
                // fecha del seguimiento original que se está borrando.
                await _colombiaConsumoB.AplicarDevolucionAsync(loteRow.GranjaId, positivos, refStr, "Devolución por eliminación de seguimiento lote levante", fechaMovimiento: DateTime.UtcNow.Date);
            }
            // A7 — la devolución de aves la hace SeguimientoDiarioService.DeleteAsync, dentro de
            // esta misma transacción.
            var okCo = await _seguimientoDiarioService.DeleteAsync((long)id);
            if (!okCo) { if (tx is not null) await tx.RollbackAsync(); return false; }
            await _ctx.SaveChangesAsync();
            if (tx is not null) await tx.CommitAsync();
            return true;
        }

        // Ecuador/Panamá (modelo B).
        //
        // F3 (22-ago-2026): antes esto era «flujo tolerante» — un try/catch que se comía el fallo de
        // la devolución y DEJABA BORRAR el seguimiento igual (la línea de DeleteAsync de abajo corría
        // sin condición). Eso es peor que en alta/edición: el consumo desaparecía del registro, pero
        // el stock nunca volvía — la evidencia se borraba y el inventario quedaba corto sin rastro de
        // por qué. Ahora, mismo patrón que Colombia arriba: transacción condicional envolviendo
        // devolución + borrado, sin try/catch. Si la devolución falla, el `await using` deshace todo y
        // el seguimiento SIGUE existiendo — que es preferible a borrar la evidencia de un consumo que
        // nunca se devolvió.
        if (_inventarioGestionService != null && rec.Metadata != null && modelo == ModeloInventarioConsumo.ModeloB && loteRow != null)
        {
            var byItem = ParseMetadataItemsToKg(rec.Metadata.RootElement);
            var refStr = $"Seguimiento lote levante #{id} (devolución por eliminación)";

            await using var txDelEcPa = _ctx.Database.CurrentTransaction is null
                ? await _ctx.Database.BeginTransactionAsync()
                : null;
            foreach (var kv in byItem)
                if (kv.Value > 0)
                    // Devolución por ELIMINACIÓN: se fecha con el día del borrado (hecho de HOY), no
                    // con la fecha del seguimiento original que se está borrando.
                    await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                        loteRow.GranjaId, loteRow.NucleoId?.Trim(), loteRow.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento lote levante", FechaMovimiento: DateTime.UtcNow.Date));

            var okEcPa = await _seguimientoDiarioService.DeleteAsync((long)id);
            if (!okEcPa) { if (txDelEcPa is not null) await txDelEcPa.RollbackAsync(); return false; }
            if (txDelEcPa is not null) await txDelEcPa.CommitAsync();
            return true;
        }

        // A7 — la devolución de aves la hace SeguimientoDiarioService.DeleteAsync.
        return await _seguimientoDiarioService.DeleteAsync((long)id);
    }

    /// <summary>
    /// REQ-006: bloqueo backend de edición sobre lote de levante cerrado (antes el guard era solo UI —
    /// ver seguimiento-lote-levante-list.component.ts:163-166,888 — y un request directo a la API podía
    /// editar/borrar registros de un lote ya cerrado). Mismo criterio que
    /// LotePosturaLevanteService.cs:335 (CloseAsync): EstadoCierre == "Cerrado" (case-insensitive).
    /// Resuelve el LotePosturaLevante por Id si viene informado; si no, por LoteId. Si no se encuentra
    /// el registro de levante no bloquea (no hay estado de cierre que validar). Solo aplica a Levante;
    /// Producción lo cubre otro módulo.
    /// </summary>
    private async Task EnsureLoteLevanteAbiertoAsync(int loteId, int? lotePosturaLevanteId)
    {
        var lev = lotePosturaLevanteId.HasValue
            ? await _ctx.LotePosturaLevante.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId.Value && l.DeletedAt == null)
            : await _ctx.LotePosturaLevante.AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId && l.DeletedAt == null);

        var estado = (lev?.EstadoCierre ?? "").Trim();
        if (string.Equals(estado, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote de levante está cerrado; no se pueden crear, modificar ni eliminar registros de seguimiento diario.");
    }

    /// <summary>
    /// Corte de etapa: bloquea el alta de un día de LEVANTE cuando producción ya registró ese mismo
    /// día del mismo lote CON consumo o bajas. No basta con que exista la fila: el arrastre de huevos
    /// del levante crea legítimamente filas de producción de solo huevos, y esas no molestan. Lo que
    /// se impide es el doble conteo — el caso K345, donde 14 días de julio-2025 quedaron en las dos
    /// tablas con el mismo consumo (16.952 kg y 10 aves contados dos veces por cualquier reporte que
    /// sume el ciclo). Ver <see cref="CorteEtapaPosturaCalculos"/>.
    /// </summary>
    private async Task EnsureDiaSinAporteDeProduccionAsync(SeguimientoLoteLevanteDto dto)
    {
        var (desde, hasta) = FechasPuras.RangoDiaUtc(dto.FechaRegistro);

        var otra = await _ctx.SeguimientoProduccion.AsNoTracking()
            .Where(s => s.LoteId == dto.LoteId && s.Fecha >= desde && s.Fecha < hasta && s.DeletedAt == null)
            .Select(s => new
            {
                Consumo = s.ConsKgH + s.ConsKgM,
                Mortalidad = s.MortalidadH + s.MortalidadM,
                Seleccion = s.SelH + s.SelM
            })
            .FirstOrDefaultAsync();

        if (otra is null) return;

        var nuevo = new CorteEtapaPosturaCalculos.AporteDia(
            (decimal)dto.ConsumoKgHembras + (decimal)(dto.ConsumoKgMachos ?? 0),
            dto.MortalidadHembras + dto.MortalidadMachos,
            dto.SelH + dto.SelM);

        var existente = new CorteEtapaPosturaCalculos.AporteDia(otra.Consumo, otra.Mortalidad, otra.Seleccion);

        if (CorteEtapaPosturaCalculos.HayDobleConteo(nuevo, existente))
            throw new InvalidOperationException(
                CorteEtapaPosturaCalculos.MensajeLevanteChocaConProduccion(dto.FechaRegistro));
    }

    /// <summary>
    /// REQ-011b (soft-check, NO bloqueo duro): advierte cuando se registra consumo/mortalidad/selección
    /// de un sexo con saldo 0 a la fecha del registro — señal de lote poblado solo por traslado (auto-
    /// consumo/mortalidad calculado sobre una base que la aritmética de saldo no ve) o de una fecha de
    /// registro fuera del rango real del lote. Implementado como advertencia en el log (no en el DTO de
    /// respuesta, que es un record inmutable compartido por otros módulos) para no romper ajustes
    /// retroactivos legítimos con un error duro.
    /// </summary>
    private async Task ValidarConsumoVsSaldoPorSexoAsync(SeguimientoLoteLevanteDto dto, double consumoKgH, long? excludeRegistroId = null)
    {
        try
        {
            var huboMovH = consumoKgH > 0 || dto.MortalidadHembras > 0 || dto.SelH > 0;
            var huboMovM = (dto.ConsumoKgMachos ?? 0) > 0 || dto.MortalidadMachos > 0 || dto.SelM > 0;
            if (!huboMovH && !huboMovM) return;

            var (saldoH, saldoM) = await CalcularSaldoPorSexoAFechaAsync(dto.LoteId, dto.FechaRegistro, excludeRegistroId);

            if (huboMovH && saldoH == 0)
                _logger?.LogWarning(
                    "REQ-011b: seguimiento lote levante {LoteId} fecha {Fecha:yyyy-MM-dd} registra consumo/mortalidad/selección de HEMBRAS (consumoKgH={ConsumoKgH}, mortH={MortH}, selH={SelH}) con saldo de hembras = 0 a esa fecha. Posible lote poblado solo por traslado o fecha de registro fuera de rango.",
                    dto.LoteId, dto.FechaRegistro, consumoKgH, dto.MortalidadHembras, dto.SelH);

            if (huboMovM && saldoM == 0)
                _logger?.LogWarning(
                    "REQ-011b: seguimiento lote levante {LoteId} fecha {Fecha:yyyy-MM-dd} registra consumo/mortalidad/selección de MACHOS (consumoKgM={ConsumoKgM}, mortM={MortM}, selM={SelM}) con saldo de machos = 0 a esa fecha. Posible lote poblado solo por traslado o fecha de registro fuera de rango.",
                    dto.LoteId, dto.FechaRegistro, dto.ConsumoKgMachos ?? 0, dto.MortalidadMachos, dto.SelM);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error al validar saldo por sexo (REQ-011b, soft-check) en seguimiento lote levante {LoteId}", dto.LoteId);
        }
    }
}
