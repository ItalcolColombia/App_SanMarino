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

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var createDto = MapToCreateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);

        var modelo = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (Fase 3 paso 2) ────────────
        // Colombia unifica con Ecuador/Panamá sobre el modelo B, pero a NIVEL GRANJA (id-mapping
        // catalogItemId→item_inventario_ecuador por código). Validación previa de stock B de TODOS
        // los ítems ANTES de persistir; guardado del seguimiento (+ ajuste de aves dentro de
        // CreateAsync) + descuento en UNA IDbContextTransaction. Si falta stock/ítem → throw por
        // ítem → rollback → NO se guarda. (Antes Fase 2: modelo A vía _farmInventoryConsumo.)
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && dto.Metadata != null)
        {
            var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, positivos); // lanza si falta (antes de persistir)

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            var createdCo = await _seguimientoDiarioService.CreateAsync(createDto);
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento lote levante #{createdCo.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                await _colombiaConsumoB.AplicarConsumoAsync(lote.GranjaId, positivos, refStr);
            }
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return MapToLevanteDto(createdCo);
        }

        var created = await _seguimientoDiarioService.CreateAsync(createDto);

        // Ecuador/Panamá: consumo por ítems en metadata (item_inventario_ecuador) → inventario_gestion.
        // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá descuentan del modelo B (flujo tolerante,
        // sin tx nueva). Para lotes Colombia se usó el bloque modelo A de arriba.
        if (_inventarioGestionService != null && dto.Metadata != null && modelo == ModeloInventarioConsumo.ModeloB)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                var refStr = $"Seguimiento lote levante #{created.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                foreach (var kv in byItem)
                    if (kv.Value > 0)
                        await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null));
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al registrar consumo inventario (levante)"); }
        }

        // Feature 13 (refinamiento): el descuento de aves manual (mort+sel+err) sobre
        // LotePosturaLevante ahora está centralizado dentro de SeguimientoDiarioService.CreateAsync
        // — se aplica tanto en alta nueva como en merge sobre traslado. Ya no se repite aquí.

        return MapToLevanteDto(created);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.Lotes.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote '{dto.LoteId}' no existe o no pertenece a la compañía.");

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

        var (kcalAveH, protAveH) = CalcularDerivados(consumoKgH, kcalAlH, protAlH);
        var updateDto = MapToUpdateUnificado(dto, consumoKgH, kcalAlH, protAlH, kcalAveH, protAveH);

        var modelo = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (Fase 3 paso 2) ──
        // diff old/new por catalogItemId (id-mapping A→B): diff>0 = consumo adicional; diff<0 = devolución.
        // Validación previa del stock B de los diff POSITIVOS ANTES de persistir; update + diff +
        // ajuste de aves envueltos en UNA tx (todo-o-nada). Si falta stock → rollback, NO se guarda.
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null)
        {
            var newByItemId = dto.Metadata != null ? ParseMetadataItemsToKg(dto.Metadata.RootElement) : new Dictionary<int, decimal>();
            var incrementos = new Dictionary<int, decimal>();
            var allIds = new HashSet<int>(oldByItemId.Keys);
            foreach (var k in newByItemId.Keys) allIds.Add(k);
            foreach (var id in allIds)
            {
                var diff = newByItemId.GetValueOrDefault(id) - oldByItemId.GetValueOrDefault(id);
                if (diff > 0) incrementos[id] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, incrementos); // lanza si falta (antes de persistir)

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            var updatedCo = await _seguimientoDiarioService.UpdateAsync(updateDto);
            if (updatedCo is null) { await tx.RollbackAsync(); return null; }

            var refCo = $"Seguimiento lote levante #{dto.Id} {dto.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(lote.GranjaId, oldByItemId, newByItemId, refCo);

            var newHCo = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
            var newMCo = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
            var deltaHCo = oldH - newHCo;
            var deltaMCo = oldM - newMCo;
            if (deltaHCo != 0 || deltaMCo != 0)
                await AjustarAvesEnLotePosturaLevanteAsync(dto.LoteId, dto.LotePosturaLevanteId, deltaHCo, deltaMCo);

            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return MapToLevanteDto(updatedCo);
        }

        var updated = await _seguimientoDiarioService.UpdateAsync(updateDto);
        if (updated is null) return null;

        // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá ajustan el modelo B (flujo tolerante).
        if (_inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0) &&
            modelo == ModeloInventarioConsumo.ModeloB)
        {
            try
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
                        await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                            farmId, nucleoId, galponId, itemId, diff, "kg", refStr + " (ajuste)", null));
                    else if (diff < 0)
                        await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            farmId, nucleoId, galponId, itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento lote levante"));
                }
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al actualizar inventario (levante)"); }
        }

        var newH = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
        var newM = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
        var deltaH = oldH - newH;
        var deltaM = oldM - newM;
        if (deltaH != 0 || deltaM != 0)
        {
            try
            {
                await AjustarAvesEnLotePosturaLevanteAsync(dto.LoteId, dto.LotePosturaLevanteId, deltaH, deltaM);
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al ajustar aves en lote postura levante (actualización)"); }
        }

        return MapToLevanteDto(updated);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var rec = await _seguimientoDiarioService.GetByIdAsync((long)id);
        if (rec == null || rec.TipoSeguimiento != TipoLevante)
            return await _seguimientoDiarioService.DeleteAsync((long)id);

        int? loteIdInt = int.TryParse(rec.LoteId, out var lid) ? lid : null;
        var loteRow = loteIdInt.HasValue
            ? await _ctx.Lotes.AsNoTracking()
                .Where(l => l.LoteId == loteIdInt.Value && l.CompanyId == _current.CompanyId && l.DeletedAt == null)
                .Select(l => new { l.GranjaId, l.NucleoId, l.GalponId, l.PaisId })
                .FirstOrDefaultAsync()
            : null;
        var modelo = loteRow != null
            ? InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(loteRow.GranjaId, loteRow.PaisId))
            : ModeloInventarioConsumo.Ninguno;

        var hembras = (rec.MortalidadHembras ?? 0) + (rec.SelH ?? 0) + (rec.ErrorSexajeHembras ?? 0);
        var machos = (rec.MortalidadMachos ?? 0) + (rec.SelM ?? 0) + (rec.ErrorSexajeMachos ?? 0);

        // ── Colombia (modelo B nivel granja) — devolución total + restauración de aves + borrado, ATÓMICO ──
        if (modelo == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && loteRow != null)
        {
            var byItem = rec.Metadata != null ? ParseMetadataItemsToKg(rec.Metadata.RootElement) : new Dictionary<int, decimal>();
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento lote levante #{id} (devolución por eliminación)";
                await _colombiaConsumoB.AplicarDevolucionAsync(loteRow.GranjaId, positivos, refStr, "Devolución por eliminación de seguimiento lote levante");
            }
            if ((hembras > 0 || machos > 0) && loteIdInt.HasValue)
                await AjustarAvesEnLotePosturaLevanteAsync(loteIdInt.Value, rec.LotePosturaLevanteId, hembras, machos);

            var okCo = await _seguimientoDiarioService.DeleteAsync((long)id);
            if (!okCo) { await tx.RollbackAsync(); return false; }
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
            return true;
        }

        // Ecuador/Panamá (modelo B) y resto — flujo tolerante (sin tx nueva), como antes.
        if (_inventarioGestionService != null && rec.Metadata != null && modelo == ModeloInventarioConsumo.ModeloB && loteRow != null)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(rec.Metadata.RootElement);
                var refStr = $"Seguimiento lote levante #{id} (devolución por eliminación)";
                foreach (var kv in byItem)
                    if (kv.Value > 0)
                        await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            loteRow.GranjaId, loteRow.NucleoId?.Trim(), loteRow.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento lote levante"));
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al devolver inventario al eliminar seguimiento levante"); }
        }

        if ((hembras > 0 || machos > 0) && loteIdInt.HasValue)
        {
            try
            {
                await AjustarAvesEnLotePosturaLevanteAsync(loteIdInt.Value, rec.LotePosturaLevanteId, hembras, machos);
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al restaurar aves al eliminar seguimiento levante"); }
        }
        return await _seguimientoDiarioService.DeleteAsync((long)id);
    }
}
