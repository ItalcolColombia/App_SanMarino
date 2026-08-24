// src/ZooSanMarino.Infrastructure/Services/InventarioGestion/Funciones/InventarioGestionService.Ingreso.cs
// Ingresos de inventario: registro (nivel galpon y nivel granja), listado, edicion de fecha/destino,
// ventana de alimento previo al encasetamiento (D4) y eliminacion.
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
    public async Task<InventarioGestionStockDto> RegistrarIngresoAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        if (_current?.CompanyId > 0 && _current.CompanyId != companyId)
            throw new InvalidOperationException("La granja no pertenece a su empresa.");
        var effectivePais = await GetEffectivePaisIdAsync(req.FarmId, ct);
        if (effectivePais > 0 && paisId != effectivePais)
            throw new InvalidOperationException("La granja no pertenece al país activo.");

        var isAlimento = IsAlimento(item);

        // ¿La empresa ubica el inventario por SILO? La decisión sale del flag de la empresa dueña de
        // la granja (InventarioUbicacionSiloCalculos, puro y con tests). Con el flag apagado, todo lo
        // de abajo es exactamente lo de siempre.
        var modoUbicacion = await ResolverModoUbicacionAsync(req.FarmId, ct);
        var errorSilo = InventarioUbicacionSiloCalculos.ValidarUbicacion(modoUbicacion, req.SiloId, req.GalponId, isAlimento);
        if (errorSilo is not null) throw new InvalidOperationException(errorSilo);

        var usaUbicacion = false;
        if (modoUbicacion == ModoUbicacionInventario.PorSilo)
        {
            // El silo ES la ubicación: no se valida núcleo/galpón porque no se persisten. Lo que sí
            // se valida es que el silo sea de ESTA granja —regla 4 del plan: nunca un descuento
            // silencioso en el silo de otra—.
            await ValidarSiloDeGranjaAsync(req.FarmId, req.SiloId!.Value, ct);
        }
        else
        {
            // Alimento por galpón vs nivel granja: CONFIGURABLE por empresa/granja (antes era por país).
            var nivelGranja = await EsInventarioNivelGranjaAsync(req.FarmId, ct);
            usaUbicacion = isAlimento && !nivelGranja;
            if (usaUbicacion && (string.IsNullOrWhiteSpace(req.NucleoId) || string.IsNullOrWhiteSpace(req.GalponId)))
                throw new InvalidOperationException("Para ítem tipo alimento debe indicar Núcleo y Galpón.");
            if (!usaUbicacion && (!string.IsNullOrWhiteSpace(req.NucleoId) || !string.IsNullOrWhiteSpace(req.GalponId)))
                throw new InvalidOperationException(nivelGranja
                    ? "Esta granja maneja el alimento a nivel granja (no use Núcleo/Galpón)."
                    : "Para ítems que no son alimento el inventario es solo a nivel granja (no use Núcleo/Galpón).");
        }

        var origenTipoNorm = req.OrigenTipo?.Trim() ?? "";
        if (string.Equals(origenTipoNorm, "granja", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es otra granja, indique la granja de procedencia (OrigenFarmId).");
            if (req.OrigenFarmId.Value == req.FarmId)
                throw new InvalidOperationException("La granja de origen debe ser distinta a la granja de destino del ingreso.");
            var (origCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (origCompanyId != companyId)
                throw new InvalidOperationException("La granja de origen debe pertenecer a la misma empresa.");
        }
        if (string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase))
        {
            if (!req.OrigenFarmId.HasValue || req.OrigenFarmId.Value <= 0)
                throw new InvalidOperationException("Cuando el origen es bodega, indique la granja a la que pertenece la bodega de procedencia (OrigenFarmId).");
            var (bodegaFarmCompanyId, _) = await GetFarmCompanyAndPaisAsync(req.OrigenFarmId.Value, ct);
            if (bodegaFarmCompanyId != companyId)
                throw new InvalidOperationException("La granja de la bodega de origen debe pertenecer a la misma empresa.");
        }

        var (nucleoId, galponId, siloId) = InventarioUbicacionSiloCalculos.NormalizarUbicacion(
            modoUbicacion,
            usaUbicacion ? req.NucleoId!.Trim() : null,
            usaUbicacion ? req.GalponId!.Trim() : null,
            req.SiloId);

        GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        var estadoIngreso = string.Equals(origenTipoNorm, "planta", StringComparison.OrdinalIgnoreCase)
            ? "Entrada planta"
            : string.Equals(origenTipoNorm, "bodega", StringComparison.OrdinalIgnoreCase)
                ? "Entrada bodega"
                : "Entrada granja";
        var movCreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        // TK-2026-000019 — la unidad la fija el catálogo del ítem. Antes era `req.Unit ?? "kg"`: el
        // front manda la del ítem, pero cualquier otro llamador (o un request sin unidad) grababa
        // kilos sobre un producto que se vende en litros.
        var unidad = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        // A1 — upsert ATÓMICO. Antes esto era buscar-o-insertar: dos ingresos concurrentes sobre
        // una clave sin fila no encontraban nada y ambos insertaban, y como todas las lecturas
        // usan FirstOrDefault, la segunda fila quedaba INVISIBLE para siempre. Con el índice
        // único de la clave natural, el ON CONFLICT convierte esa carrera en una suma.
        InventarioGestionStock existing = null!;
        var mov = new InventarioGestionMovimiento
        {
            CompanyId = companyId,
            PaisId = paisId,
            FarmId = req.FarmId,
            NucleoId = nucleoId,
            GalponId = galponId,
            SiloId = siloId,
            ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
            Quantity = req.Quantity,
            Unit = unidad,
            MovementType = "Ingreso",
            Estado = estadoIngreso,
            Reference = req.Reference?.Trim(),
            Reason = req.Reason?.Trim(),
            CreatedAt = movCreatedAt,
            CreatedByUserId = _current?.UserId.ToString(),
            ParaProximoCiclo = req.ParaProximoCiclo,
            // Auditoría: el instante REAL de captura. `CreatedAt` lo pisa la fecha que tipea el
            // usuario, así que no puede servir de auditoría; acá nunca se escribe esa fecha.
            RegistradoAt = DateTimeOffset.UtcNow
        };

        // El stock y el movimiento que lo explica van juntos o no van.
        await EnTransaccionAsync(async () =>
        {
            existing = await SumarStockAtomicoAsync(
                companyId, paisId, req.FarmId, nucleoId, galponId,
                req.ItemInventarioEcuadorId, req.Quantity, unidad, siloId, ct);

            // El movimiento y la fila de stock quedan con la MISMA unidad, la del catálogo: el
            // upsert ya realineó la fila (`unit = EXCLUDED.unit`). Antes acá se heredaba la unidad
            // vieja de la fila, que es cómo el 'kg' original se propagaba a cada movimiento nuevo.
            mov.Unit = existing.Unit;

            _db.InventarioGestionMovimientos.Add(mov);
            await _db.SaveChangesAsync(ct);
        }, ct);

        await RefrescarSaldoAlimentoEngordeAsync(companyId, req.FarmId, nucleoId, galponId, mov.MovementType, ct);

        // Avisa —sin bloquear— si el ingreso quedó fechado fuera del ciclo vigente del galpón.
        // v16a: antes se saltaba cuando venía la marca «para el próximo ciclo» (la atribución era
        // explícita y el aviso, ruido). Con la marca apagada por `GuardarMarcaProximoCicloApagada`
        // ese camino es inalcanzable, así que el aviso vuelve a evaluarse siempre.
        var aviso = await EvaluarAvisoFechaFueraDeCicloAsync(
            companyId, req.FarmId, nucleoId, galponId, movCreatedAt, ct);

        var dto = (await GetStockAsync(req.FarmId, nucleoId, galponId, null, null, ct))
            .FirstOrDefault(x => x.ItemInventarioEcuadorId == req.ItemInventarioEcuadorId
                              && x.NucleoId == nucleoId && x.GalponId == galponId && x.SiloId == siloId)
            ?? new InventarioGestionStockDto(existing.Id, existing.FarmId, existing.NucleoId, existing.GalponId, existing.ItemInventarioEcuadorId, item.Codigo, item.Nombre, item.TipoItem ?? "alimento", existing.Quantity, existing.Unit, null, null, null, null, null, existing.SiloId);

        return aviso is null ? dto : dto with { AvisoFechaFueraDeCiclo = aviso };
    }

    /// <summary>
    /// Fase 3 — devolución a nivel granja (Colombia): repone <c>inventario_gestion_stock</c> por
    /// (farm, item, nucleo=NULL, galpon=NULL) e inserta un movimiento <c>Ingreso</c>. Crea el stock
    /// si no existe.
    ///
    /// <para>
    /// F4 (22-ago-2026), trampa #1 del plan: si esto se quedaba <c>read-modify-write</c> RASTREADO
    /// mientras <see cref="RegistrarConsumoNivelGranjaAsync"/> pasaba a SQL crudo, un
    /// <c>SaveChangesAsync</c> de ESTE método —dentro de la MISMA unidad de trabajo, por ejemplo
    /// <c>AplicarDiffAsync</c> resolviendo dos <c>ItemConsumoKey</c> distintas al mismo
    /// <c>itemBId</c>— escribiría el absoluto de esta fila rastreada y <b>pisaría</b> el descuento
    /// atómico del otro ítem sobre la misma fila. Régimen mixto = footgun documentado en
    /// <c>StockAtomico.cs:44-48</c>. Ahora usa <see cref="SumarStockAtomicoAsync"/> — el mismo
    /// <c>INSERT ... ON CONFLICT ... DO UPDATE</c> que ya usan ingreso y traslados de Ecuador/Panamá—,
    /// así que dos operaciones sobre la misma fila, vengan de donde vengan, se serializan en la base.
    /// </para>
    /// </summary>
    public async Task RegistrarIngresoNivelGranjaAsync(InventarioGestionIngresoRequest req, CancellationToken ct = default)
    {
        if (req.Quantity <= 0) throw new InvalidOperationException("La cantidad debe ser positiva.");
        var item = await _db.ItemInventario.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ItemInventarioEcuadorId, ct);
        if (item == null) throw new InvalidOperationException("El ítem de inventario no existe.");

        var (companyId, paisId) = await GetFarmCompanyAndPaisAsync(req.FarmId, ct);
        var unidad = UnidadInventarioCalculos.Resolver(item.Unidad, req.Unit);

        GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        // El ingreso y el movimiento que lo explica van juntos o no van, igual que en el consumo.
        await EnTransaccionAsync(async () =>
        {
            await SumarStockAtomicoAsync(
                companyId, paisId, req.FarmId, null, null,
                req.ItemInventarioEcuadorId, req.Quantity, unidad, req.SiloId, ct);

            _db.InventarioGestionMovimientos.Add(new InventarioGestionMovimiento
            {
                CompanyId = companyId,
                PaisId = paisId,
                FarmId = req.FarmId,
                NucleoId = null,
                GalponId = null,
                ItemInventarioEcuadorId = req.ItemInventarioEcuadorId,
                SiloId = req.SiloId,
                Quantity = req.Quantity,
                // TK-2026-000019 — la del catálogo, igual que el consumo de nivel granja.
                Unit = unidad,
                MovementType = "Ingreso",
                Estado = "Ingreso",
                Reference = req.Reference?.Trim(),
                Reason = req.Reason?.Trim(),
                // F2.2 (22-ago-2026): antes hardcodeaba UtcNow aunque `req.FechaMovimiento` ya existía en
                // el DTO — una edición devolvía el ajuste positivo al día del seguimiento y su devolución
                // quedaba en HOY: los dos lados del mismo diff en días distintos. Mismo criterio que
                // RegistrarConsumoNivelGranjaAsync, con la ancla de INGRESO (12:00), no la de consumo.
                CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento),
                CreatedByUserId = _current?.UserId.ToString(),
                // Mismo criterio que RegistrarIngresoAsync: la marca viaja en el request y el instante de
                // captura se guarda aparte. Sin esto, todo lo que entra por Colombia quedaría marcado
                // como «fila anterior a la columna» para siempre.
                ParaProximoCiclo = req.ParaProximoCiclo,
                RegistradoAt = DateTimeOffset.UtcNow
            });
            await _db.SaveChangesAsync(ct);
        }, ct);
    }

    public async Task<List<InventarioGestionIngresoListDto>> GetIngresosAsync(
        int? farmId = null,
        DateTime? fechaDesde = null,
        DateTime? fechaHasta = null,
        string? search = null,
        string? itemTipoItem = null,
        string? nucleoId = null,
        string? galponId = null,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            return new List<InventarioGestionIngresoListDto>();

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);
        if (allowedFarmIds.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var ingresoTypes = new[] { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };

        var query = _db.InventarioGestionMovimientos
            .AsNoTracking()
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .Where(x => x.CompanyId == companyId.Value
                        && ingresoTypes.Contains(x.MovementType)
                        && allowedFarmIds.Contains(x.FarmId));

        if (farmId.HasValue)
            query = query.Where(x => x.FarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var start = fechaDesde.Value.Date;
            query = query.Where(x => x.CreatedAt >= start);
        }

        if (fechaHasta.HasValue)
        {
            var end = fechaHasta.Value.Date.AddDays(1);
            query = query.Where(x => x.CreatedAt < end);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Codigo ?? "").ToLower().Contains(s) ||
                (x.ItemInventario.Nombre ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            query = query.Where(x =>
                (x.ItemInventario.Concepto != null && x.ItemInventario.Concepto.Trim().ToLower() == t) ||
                (x.ItemInventario.TipoItem != null && x.ItemInventario.TipoItem.Trim().ToLower() == t));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            query = query.Where(x => x.NucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            query = query.Where(x => x.GalponId == galponId);

        var list = await query
            .OrderByDescending(x => x.CreatedAt)
            .Take(2000)
            .ToListAsync(ct);

        // Query orphaned historico records: food ingresos cuyo inventario_gestion_movimiento
        // fue eliminado físicamente pero cuyo registro en lote_registro_historico_unificado
        // quedó con anulado=false (el lookup en EliminarIngresoAsync no lo encontró).
        var ingresoTiposHist = new[] { "INV_INGRESO", "INV_TRASLADO_ENTRADA" };

        IQueryable<LoteRegistroHistoricoUnificado> orphanedQuery = _db.LoteRegistroHistoricoUnificados
            .AsNoTracking()
            .Where(h => h.CompanyId == companyId.Value
                && h.OrigenTabla == "inventario_gestion_movimiento"
                && ingresoTiposHist.Contains(h.TipoEvento)
                && !h.Anulado
                && allowedFarmIds.Contains(h.FarmId)
                && !_db.InventarioGestionMovimientos.Any(m => m.Id == h.OrigenId));

        if (farmId.HasValue)
            orphanedQuery = orphanedQuery.Where(h => h.FarmId == farmId.Value);

        if (fechaDesde.HasValue)
        {
            var startO = fechaDesde.Value.Date;
            orphanedQuery = orphanedQuery.Where(h => h.FechaOperacion >= startO);
        }

        if (fechaHasta.HasValue)
        {
            var endO = fechaHasta.Value.Date.AddDays(1);
            orphanedQuery = orphanedQuery.Where(h => h.FechaOperacion < endO);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLowerInvariant();
            orphanedQuery = orphanedQuery.Where(h => (h.ItemResumen ?? "").ToLower().Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(itemTipoItem))
        {
            var t = itemTipoItem.Trim().ToLowerInvariant();
            orphanedQuery = orphanedQuery.Where(h =>
                h.ItemInventarioEcuadorId != null &&
                _db.ItemInventario.Any(i => i.Id == h.ItemInventarioEcuadorId &&
                    ((i.Concepto != null && i.Concepto.Trim().ToLower() == t) ||
                     (i.TipoItem != null && i.TipoItem.Trim().ToLower() == t))));
        }

        if (!string.IsNullOrWhiteSpace(nucleoId))
            orphanedQuery = orphanedQuery.Where(h => h.NucleoId == nucleoId);

        if (!string.IsNullOrWhiteSpace(galponId))
            orphanedQuery = orphanedQuery.Where(h => h.GalponId == galponId);

        var orphaned = await orphanedQuery
            .OrderByDescending(h => h.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        if (list.Count == 0 && orphaned.Count == 0)
            return new List<InventarioGestionIngresoListDto>();

        var farmIds = list.Select(x => x.FarmId)
            .Concat(orphaned.Select(h => h.FarmId))
            .Distinct().ToList();

        var nucleoIds = list.Where(x => !string.IsNullOrWhiteSpace(x.NucleoId)).Select(x => x.NucleoId!)
            .Concat(orphaned.Where(h => !string.IsNullOrWhiteSpace(h.NucleoId)).Select(h => h.NucleoId!))
            .Distinct().ToList();

        var galponIds = list.Where(x => !string.IsNullOrWhiteSpace(x.GalponId)).Select(x => x.GalponId!)
            .Concat(orphaned.Where(h => !string.IsNullOrWhiteSpace(h.GalponId)).Select(h => h.GalponId!))
            .Distinct().ToList();

        var nucleos = nucleoIds.Count > 0
            ? await _db.Nucleos.AsNoTracking()
                .Where(n => nucleoIds.Contains(n.NucleoId) && farmIds.Contains(n.GranjaId))
                .ToDictionaryAsync(n => (n.NucleoId, n.GranjaId), n => n.NucleoNombre, ct)
            : new Dictionary<(string, int), string>();

        var galpones = galponIds.Count > 0
            ? await _db.Galpones.AsNoTracking()
                .Where(g => galponIds.Contains(g.GalponId) && farmIds.Contains(g.GranjaId))
                .ToDictionaryAsync(g => (g.GalponId, g.GranjaId), g => g.GalponNombre, ct)
            : new Dictionary<(string, int), string>();

        // Farms y items para registros huérfanos (list ya tiene Farm cargado via Include)
        var orphanedFarmIds = orphaned.Select(h => h.FarmId).Distinct().ToList();
        var orphanedFarms = orphanedFarmIds.Count > 0
            ? await _db.Farms.AsNoTracking()
                .Where(f => orphanedFarmIds.Contains(f.Id))
                .ToDictionaryAsync(f => f.Id, f => f.Name, ct)
            : new Dictionary<int, string>();

        var orphanedItemIds = orphaned
            .Where(h => h.ItemInventarioEcuadorId.HasValue)
            .Select(h => h.ItemInventarioEcuadorId!.Value)
            .Distinct().ToList();
        var orphanedItems = orphanedItemIds.Count > 0
            ? await _db.ItemInventario.AsNoTracking()
                .Where(i => orphanedItemIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id, ct)
            : new Dictionary<int, ItemInventario>();

        // Solo los movimientos vivos traen silo: en las filas huérfanas el dato murió con el
        // movimiento (el espejo lo guarda, pero la entidad del histórico no lo mapea).
        var siloNombres = await NombresDeSilosAsync(list.Select(x => x.SiloId), ct);

        var mainDtos = list.Select(x =>
        {
            string? nucleoNombre = x.NucleoId != null && nucleos.TryGetValue((x.NucleoId, x.FarmId), out var nn) ? nn : null;
            string? galponNombre = x.GalponId != null && galpones.TryGetValue((x.GalponId, x.FarmId), out var gn) ? gn : null;

            return new InventarioGestionIngresoListDto(
                x.Id,
                x.FarmId,
                x.Farm.Name,
                x.NucleoId,
                nucleoNombre,
                x.GalponId,
                galponNombre,
                x.ItemInventarioEcuadorId,
                x.ItemInventario.Codigo,
                x.ItemInventario.Nombre,
                x.ItemInventario.Concepto ?? x.ItemInventario.TipoItem ?? "alimento",
                x.ItemInventario.TipoItem ?? "alimento",
                x.Quantity,
                x.Unit,
                x.Reference,
                x.Reason,
                x.Estado,
                x.CreatedAt,
                x.CreatedAt,
                x.ParaProximoCiclo,
                x.RegistradoAt,
                x.SiloId,
                x.SiloId.HasValue && siloNombres.TryGetValue(x.SiloId.Value, out var sn) ? sn : null);
        });

        var orphanedDtos = orphaned.Select(h =>
        {
            orphanedItems.TryGetValue(h.ItemInventarioEcuadorId ?? 0, out var item);

            // ItemResumen viene del trigger como "codigo — nombre"
            string itemCodigo = item?.Codigo ?? "";
            string itemNombre = item?.Nombre ?? "";
            if (string.IsNullOrEmpty(itemCodigo) && !string.IsNullOrEmpty(h.ItemResumen))
            {
                var parts = h.ItemResumen.Split('—', 2);
                itemCodigo = parts[0].Trim();
                itemNombre = parts.Length > 1 ? parts[1].Trim() : h.ItemResumen;
            }

            string? nucleoNombre = h.NucleoId != null && nucleos.TryGetValue((h.NucleoId, h.FarmId), out var nn) ? nn : null;
            string? galponNombre = h.GalponId != null && galpones.TryGetValue((h.GalponId, h.FarmId), out var gn) ? gn : null;
            orphanedFarms.TryGetValue(h.FarmId, out var farmName);

            return new InventarioGestionIngresoListDto(
                h.OrigenId,
                h.FarmId,
                farmName,
                h.NucleoId,
                nucleoNombre,
                h.GalponId,
                galponNombre,
                h.ItemInventarioEcuadorId ?? 0,
                itemCodigo,
                itemNombre,
                item?.Concepto ?? item?.TipoItem ?? "alimento",
                item?.TipoItem ?? "alimento",
                h.CantidadKg ?? 0,
                h.Unidad ?? "kg",
                h.Referencia,
                null,
                null,
                new DateTimeOffset(h.FechaOperacion, TimeSpan.Zero),
                h.CreatedAt,
                // El movimiento ya no existe: la marca sobrevive en el espejo, el instante de captura no
                // (`registrado_at` vive solo en inventario_gestion_movimiento).
                h.ParaProximoCiclo,
                null);
        });

        return mainDtos.Concat(orphanedDtos)
            .OrderByDescending(d => d.CreatedAt)
            .ToList();
    }

    public async Task<InventarioGestionIngresoListDto> ActualizarFechaIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarFechaIngresoRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposEntradaEditables = new HashSet<string>(StringComparer.Ordinal) { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposEntradaEditables.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se puede editar la fecha de movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        mov.CreatedAt = ResolveMovimientoCreatedAt(req.FechaMovimiento);
        await _db.SaveChangesAsync(ct);

        // Sincronizar fecha_operacion en tabla espejo lote_registro_historico_unificado
        var fechaDateIngreso = mov.CreatedAt.UtcDateTime.Date;
        var histIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histIngreso != null)
        {
            histIngreso.FechaOperacion = fechaDateIngreso;
        }
        else
        {
            // Fallback: identificar por granja + nucleo + galpon + item + cantidad sin estar anulado
            var histFallback = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
            if (histFallback != null)
                histFallback.FechaOperacion = fechaDateIngreso;
        }
        await _db.SaveChangesAsync(ct);

        // Correr la fecha de un ingreso lo mueve de día dentro del saldo del galpón.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);

        return await MapIngresoListDtoAsync(mov, ct);
    }

    /// <summary>
    /// Proyección de un movimiento de ingreso ya cargado (con <c>Farm</c> e <c>ItemInventario</c>) al
    /// DTO del listado. Extraída sin cambiar nada: las dos ediciones de un ingreso —fecha y destino de
    /// ciclo— devuelven exactamente la misma fila.
    /// </summary>
    private async Task<InventarioGestionIngresoListDto> MapIngresoListDtoAsync(
        InventarioGestionMovimiento mov, CancellationToken ct)
    {
        string? nucleoNombre = null;
        string? galponNombre = null;
        if (mov.NucleoId != null)
            nucleoNombre = await _db.Nucleos.AsNoTracking()
                .Where(n => n.NucleoId == mov.NucleoId && n.GranjaId == mov.FarmId)
                .Select(n => n.NucleoNombre)
                .FirstOrDefaultAsync(ct);
        if (mov.GalponId != null)
            galponNombre = await _db.Galpones.AsNoTracking()
                .Where(g => g.GalponId == mov.GalponId && g.GranjaId == mov.FarmId)
                .Select(g => g.GalponNombre)
                .FirstOrDefaultAsync(ct);

        return new InventarioGestionIngresoListDto(
            mov.Id,
            mov.FarmId,
            mov.Farm.Name,
            mov.NucleoId,
            nucleoNombre,
            mov.GalponId,
            galponNombre,
            mov.ItemInventarioEcuadorId,
            mov.ItemInventario.Codigo,
            mov.ItemInventario.Nombre,
            mov.ItemInventario.Concepto ?? mov.ItemInventario.TipoItem ?? "alimento",
            mov.ItemInventario.TipoItem ?? "alimento",
            mov.Quantity,
            mov.Unit,
            mov.Reference,
            mov.Reason,
            mov.Estado,
            mov.CreatedAt,
            mov.CreatedAt,
            mov.ParaProximoCiclo,
            mov.RegistradoAt);
    }

    /// <summary>
    /// Cambia la atribución de ciclo de un ingreso ya registrado y la refleja en
    /// <c>lote_registro_historico_unificado</c>.
    /// <para>
    /// El espejo se busca <b>primero</b> por <c>origen_tabla + origen_id</c>, que es la clave real
    /// (<c>uq_lote_hist_origen</c>). El fallback por granja+núcleo+galpón+ítem+cantidad se conserva tal
    /// cual está en <see cref="ActualizarFechaIngresoAsync"/> para no divergir, pero es <b>frágil</b>:
    /// con dos ingresos idénticos en la misma ubicación puede marcar el otro. Se llega a él solo con
    /// filas viejas sin <c>origen_id</c>; si se cambia, hay que cambiarlo en los dos lugares a la vez.
    /// </para>
    /// </summary>
    public async Task<InventarioGestionIngresoListDto> ActualizarDestinoCicloIngresoAsync(
        int movimientoId,
        InventarioGestionActualizarDestinoCicloRequest req,
        CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .Include(x => x.ItemInventario)
            .Include(x => x.Farm)
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        if (mov == null)
            throw new InvalidOperationException("No se encontró el ingreso indicado.");

        var tiposEntradaEditables = new HashSet<string>(StringComparer.Ordinal) { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposEntradaEditables.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se puede marcar el destino de ciclo de movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        if (string.IsNullOrWhiteSpace(mov.GalponId))
            throw new InvalidOperationException("La marca «para el próximo ciclo» solo aplica a movimientos con galpón: sin galpón no hay ciclo al que atribuir el alimento.");

        // v16a: PONER la marca está deshabilitado; QUITARLA no, para que ninguna marca vieja quede
        // sin poder corregirse (R3). Si el movimiento ya está en el valor pedido, no hay escritura.
        if (req.ParaProximoCiclo != mov.ParaProximoCiclo)
            GuardarMarcaProximoCicloApagada(req.ParaProximoCiclo);

        mov.ParaProximoCiclo = req.ParaProximoCiclo;
        await _db.SaveChangesAsync(ct);

        // Espejo: mismo patrón de búsqueda que ActualizarFechaIngresoAsync (clave real primero,
        // fallback frágil después). El histórico se ANULA, nunca se borra, así que la fila vive.
        var hist = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h => h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (hist == null)
        {
            hist = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
        }
        if (hist != null)
            hist.ParaProximoCiclo = req.ParaProximoCiclo;
        await _db.SaveChangesAsync(ct);

        // Cambiar de ciclo mueve los kg de una apertura a otra: el saldo persistido se recalcula
        // desde la fn, igual que al correr la fecha.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);

        return await MapIngresoListDtoAsync(mov, ct);
    }

    /// <inheritdoc />
    public async Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetAsync(
        int farmId,
        string? nucleoId,
        string? galponId,
        DateTime fechaMovimiento,
        CancellationToken ct = default)
    {
        var companyId = await _db.Farms.AsNoTracking()
            .Where(f => f.Id == farmId)
            .Select(f => (int?)f.CompanyId)
            .FirstOrDefaultAsync(ct);

        // Empresa efectiva SIEMPRE por datos (farms.company_id) y fail-closed: sin granja no hay
        // ventana que abrir, así que la regla del mes en curso queda como única.
        if (companyId is not { } company || company <= 0)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, 0);

        var dias = await _db.Companies.AsNoTracking()
            .Where(c => c.Id == company)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct) ?? 10;

        var galpon = (galponId ?? "").Trim();
        if (galpon.Length == 0)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, dias);

        var nucleo = (nucleoId ?? "").Trim();
        // `fecha_encaset` es timestamptz: el límite tiene que ir anclado en UTC o Npgsql rechaza el
        // parámetro (Kind=Unspecified). Medianoche UTC del día del movimiento incluye el encaset del
        // MISMO día, que el front graba a mediodía UTC (FechasPuras).
        var desde = FechasPuras.RangoDiaUtc(fechaMovimiento).Desde;

        // Encaset más cercano DEL GALPÓN a partir de la fecha del movimiento. "A partir de" y no
        // "futuro": el alimento se digita días después de llegar, así que el encaset que lo justifica
        // ya puede haber ocurrido. Se miran las dos poblaciones porque el pedido cubre engorde y postura.
        var encasetEngorde = await _db.LoteAveEngorde.AsNoTracking()
            .Where(l => l.CompanyId == company
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.FechaEncaset != null
                     && l.FechaEncaset >= desde)
            .MinAsync(l => l.FechaEncaset, ct);

        var encasetPostura = await _db.Lotes.AsNoTracking()
            .Where(l => l.CompanyId == company
                     && l.DeletedAt == null
                     && l.GranjaId == farmId
                     && (l.NucleoId == null ? "" : l.NucleoId.Trim()) == nucleo
                     && (l.GalponId == null ? "" : l.GalponId.Trim()) == galpon
                     && l.FechaEncaset != null
                     && l.FechaEncaset >= desde)
            .MinAsync(l => l.FechaEncaset, ct);

        var proximo = (encasetEngorde, encasetPostura) switch
        {
            (null, null) => (DateTime?)null,
            (null, { } p) => p,
            ({ } e, null) => e,
            ({ } e, { } p) => e <= p ? e : p
        };

        return new InventarioGestionVentanaAlimentoPrevioDto(proximo, dias);
    }

    /// <inheritdoc />
    public async Task<InventarioGestionVentanaAlimentoPrevioDto> ResolverVentanaAlimentoPrevioEncasetDeIngresoAsync(
        int movimientoId,
        DateTime fechaMovimiento,
        CancellationToken ct = default)
    {
        var ubicacion = await _db.InventarioGestionMovimientos.AsNoTracking()
            .Where(m => m.Id == movimientoId)
            .Select(m => new { m.FarmId, m.NucleoId, m.GalponId })
            .FirstOrDefaultAsync(ct);

        if (ubicacion == null)
            return new InventarioGestionVentanaAlimentoPrevioDto(null, 0);

        return await ResolverVentanaAlimentoPrevioEncasetAsync(
            ubicacion.FarmId, ubicacion.NucleoId, ubicacion.GalponId, fechaMovimiento, ct);
    }

    /// <summary>
    /// Elimina un movimiento de tipo Ingreso / TrasladoEntrada / TrasladoInterGranjaEntrada.
    /// No modifica stock. Marca anulado=true en lote_registro_historico_unificado (auditoría)
    /// y elimina físicamente el registro de inventario_gestion_movimiento.
    /// </summary>
    public async Task EliminarIngresoAsync(int movimientoId, CancellationToken ct = default)
    {
        var companyId = await GetEffectiveCompanyIdAsync(ct);
        if (companyId == null || companyId.Value <= 0)
            throw new InvalidOperationException("No tiene empresa activa para esta operación.");

        var allowedFarmIds = await GetAssignedFarmIdsInCompanyAsync(companyId.Value, ct).ConfigureAwait(false);

        var mov = await _db.InventarioGestionMovimientos
            .FirstOrDefaultAsync(x => x.Id == movimientoId && x.CompanyId == companyId.Value, ct);

        // Caso huérfano: el movimiento ya fue eliminado físicamente pero quedó un registro
        // en lote_registro_historico_unificado con anulado=false. Solo marcarlo anulado.
        if (mov == null)
        {
            var ingresoTiposHist = new[] { "INV_INGRESO", "INV_TRASLADO_ENTRADA" };
            var histHuerfano = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.OrigenTabla == "inventario_gestion_movimiento"
                    && h.OrigenId == movimientoId
                    && h.CompanyId == companyId.Value
                    && ingresoTiposHist.Contains(h.TipoEvento)
                    && !h.Anulado
                    && allowedFarmIds.Contains(h.FarmId), ct);

            if (histHuerfano == null)
                throw new InvalidOperationException("No se encontró el ingreso indicado.");

            histHuerfano.Anulado = true;
            await _db.SaveChangesAsync(ct);
            await RefrescarSaldoAlimentoEngordeAsync(
                histHuerfano.CompanyId, histHuerfano.FarmId, histHuerfano.NucleoId, histHuerfano.GalponId, "Ingreso", ct);
            return;
        }

        var tiposIngreso = new HashSet<string>(StringComparer.Ordinal)
            { "Ingreso", "TrasladoEntrada", "TrasladoInterGranjaEntrada" };
        if (!tiposIngreso.Contains(mov.MovementType))
            throw new InvalidOperationException("Solo se pueden eliminar movimientos de tipo Ingreso o entrada de traslado.");

        if (!allowedFarmIds.Contains(mov.FarmId))
            throw new InvalidOperationException("No tiene acceso a este ingreso.");

        // Marcar anulado en tabla espejo (auditoría)
        var histElimIngreso = await _db.LoteRegistroHistoricoUnificados
            .FirstOrDefaultAsync(h =>
                h.OrigenTabla == "inventario_gestion_movimiento" && h.OrigenId == movimientoId, ct);
        if (histElimIngreso == null)
        {
            // Fallback: buscar por granja + nucleo + galpon + item + cantidad sin estar anulado
            histElimIngreso = await _db.LoteRegistroHistoricoUnificados
                .FirstOrDefaultAsync(h =>
                    h.FarmId == mov.FarmId &&
                    h.NucleoId == mov.NucleoId &&
                    h.GalponId == mov.GalponId &&
                    h.ItemInventarioEcuadorId == mov.ItemInventarioEcuadorId &&
                    h.CantidadKg == mov.Quantity &&
                    !h.Anulado, ct);
        }
        if (histElimIngreso != null)
            histElimIngreso.Anulado = true;

        _db.InventarioGestionMovimientos.Remove(mov);
        await _db.SaveChangesAsync(ct);
        // El histórico queda `anulado`, que el saldo sí filtra: el alimento eliminado debe desaparecer.
        await RefrescarSaldoAlimentoEngordeAsync(mov.CompanyId, mov.FarmId, mov.NucleoId, mov.GalponId, mov.MovementType, ct);
    }
}
