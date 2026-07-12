// Alta/edición/baja del seguimiento diario de aves de engorde, incluyendo el gate de inventario
// por país (Colombia modelo B nivel granja / Ecuador-Panamá modelo B) y el snapshot de consumo.
// Partial de SeguimientoAvesEngordeService.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoAvesEngordeService
{
    /// <summary>
    /// Construye el histórico de consumo de alimento por ítem para el campo historico_consumo_alimento.
    /// saldo_inicial = stock actual + oldConsumo (para edición, para restituir al estado pre-consumo del registro anterior).
    /// </summary>
    private async Task<JsonDocument?> BuildHistoricoConsumoAlimentoAsync(
        JsonDocument? metadata,
        int farmId, string? nucleoId, string? galponId,
        Dictionary<int, decimal>? oldByItemId = null)
    {
        if (metadata is null) return null;

        var newByItemId = ParseMetadataItemsToKg(metadata.RootElement);
        if (newByItemId.Count == 0) return null;

        var itemIds = newByItemId.Keys.ToList();

        var catalogItems = await _ctx.ItemInventario
            .AsNoTracking()
            .Where(i => itemIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id, i => i.Nombre);

        var nucleoIdN = (nucleoId ?? "").Trim();
        var galponIdN = (galponId ?? "").Trim();
        var stockByItem = await _ctx.InventarioGestionStock
            .AsNoTracking()
            .Where(s =>
                s.FarmId == farmId &&
                (s.NucleoId == null ? "" : s.NucleoId.Trim()) == nucleoIdN &&
                (s.GalponId == null ? "" : s.GalponId.Trim()) == galponIdN &&
                itemIds.Contains(s.ItemInventarioEcuadorId))
            .ToDictionaryAsync(s => s.ItemInventarioEcuadorId, s => s.Quantity);

        var historico = new List<object>();
        foreach (var kv in newByItemId)
        {
            var itemId = kv.Key;
            var consumo = kv.Value;
            var nombre = catalogItems.GetValueOrDefault(itemId, $"Ítem #{itemId}");
            var oldConsumo = oldByItemId?.GetValueOrDefault(itemId, 0m) ?? 0m;
            var currentStock = stockByItem.GetValueOrDefault(itemId, 0m);
            var saldoInicial = currentStock + oldConsumo;
            var saldoFinal = Math.Max(0, saldoInicial - consumo);

            historico.Add(new
            {
                nombre_alimento = nombre,
                saldo_inicial = saldoInicial,
                consumo = consumo,
                saldo_final = saldoFinal,
                unidad_medida = "kg"
            });
        }

        if (historico.Count == 0) return null;
        return JsonDocument.Parse(JsonSerializer.Serialize(historico));
    }

    public async Task<SeguimientoLoteLevanteDto> CreateAsync(SeguimientoLoteLevanteDto dto)
    {
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == dto.LoteId && l.CompanyId == _current.CompanyId && l.DeletedAt == null);
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

        // Para que el "Registro diario" muestre Ingreso/Traslado/Documento/Despacho,
        // llenamos campos en metadata desde el histórico unificado por lote+fecha.
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForEntity = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        // Snapshot del consumo por ítem antes de descontar del inventario.
        var historicoConsumo = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId);

        var ent = new SeguimientoDiarioAvesEngorde
        {
            LoteAveEngordeId = dto.LoteId,
            Fecha = dto.FechaRegistro,
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
            CreatedByUserId = dto.CreatedByUserId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = null,
            HistoricoConsumoAlimento = historicoConsumo
        };
        _ctx.SeguimientoDiarioAvesEngorde.Add(ent);

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO (Fase 3 paso 2, mirror levante) ──
        // Valida stock B de TODOS los ítems ANTES de commitear; guarda el seguimiento + descuenta en
        // UNA transacción. Si falta stock/ítem → throw por ítem → rollback → NO se guarda. Los ítems
        // Colombia traen catalogItemId (id-mapping A→B por código dentro del servicio).
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && dto.Metadata != null)
        {
            var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
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
            // Este servicio atiende engorde Colombia → un lote Colombia usa el bloque atómico de arriba.
            if (_inventarioGestionService != null && dto.Metadata != null && modeloInv == ModeloInventarioConsumo.ModeloB)
            {
                try
                {
                    var byItem = ParseMetadataItemsToKg(dto.Metadata.RootElement);
                    var refStr = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
                    foreach (var kv in byItem)
                        if (kv.Value > 0)
                            await _inventarioGestionService.RegistrarConsumoAsync(new InventarioGestionConsumoRequest(
                                lote.GranjaId, lote.NucleoId?.Trim(), lote.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, null));
                }
                catch (Exception ex) { _logger?.LogError(ex, "Error al registrar consumo inventario (aves engorde)"); }
            }
        }

        var totalRetiradas = dto.MortalidadHembras + dto.MortalidadMachos + dto.SelH + dto.SelM + dto.ErrorSexajeHembras + dto.ErrorSexajeMachos;
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
                    observaciones: $"Aves de Engorde - Mortalidad H: {dto.MortalidadHembras}, M: {dto.MortalidadMachos} | Selección H: {dto.SelH}, M: {dto.SelM} | Error sexaje H: {dto.ErrorSexajeHembras}, M: {dto.ErrorSexajeMachos} | {dto.Observaciones}");
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde: {ex.Message}"); }
        }

        await RecalcularSaldoAlimentoPorLoteAsync(dto.LoteId, _current.CompanyId);
        await _ctx.Entry(ent).ReloadAsync();

        return MapToDto(ent);
    }

    public async Task<SeguimientoLoteLevanteDto?> UpdateAsync(SeguimientoLoteLevanteDto dto)
    {
        var companyId = _current.CompanyId;
        var lote = await _ctx.LoteAveEngorde.AsNoTracking()
            .SingleOrDefaultAsync(l => l.LoteAveEngordeId == dto.LoteId && l.CompanyId == companyId && l.DeletedAt == null);
        if (lote is null)
            throw new InvalidOperationException($"Lote aves de engorde '{dto.LoteId}' no existe o no pertenece a la compañía.");
        if (string.Equals(lote.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede editar el registro.");

        var ent = await (from s in _ctx.SeguimientoDiarioAvesEngorde
                         join l in _ctx.LoteAveEngorde.AsNoTracking() on s.LoteAveEngordeId equals l.LoteAveEngordeId
                         where s.Id == dto.Id && l.CompanyId == companyId && l.DeletedAt == null
                         select s).SingleOrDefaultAsync();
        if (ent is null) return null;

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

        ent.Fecha = dto.FechaRegistro;
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
        var oldByItemId = ent.Metadata != null ? ParseMetadataItemsToKg(ent.Metadata.RootElement) : new Dictionary<int, decimal>();

        // Reconstruir snapshot de consumo por ítem (saldo_inicial = stock actual + consumo anterior del registro).
        var historicoConsumoUpdate = await BuildHistoricoConsumoAlimentoAsync(
            dto.Metadata, lote.GranjaId, lote.NucleoId, lote.GalponId, oldByItemId);

        // Patch de metadata (Ingreso/Traslado/Documento/Despacho) desde histórico unificado.
        var stockPatch = await BuildStockMetadataPatchAsync(dto.LoteId, dto.FechaRegistro.Date);
        var metadataForSave = MergeMetadataWithPatch(dto.Metadata, stockPatch);

        // jsonb + JsonDocument: forzar persistencia; si no, EF puede no marcar Metadata como modificado y el inventario sí aplica el diff desde dto.Metadata.
        ent.Metadata = CloneJsonDocument(metadataForSave);
        ent.ItemsAdicionales = CloneJsonDocument(dto.ItemsAdicionales);
        ent.HistoricoConsumoAlimento = CloneJsonDocument(historicoConsumoUpdate);
        ent.KcalAlH = kcalAlH;
        ent.ProtAlH = protAlH;
        ent.KcalAveH = kcalAlH is null ? null : Math.Round(consumoKgH * kcalAlH.Value, 3);
        ent.ProtAveH = protAlH is null ? null : Math.Round(consumoKgH * protAlH.Value, 3);
        ent.UpdatedAt = DateTime.UtcNow;
        // Reforzar persistencia de todas las columnas escalares (además de jsonb).
        _ctx.Entry(ent).State = EntityState.Modified;
        _ctx.Entry(ent).Property(e => e.Metadata).IsModified = true;
        _ctx.Entry(ent).Property(e => e.ItemsAdicionales).IsModified = true;
        _ctx.Entry(ent).Property(e => e.HistoricoConsumoAlimento).IsModified = true;

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(lote.GranjaId, lote.PaisId));
        var newByItemIdInv = dto.Metadata != null ? ParseMetadataItemsToKg(dto.Metadata.RootElement) : new Dictionary<int, decimal>();

        // ── Colombia (modelo B nivel granja) — BLOQUEO ATÓMICO en edición (mirror levante) ──
        // diff old/new por catalogItemId: diff>0 = consumo adicional; diff<0 = devolución. Valida el
        // stock B de los diff POSITIVOS ANTES de commitear; update + diff en UNA tx (todo-o-nada).
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null)
        {
            var incrementos = new Dictionary<int, decimal>();
            var allIds = new HashSet<int>(oldByItemId.Keys);
            foreach (var k in newByItemIdInv.Keys) allIds.Add(k);
            foreach (var itemId in allIds)
            {
                var diff = newByItemIdInv.GetValueOrDefault(itemId) - oldByItemId.GetValueOrDefault(itemId);
                if (diff > 0) incrementos[itemId] = diff;
            }
            await _colombiaConsumoB.ValidarStockConsumoAsync(lote.GranjaId, incrementos); // lanza si falta (antes de persistir)

            await using var tx = await _ctx.Database.BeginTransactionAsync();
            await _ctx.SaveChangesAsync();
            var refCo = $"Seguimiento aves engorde #{ent.Id} {dto.FechaRegistro:yyyy-MM-dd}";
            await _colombiaConsumoB.AplicarDiffAsync(lote.GranjaId, oldByItemId, newByItemIdInv, refCo);
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
                                farmId, nucleoId, galponId, itemId, -diff, "kg", refStr + " (devolución)", "Devolución desde seguimiento aves engorde"));
                    }
                }
                catch (Exception ex) { _logger?.LogError(ex, "Error al actualizar inventario (aves engorde)"); }
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
                        observaciones: $"Aves de Engorde (actualización) - ajuste retiro H:{deltaHRet}, M:{deltaMRet}");
                }

                if (deltaHRet < 0 || deltaMRet < 0)
                {
                    await DevolverAvesAlInventarioAsync(
                        dto.LoteId,
                        Math.Abs(Math.Min(0, deltaHRet)),
                        Math.Abs(Math.Min(0, deltaMRet)));
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error al registrar retiro desde seguimiento engorde (actualización): {ex.Message}"); }
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
        if (string.Equals(ent.EstadoOperativoLote, "Cerrado", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("El lote está cerrado (liquidado). No se puede eliminar el registro.");

        // Modelo de inventario según país del lote (S1 / Fase 3 paso 2).
        var modeloInv = InventarioConsumoGate.ResolverModelo(await ResolverPaisIdLoteAsync(ent.GranjaId, ent.PaisId));

        // ── Colombia (modelo B nivel granja) — devolución total por eliminación (mirror levante) ──
        // Los ítems Colombia traen catalogItemId (id-mapping A→B por código dentro del servicio).
        // Las mutaciones de stock se persisten con el SaveChanges posterior del borrado (mismo _ctx).
        if (modeloInv == ModeloInventarioConsumo.ModeloBNivelGranja && _colombiaConsumoB != null && ent.Seguimiento.Metadata != null)
        {
            try
            {
                var byItem = ParseMetadataItemsToKg(ent.Seguimiento.Metadata.RootElement);
                var positivos = byItem.Where(kv => kv.Value > 0).ToDictionary(kv => kv.Key, kv => kv.Value);
                if (positivos.Count > 0)
                {
                    var refStr = $"Seguimiento aves engorde #{id} (devolución por eliminación)";
                    await _colombiaConsumoB.AplicarDevolucionAsync(ent.GranjaId, positivos, refStr, "Devolución por eliminación de seguimiento aves engorde");
                }
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al devolver inventario Colombia al eliminar seguimiento aves engorde"); }
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
                            ent.GranjaId, ent.NucleoId?.Trim(), ent.GalponId?.Trim(), kv.Key, kv.Value, "kg", refStr, "Devolución por eliminación de seguimiento aves engorde"));
            }
            catch (Exception ex) { _logger?.LogError(ex, "Error al devolver inventario al eliminar seguimiento aves engorde"); }
        }

        // Anular INV_CONSUMO del seguimiento eliminado en el histórico unificado.
        // Sin esto, si se crea un nuevo seguimiento para la misma fecha, aparecerían dos
        // registros INV_CONSUMO activos (el antiguo + el nuevo), duplicando consumoBodegaKg.
        // La "devolución por eliminación" INV_INGRESO ya revierte el stock; el INV_CONSUMO
        // original debe quedar anulado para que el histórico refleje solo el estado real.
        try
        {
            var refPrefix = $"Seguimiento aves engorde #{id}";
            var farmIdDel   = ent.GranjaId;
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
        catch (Exception ex) { Console.WriteLine($"Error al anular INV_CONSUMO al eliminar seguimiento aves engorde: {ex.Message}"); }

        var retH = (ent.Seguimiento.MortalidadHembras ?? 0) + (ent.Seguimiento.SelH ?? 0) + (ent.Seguimiento.ErrorSexajeHembras ?? 0);
        var retM = (ent.Seguimiento.MortalidadMachos ?? 0) + (ent.Seguimiento.SelM ?? 0) + (ent.Seguimiento.ErrorSexajeMachos ?? 0);
        if (retH > 0 || retM > 0)
        {
            try { await DevolverAvesAlInventarioAsync(ent.Seguimiento.LoteAveEngordeId, retH, retM); }
            catch (Exception ex) { Console.WriteLine($"Error al devolver aves al eliminar seguimiento engorde: {ex.Message}"); }
        }

        var loteIdSeg = ent.Seguimiento.LoteAveEngordeId;
        _ctx.SeguimientoDiarioAvesEngorde.Remove(ent.Seguimiento);
        await _ctx.SaveChangesAsync();
        await RecalcularSaldoAlimentoPorLoteAsync(loteIdSeg, companyId);
        return true;
    }

    private async Task DevolverAvesAlInventarioAsync(int loteId, int hembras, int machos)
    {
        if (hembras <= 0 && machos <= 0) return;
        var inv = await _ctx.InventarioAves
            .Where(i => i.LoteId == loteId &&
                        i.CompanyId == _current.CompanyId &&
                        i.DeletedAt == null &&
                        i.Estado == "Activo")
            .OrderByDescending(i => i.FechaActualizacion)
            .FirstOrDefaultAsync();
        if (inv == null) return;
        inv.CantidadHembras += Math.Max(0, hembras);
        inv.CantidadMachos += Math.Max(0, machos);
        inv.FechaActualizacion = DateTime.UtcNow;
        inv.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync();
    }
}
