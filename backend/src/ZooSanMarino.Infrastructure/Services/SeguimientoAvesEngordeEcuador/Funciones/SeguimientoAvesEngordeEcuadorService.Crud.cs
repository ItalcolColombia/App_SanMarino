// Alta/edición/baja del seguimiento diario de aves engorde Ecuador, con afectación de inventario
// (gate por país: Colombia modelo B nivel granja / Ecuador-Panamá modelo B), snapshot de consumo,
// retiro/devolución de aves y recálculo de saldo de alimento.
// Partial de SeguimientoAvesEngordeEcuadorService.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeEcuadorService
{
    // ─── CRUD con afectación de inventario y recálculo de saldo ──────────────

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l =>
                l.LoteAveEngordeId == dto.LoteId
                && l.CompanyId == companyId
                && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se pueden agregar registros diarios.");

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

        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForEntity = MergeMetadataWithPatch(dto.Metadata, stockPatch);
        var historicoConsumo = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId);

        var ent = new SeguimientoDiarioAvesEngorde
        {
            LoteAveEngordeId = dto.LoteId,
            Fecha = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro),
            MortalidadHembras = dto.MortalidadHembras,
            MortalidadMachos = dto.MortalidadMachos,
            SelH = dto.SelH,
            SelM = dto.SelM,
            ErrorSexajeHembras = dto.ErrorSexajeHembras,
            ErrorSexajeMachos = dto.ErrorSexajeMachos,
            ConsumoKgHembras = (decimal)consumoKgH,
            ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento = dto.TipoAlimento,
            Observaciones = dto.Observaciones,
            Ciclo = dto.Ciclo,
            PesoPromHembras = dto.PesoPromH,
            PesoPromMachos = dto.PesoPromM,
            UniformidadHembras = dto.UniformidadH,
            UniformidadMachos = dto.UniformidadM,
            CvHembras = dto.CvH,
            CvMachos = dto.CvM,
            ConsumoAguaDiario = dto.ConsumoAguaDiario,
            ConsumoAguaPh = dto.ConsumoAguaPh,
            ConsumoAguaOrp = dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura,
            Metadata = metadataForEntity,
            ItemsAdicionales = dto.ItemsAdicionales,
            KcalAlH = kcalAlH,
            ProtAlH = protAlH,
            KcalAveH = kcalAveH,
            ProtAveH = protAveH,
            CreatedByUserId = dto.CreatedByUserId ?? _current?.UserId.ToString(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            HistoricoConsumoAlimento = historicoConsumo
        };
        _ctx.SeguimientoDiarioAvesEngorde.Add(ent);

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (defensivo; mirror levante) ──
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && dto.Metadata != null)
        {
            var byItem = ParseMetadataItemsToKgPorOrigen(dto.Metadata.RootElement);
            var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, positivos); // lanza si falta (antes de persistir)

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            await _ctx.SaveChangesAsync();
            if (positivos.Count > 0)
            {
                var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                await _colombiaConsumoB.AplicarConsumoAsync(lote.GranjaId, positivos, refStr);
            }
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await _ctx.SaveChangesAsync();

            // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá descuentan del modelo B (con núcleo/galpón).
            if (_inventarioGestionService != null && dto.Metadata != null && modeloInv == ModeloInventarioConsumo.ModeloB)
            {
                try
                {
                    var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                    var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(),
                                kv.Key, kv.Value, "kg", refStr, null));
                }
                catch (Exception ex) { _logger?.LogError(ex, "Error al registrar consumo inventario (aves engorde Ecuador)"); }
            }
        }

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos
            + dto.SelH + dto.SelM
            + dto.ErrorSexajeHembras + dto.ErrorSexajeMachos;
        if (totalRetiradas > 0)
        {
            try
            {
                await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                    loteId: dto.LoteId,
                    hembrasRetiradas: dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras,
                    machosRetirados: dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos,
                    mixtasRetiradas: 0,
                    fechaMovimiento: dto.FechaRegistro,
                    fuenteSeguimiento: "Engorde",
                    observaciones: $"Aves de Engorde (Ecuador) - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | Error sexaje H: {dto.ErrorSexajeHembras}, M: {dto.ErrorSexajeMachos} | {dto.Observaciones}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde Ecuador: {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, companyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l =>
                l.LoteAveEngordeId == dto.LoteId
                && l.CompanyId == companyId
                && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede editar el registro.");

        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == dto.Id && l.CompanyId == companyId && l.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;
        if (ent.OrigenCruce)
            throw new InvalidOperationException(
                "Este registro se genera automáticamente desde los lotes reproductora (primeros 7 días). Para corregirlo, edite el seguimiento del lote reproductora.");

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

        var oldHRet = (ent.MortalidadHembras ?? 0) + (ent.SelH ?? 0) + (ent.ErrorSexajeHembras ?? 0);
        var oldMRet = (ent.MortalidadMachos ?? 0) + (ent.SelM ?? 0) + (ent.ErrorSexajeMachos ?? 0);
        var oldByItemId = ent.Metadata != null
            ? ParseMetadataItemsToKg(ent.Metadata.RootElement)
            : new Dictionary<int, decimal>();
        // Snapshot TIPADO del consumo anterior (conserva el origen del id, camino 1/2) para la rama
        // Colombia — hay que capturarlo AQUÍ, antes de pisar ent.Metadata con el nuevo.
        var oldByItemCo = ent.Metadata != null
            ? ParseMetadataItemsToKgPorOrigen(ent.Metadata.RootElement)
            : new Dictionary<ItemConsumoKey, decimal>();

        ent.Fecha = FechasPuras.AnclarMediodiaUtc(dto.FechaRegistro);
        ent.MortalidadHembras = dto.MortalidadHembras;
        ent.MortalidadMachos = dto.MortalidadMachos;
        ent.SelH = dto.SelH;
        ent.SelM = dto.SelM;
        ent.ErrorSexajeHembras = dto.ErrorSexajeHembras;
        ent.ErrorSexajeMachos = dto.ErrorSexajeMachos;
        ent.ConsumoKgHembras = (decimal)consumoKgH;
        ent.ConsumoKgMachos = dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null;
        ent.TipoAlimento = dto.TipoAlimento;
        ent.Observaciones = dto.Observaciones;
        ent.Ciclo = dto.Ciclo;
        ent.PesoPromHembras = dto.PesoPromH;
        ent.PesoPromMachos = dto.PesoPromM;
        ent.UniformidadHembras = dto.UniformidadH;
        ent.UniformidadMachos = dto.UniformidadM;
        ent.CvHembras = dto.CvH;
        ent.CvMachos = dto.CvM;
        ent.ConsumoAguaDiario = dto.ConsumoAguaDiario;
        ent.ConsumoAguaPh = dto.ConsumoAguaPh;
        ent.ConsumoAguaOrp = dto.ConsumoAguaOrp;
        ent.ConsumoAguaTemperatura = dto.ConsumoAguaTemperatura;

        var historicoConsumoUpdate = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId, oldByItemId);
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForSave = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        ent.Metadata = CloneJsonDocument(metadataForSave);
        ent.ItemsAdicionales = CloneJsonDocument(dto.ItemsAdicionales);
        ent.HistoricoConsumoAlimento = CloneJsonDocument(historicoConsumoUpdate);
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        _ctx.Entry(ent).Property(e => e.HistoricoConsumoAlimento).IsModified = true;

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));
        var newByItemIdInv = dto.Metadata != null
            ? ParseMetadataItemsToKg(dto.Metadata.RootElement)
            : new Dictionary<int, decimal>();

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (defensivo; mirror levante) ──
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null)
        {
            // Diff TIPADO (conserva el origen del id) — el diff plano (oldByItemId/newByItemIdInv)
            // sigue siendo el de la rama Ecuador/Panamá de abajo.
            var newByItemCo = dto.Metadata != null ? ParseMetadataItemsToKgPorOrigen(dto.Metadata.RootElement) : new Dictionary<ItemConsumoKey, decimal>();
            var incrementos = new Dictionary<ItemConsumoKey, decimal>();
            var allKeys = new HashSet<ItemConsumoKey>(oldByItemCo.Keys);
            foreach (var k in newByItemCo.Keys) allKeys.Add(k);
            foreach (var key in allKeys)
            {
                var diff = newByItemCo.GetValueOrDefault(key) - oldByItemCo.GetValueOrDefault(key);
                if (diff > 0) incrementos[key] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, incrementos); // lanza si falta (antes de persistir)

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            await _ctx.SaveChangesAsync();
            var refCo = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(lote.GranjaId, oldByItemCo, newByItemCo, refCo);
            await _ctx.SaveChangesAsync();
            await tx.CommitAsync();
        }
        else
        {
            await _ctx.SaveChangesAsync();

            // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá ajustan el modelo B (con núcleo/galpón).
            if (_inventarioGestionService != null && (dto.Metadata != null || oldByItemId.Count > 0) &&
                modeloInv == ModeloInventarioConsumo.ModeloB)
            {
                try
                {
                    var allItemIds = new HashSet<int>(oldByItemId.Keys);
                    foreach (var k in newByItemIdInv.Keys) allItemIds.Add(k);
                    var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    var farmId = lote.GranjaId;
                    var nucleoId = lote.NucleoId?.Trim();
                    var galponId = lote.GalponId?.Trim();
                    foreach (var itemId in allItemIds)
                    {
                        var newQty = newByItemIdInv.GetValueOrDefault(itemId);
                        var oldQty = oldByItemId.GetValueOrDefault(itemId);
                        var diff = newQty - oldQty;
                        if (diff > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                farmId, nucleoId, galponId, itemId, diff, "kg", refStr + " (ajuste)", null));
                        else if (diff < 0)
                            await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                                farmId, nucleoId, galponId, itemId, -diff, "kg",
                                refStr + " (devolución)", "Devolución desde seguimiento aves engorde Ecuador"));
                    }
                }
                catch (Exception ex) { _logger?.LogError(ex, "Error al actualizar inventario (aves engorde Ecuador)"); }
            }
        }

        var newHRet = dto.MortalidadHembras + dto.SelH + dto.ErrorSexajeHembras;
        var newMRet = dto.MortalidadMachos + dto.SelM + dto.ErrorSexajeMachos;
        var deltaHRet = newHRet - oldHRet;
        var deltaMRet = newMRet - oldMRet;
        if (deltaHRet != 0 || deltaMRet != 0)
        {
            try
            {
                if (deltaHRet > 0 || deltaMRet > 0)
                {
                    await _movimientoAvesService.RegistrarRetiroDesdeSeguimientoAsync(
                        loteId: dto.LoteId,
                        hembrasRetiradas: Math.Max(0, deltaHRet),
                        machosRetirados: Math.Max(0, deltaMRet),
                        mixtasRetiradas: 0,
                        fechaMovimiento: dto.FechaRegistro,
                        fuenteSeguimiento: "Engorde",
                        observaciones: $"Aves de Engorde Ecuador (actualización) - ajuste retiro H:{deltaHRet}, M:{deltaMRet}");
                }

                if (deltaHRet < 0 || deltaMRet < 0)
                {
                    await DevolverAvesAlInventarioAsync(
                        dto.LoteId,
                        Math.Abs(Math.Min(0, deltaHRet)),
                        Math.Abs(Math.Min(0, deltaMRet)));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde Ecuador (actualización): {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, companyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var companyId = _current.CompanyId;
        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == id && l.CompanyId == companyId && l.DeletedAt == null
                         select new { Seguimiento = s, l.GranjaId, l.NucleoId, l.GalponId, l.PaisId, l.EstadoOperativoLote }).SingleOrDefaultAsync();
        if (ent is null) return false;
        if (ent.Seguimiento.OrigenCruce)
            throw new InvalidOperationException(
                "Este registro se genera automáticamente desde los lotes reproductora (primeros 7 días). No se puede eliminar manualmente.");
        if (string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede eliminar el registro.");

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(ent.GranjaId, ent.PaisId));

        // ── Colombia (modelo B nivel granja) — devolución total por eliminación (defensivo; mirror levante) ──
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && ent.Seguimiento.Metadata != null)
        {
            try
            {
                var byItem = ParseMetadataItemsToKgPorOrigen(ent.Seguimiento.Metadata.RootElement);
                var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
                if (positivos.Count > 0)
                {
                    var refStr = $"Seguimiento aves engorde #{id} (devolución por eliminación)";
                    await _colombiaConsumoB.AplicarDevolucionAsync(ent.GranjaId, positivos, refStr, "Devolución por eliminación de seguimiento aves engorde Ecuador");
                }
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al devolver inventario Colombia al eliminar seguimiento aves engorde Ecuador"); }
        }
        // Gate por PAÍS DEL LOTE (S1): solo Ecuador/Panamá devuelven al modelo B (con núcleo/galpón).
        else if (_inventarioGestionService != null && ent.Seguimiento.Metadata != null &&
            modeloInv == ModeloInventarioConsumo.ModeloB)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(ent.Seguimiento.Metadata.RootElement);
                var refStr = $"Seguimiento aves engorde #{id} (devolución por eliminación)";
                foreach (var kv in byItem)
                    if (kv.Value > 0)
                        await _inventarioGestionService.RegistrarIngresoAsync(new InventarioGestionIngresoRequest(
                            ent.GranjaId, ent.NucleoId?.Trim(), ent.GalponId?.Trim(),
                            kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento aves engorde Ecuador"));
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al devolver inventario al eliminar seguimiento aves engorde Ecuador"); }
        }

        // Anular INV_CONSUMO huérfanos: si se borra un seguimiento, su INV_CONSUMO en el
        // histórico unificado debe quedar anulado para que un nuevo seguimiento en la misma
        // fecha no duplique consumoBodegaKg. La devolución INV_INGRESO ya revierte el stock.
        try
        {
            var refPrefix = $"Seguimiento aves engorde #{id}";
            var farmIdDel = ent.GranjaId;
            var nucleoIdDel = (ent.NucleoId ?? "").Trim();
            var galponIdDel = (ent.GalponId ?? "").Trim();
            var consumosHuerfanos = await _ctx.LoteRegistroHistoricoUnificados
                .Where(h => h.TipoEvento == "INV_CONSUMO"
                         && !h.Anulado
                         && h.FarmId == farmIdDel
                         && (h.NucleoId == null ? "" : h.NucleoId.Trim()) == nucleoIdDel
                         && (h.GalponId == null ? "" : h.GalponId.Trim()) == galponIdDel
                         && h.Referencia != null
                         && h.Referencia.StartsWith(refPrefix))
                .ToListAsync();
            foreach (var r in consumosHuerfanos)
                r.Anulado = true;
            if (consumosHuerfanos.Count > 0)
                await _ctx.SaveChangesAsync();
        }
        catch (Exception ex) { Console.WriteLine($"Error al anular INV_CONSUMO al eliminar seguimiento aves engorde Ecuador: {ex.Message}"); }

        var retH = (ent.Seguimiento.MortalidadHembras ?? 0) + (ent.Seguimiento.SelH ?? 0) + (ent.Seguimiento.ErrorSexajeHembras ?? 0);
        var retM = (ent.Seguimiento.MortalidadMachos ?? 0) + (ent.Seguimiento.SelM ?? 0) + (ent.Seguimiento.ErrorSexajeMachos ?? 0);
        if (retH > 0 || retM > 0)
        {
            try { await DevolverAvesAlInventarioAsync(ent.Seguimiento.LoteAveEngordeId, retH, retM); }
            catch (Exception ex) { Console.WriteLine($"Error al devolver aves al eliminar seguimiento engorde Ecuador: {ex.Message}"); }
        }

        var loteIdSeg = ent.Seguimiento.LoteAveEngordeId;
        _ctx.SeguimientoDiarioAvesEngorde.Remove(ent.Seguimiento);
        await _ctx.SaveChangesAsync();
        await RecalcularSaldoAlimentoPorLoteAsync(loteIdSeg, companyId);
        return true;
    }
}
