// Liquidación técnica de Pollo Engorde (Lote Ave Engorde padre + reproductores) para Ecuador:
// reporte por lote/rango/todos-liquidados, auditoría/corrección vía funciones SQL e indicadores por
// lote padre. La aritmética pesada por lote vive en la función SQL fn_indicadores_pollo_engorde.
// Partial de IndicadorEcuadorService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class IndicadorEcuadorService
{
    /// <inheritdoc />
    public async Task<LiquidacionPolloEngordeReporteDto> LiquidacionPolloEngordeReporteAsync(
        LiquidacionPolloEngordeReporteRequest request,
        CancellationToken ct = default)
    {
        var pesoAjuste = PesoAjusteDefault;
        var divisorAjuste = DivisorAjusteDefault;
        var items = new List<LiquidacionPolloEngordeItemDto>();

        if (string.Equals(request.Modo, "UnLote", StringComparison.OrdinalIgnoreCase))
        {
            // Un lote por id
            if (request.LoteAveEngordeId.HasValue && request.LoteAveEngordeId.Value > 0)
            {
                var lote = await _context.LoteAveEngorde
                    .AsNoTracking()
                    .Include(l => l.Farm)
                    .Include(l => l.Galpon)
                    .Where(l => l.LoteAveEngordeId == request.LoteAveEngordeId.Value &&
                                l.CompanyId == _currentUser.CompanyId &&
                                l.DeletedAt == null)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

                if (lote == null)
                    throw new InvalidOperationException($"No se encontró el lote de ave engorde {request.LoteAveEngordeId}.");

                var (soloFiltroUnLote, usarAdmUnLote) = ResolveFiltroLotes(request.TipoFiltroLotes);
                var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, soloFiltroUnLote, pesoAjuste, divisorAjuste, usarAdmUnLote).ConfigureAwait(false);
                if (ind == null)
                    throw new InvalidOperationException("El lote no cumple el criterio de filtro seleccionado (TipoFiltroLotes).");

                var id = lote.LoteAveEngordeId ?? 0;
                items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
                return new LiquidacionPolloEngordeReporteDto("UnLote", items);
            }

            // Varios lotes: granja y opcionalmente núcleo / galpón (sin id de lote)
            if (request.GranjaId.HasValue && request.GranjaId.Value > 0)
            {
                var query = _context.LoteAveEngorde
                    .AsNoTracking()
                    .Include(l => l.Farm)
                    .Include(l => l.Galpon)
                    .Where(l => l.CompanyId == _currentUser.CompanyId &&
                                l.DeletedAt == null &&
                                l.GranjaId == request.GranjaId.Value);

                if (!string.IsNullOrWhiteSpace(request.NucleoId))
                    query = query.Where(l => l.NucleoId == request.NucleoId);

                if (!string.IsNullOrWhiteSpace(request.GalponId))
                    query = query.Where(l => l.GalponId == request.GalponId);

                var lotes = await query.OrderBy(l => l.LoteNombre).ToListAsync(ct).ConfigureAwait(false);
                var (soloFiltroMulti, usarAdmMulti) = ResolveFiltroLotes(request.TipoFiltroLotes);

                foreach (var lote in lotes)
                {
                    var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, soloFiltroMulti, pesoAjuste, divisorAjuste, usarAdmMulti).ConfigureAwait(false);
                    if (ind == null)
                        continue;
                    var id = lote.LoteAveEngordeId ?? 0;
                    items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
                }

                if (items.Count == 0)
                    throw new InvalidOperationException(
                        "No hay lotes que cumplan el filtro en el alcance seleccionado. Ajuste granja, núcleo, galpón o TipoFiltroLotes.");

                return new LiquidacionPolloEngordeReporteDto("UnLote", items);
            }

            throw new InvalidOperationException("Modo UnLote: indique un lote (LoteAveEngordeId) o una granja para listar todos los liquidados en ese alcance.");
        }

        if (string.Equals(request.Modo, "Rango", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.FechaDesde.HasValue || !request.FechaHasta.HasValue)
                throw new InvalidOperationException("FechaDesde y FechaHasta son obligatorias en modo Rango.");

            var fechaFinInclusive = request.FechaHasta.Value.Date.AddDays(1).AddTicks(-1);
            var alcance = string.IsNullOrWhiteSpace(request.Alcance) ? "TodasLasGranjas" : request.Alcance.Trim();

            var query = _context.LoteAveEngorde
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Galpon)
                .Where(l => l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);

            if (string.Equals(alcance, "Granja", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(alcance, "Nucleo", StringComparison.OrdinalIgnoreCase))
            {
                if (!request.GranjaId.HasValue)
                    throw new InvalidOperationException("GranjaId es obligatorio para alcance Granja o Nucleo.");
                query = query.Where(l => l.GranjaId == request.GranjaId.Value);
            }

            if (string.Equals(alcance, "Nucleo", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(request.NucleoId))
                    throw new InvalidOperationException("NucleoId es obligatorio para alcance Nucleo.");
                query = query.Where(l => l.NucleoId == request.NucleoId);
            }

            var lotes = await query.OrderBy(l => l.LoteNombre).ToListAsync(ct).ConfigureAwait(false);
            var (soloFiltroRango, usarAdmRango) = ResolveFiltroLotes(request.TipoFiltroLotes);

            foreach (var lote in lotes)
            {
                var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, soloFiltroRango, pesoAjuste, divisorAjuste, usarAdmRango).ConfigureAwait(false);
                if (ind == null)
                    continue;
                if (!ind.FechaCierreLote.HasValue)
                    continue;
                if (ind.FechaCierreLote.Value < request.FechaDesde.Value.Date ||
                    ind.FechaCierreLote.Value > fechaFinInclusive)
                    continue;

                var id = lote.LoteAveEngordeId ?? 0;
                items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
            }

            return new LiquidacionPolloEngordeReporteDto("Rango", items);
        }

        if (string.Equals(request.Modo, "TodosLiquidados", StringComparison.OrdinalIgnoreCase))
        {
            if (!request.GranjaId.HasValue || request.GranjaId.Value <= 0)
                throw new InvalidOperationException("GranjaId es obligatorio para modo TodosLiquidados.");

            var query = _context.LoteAveEngorde
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Galpon)
                .Where(l => l.CompanyId == _currentUser.CompanyId &&
                            l.DeletedAt == null &&
                            l.GranjaId == request.GranjaId.Value);

            if (!string.IsNullOrWhiteSpace(request.NucleoId))
                query = query.Where(l => l.NucleoId == request.NucleoId);

            if (!string.IsNullOrWhiteSpace(request.GalponId))
                query = query.Where(l => l.GalponId == request.GalponId);

            if (!string.IsNullOrWhiteSpace(request.LoteCodigo))
                query = query.Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(request.LoteCodigo));

            var lotes = await query.OrderBy(l => l.LoteNombre).ToListAsync(ct).ConfigureAwait(false);
            var (soloFiltroTodos, usarAdmTodos) = ResolveFiltroLotes(request.TipoFiltroLotes);

            foreach (var lote in lotes)
            {
                var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, soloFiltroTodos, pesoAjuste, divisorAjuste, usarAdmTodos).ConfigureAwait(false);
                if (ind == null)
                    continue;
                var id = lote.LoteAveEngordeId ?? 0;
                items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
            }

            if (items.Count == 0)
                throw new InvalidOperationException(
                    "No hay lotes que cumplan el filtro en el alcance seleccionado. Ajuste granja, núcleo, galpón, código de lote o TipoFiltroLotes.");

            return new LiquidacionPolloEngordeReporteDto("TodosLiquidados", items);
        }

        throw new InvalidOperationException("Modo debe ser UnLote, Rango o TodosLiquidados.");
    }

    /// <inheritdoc />
    public async Task<string> AuditarLiquidacionAsync(
        AuditoriaLiquidacionRequest request,
        IReadOnlyDictionary<string, decimal?> valoresExcel,
        CancellationToken ct = default)
    {
        // Solo los valores presentes; se serializan a JSON para el parámetro jsonb de la función.
        // Toda la lógica de auditoría (reconciliación, hallazgos, simulación) vive en BD.
        var presentes = valoresExcel
            .Where(kv => kv.Value.HasValue)
            .ToDictionary(kv => kv.Key, kv => kv.Value!.Value);
        var excelJson = System.Text.Json.JsonSerializer.Serialize(presentes);

        var rows = await _context.Database
            .SqlQueryRaw<string>(
                "SELECT fn_auditoria_liquidacion_engorde({0}, {1}, {2}, {3}, {4}::jsonb)::text AS \"Value\"",
                _currentUser.CompanyId,
                request.GranjaId,
                (object?)request.NucleoId ?? DBNull.Value,
                (object?)request.LoteCodigo ?? DBNull.Value,
                excelJson)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.FirstOrDefault() ?? "{}";
    }

    /// <inheritdoc />
    public async Task<string> AplicarCorreccionSinPesoAsync(
        AplicarCorreccionRequest request,
        CancellationToken ct = default)
    {
        var rows = await _context.Database
            .SqlQueryRaw<string>(
                "SELECT fn_aplicar_correccion_despachos_sin_peso({0}, {1}, {2}, {3}, {4}, {5})::text AS \"Value\"",
                _currentUser.CompanyId,
                request.GranjaId,
                (object?)request.NucleoId ?? DBNull.Value,
                (object?)request.LoteCodigo ?? DBNull.Value,
                request.KgTotal,
                _currentUser.UserId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return rows.FirstOrDefault() ?? "{\"ok\":false,\"error\":\"Sin respuesta de la función.\"}";
    }

    /// <summary>
    /// Calcula indicadores de Pollo Engorde (Lote padre + reproductores) según documento
    /// LIQUIDACION_TECNICA_POLLO_ENGORDE.md: aves encasetadas, sacrificadas, mortalidad+selección,
    /// consumo, kg carne (despachos), conversión, conv. ajustada (variables 2,7 y 4,5), edad, m², eficiencias, etc.
    /// Solo se liquida cuando el lote tiene cero aves.
    /// </summary>
    public async Task<IndicadorPolloEngordePorLotePadreDto> CalcularIndicadoresPolloEngordePorLotePadreAsync(
        IndicadorPolloEngordePorLotePadreRequest request)
    {
        var lotePadre = await _context.LoteAveEngorde
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Galpon)
            .Where(l => l.LoteAveEngordeId == request.LoteAveEngordeId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lotePadre == null)
            throw new InvalidOperationException($"No se encontró el lote de ave engorde {request.LoteAveEngordeId}.");

        var pesoAjuste = request.PesoAjusteVariable ?? PesoAjusteDefault;
        var divisorAjuste = request.DivisorAjusteVariable ?? DivisorAjusteDefault;

        var indicadorPadre = await CalcularIndicadorLoteAveEngordeAsync(lotePadre, request.FechaDesde, request.FechaHasta, request.SoloLotesCerrados, pesoAjuste, divisorAjuste);
        // Cuando "Solo lotes cerrados" está activo y el lote padre no está cerrado, no lanzar: igual se devuelven los reproductores con 0 aves asociados.
        // Así el usuario puede ver los lotes reproductores ya cerrados (AB0) aunque el padre aún tenga aves.

        var reproductores = await _context.LoteReproductoraAveEngorde
            .AsNoTracking()
            .Where(r => r.LoteAveEngordeId == request.LoteAveEngordeId)
            .OrderBy(r => r.Id)
            .ToListAsync();

        var listReproductores = new List<IndicadorReproductorDto>();
        foreach (var rep in reproductores)
        {
            var indRep = await CalcularIndicadorLoteReproductoraAveEngordeAsync(rep, lotePadre.GranjaId, request.FechaDesde, request.FechaHasta, request.SoloLotesCerrados, pesoAjuste, divisorAjuste);
            if (indRep != null)
                listReproductores.Add(new IndicadorReproductorDto(rep.Id, rep.NombreLote, indRep));
        }

        // Si no se filtró por cerrados, el padre debe existir; si se filtró por cerrados, se permite padre null y solo se muestran reproductores con 0 aves.
        if (indicadorPadre == null && !request.SoloLotesCerrados)
            throw new InvalidOperationException("Error calculando indicador del lote padre.");

        return new IndicadorPolloEngordePorLotePadreDto(indicadorPadre, listReproductores);
    }

    // ========== Métodos privados (pollo engorde) ==========

    private async Task<IndicadorEcuadorDto?> CalcularIndicadorLoteAveEngordeAsync(
        LoteAveEngorde lote,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        bool soloLotesCerrados,
        decimal pesoAjuste,
        decimal divisorAjuste,
        bool usarCierreAdministrativo = false)
    {
        var loteId = lote.LoteAveEngordeId ?? 0;
        // Parte A (performance): el cálculo que antes hacía 8-10 queries N+1 por lote se consolidó
        // en fn_indicadores_pollo_engorde (incluye el fix R3.1 de peso individual y los campos R1/R2).
        var rows = await _context.Database
            .SqlQueryRaw<IndicadorEcuadorRow>(
                "SELECT * FROM fn_indicadores_pollo_engorde({0}::int, {1}::numeric, {2}::numeric)",
                loteId, pesoAjuste, divisorAjuste)
            .ToListAsync();
        var r = rows.FirstOrDefault();
        if (r is null) return null;

        // Filtros administrativos (idénticos a la versión previa; usan los marcadores de la función).
        if (soloLotesCerrados)
        {
            if (usarCierreAdministrativo)
            {
                var adminCerrado = r.EstadoOperativoLote != "Abierto"
                                || r.LiquidadoAtMarker != null
                                || r.LoteCerrado
                                || r.RatioSacrificadas >= 0.9m;
                if (!adminCerrado) return null;
            }
            else if (!r.LoteCerrado)
            {
                return null;
            }
        }

        return r.ToDto();
    }

    private async Task<IndicadorEcuadorDto?> CalcularIndicadorLoteReproductoraAveEngordeAsync(
        LoteReproductoraAveEngorde rep,
        int granjaId,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        bool soloLotesCerrados,
        decimal pesoAjuste,
        decimal divisorAjuste)
    {
        var avesEncasetadas = (rep.AvesInicioHembras ?? 0) + (rep.AvesInicioMachos ?? 0) + (rep.Mixtas ?? 0);
        if (avesEncasetadas == 0) avesEncasetadas = (rep.H ?? 0) + (rep.M ?? 0) + (rep.Mixtas ?? 0);

        var avesSacrificadas = await AvesSacrificadasPolloEngordeAsync(loteAveEngordeId: null, loteReproductoraId: rep.Id);
        var (mortalidad, seleccion) = await MortalidadSeleccionReproductoraAveEngordeAsync(rep.Id);
        var mortalidadTotal = mortalidad + seleccion;
        var mortalidadPorcentaje = avesEncasetadas > 0 ? (decimal)mortalidadTotal / avesEncasetadas * 100 : 0;
        var supervivenciaPorcentaje = avesEncasetadas > 0 ? (decimal)(avesEncasetadas - mortalidadTotal) / avesEncasetadas * 100 : 0;

        var consumoTotal = await ConsumoPolloEngordeAsync(loteAveEngordeId: null, loteReproductoraId: rep.Id);
        var consumoAveGramos = avesSacrificadas > 0 ? consumoTotal / avesSacrificadas * 1000 : 0;

        var (kgCarne, edadPromedio) = await KgCarneYEdadPolloEngordeAsync(loteAveEngordeId: null, loteReproductoraId: rep.Id);
        var pesoPromedio = avesSacrificadas > 0 ? kgCarne / avesSacrificadas : 0;
        var conversion = kgCarne > 0 ? consumoTotal / kgCarne : 0;
        var conversionAjustada = CalcularConversionAjustada(conversion, pesoPromedio, pesoAjuste, divisorAjuste);

        var galponId = await _context.LoteAveEngorde.Where(l => l.LoteAveEngordeId == rep.LoteAveEngordeId).Select(l => l.GalponId).FirstOrDefaultAsync();
        var metrosCuadrados = await CalcularMetrosCuadradosAsync(galponId, granjaId);
        var avesPorM2 = metrosCuadrados > 0 ? avesSacrificadas / metrosCuadrados : 0;
        var kgPorM2 = metrosCuadrados > 0 ? kgCarne / metrosCuadrados : 0;

        var eficienciaAmericana = conversion > 0 ? (pesoPromedio / conversion) * 100 : 0;
        var eficienciaEuropea = (conversion > 0 && edadPromedio > 0) ? ((pesoPromedio * supervivenciaPorcentaje) / (edadPromedio * conversion)) * 100 : 0;
        var indiceProductividad = conversion > 0 ? (pesoPromedio / conversion) / conversion * 100 : 0;
        var gananciaDia = edadPromedio > 0 ? (pesoPromedio / edadPromedio) * 1000 : 0;

        var avesActuales = avesEncasetadas - mortalidadTotal - avesSacrificadas;
        var loteCerrado = Math.Max(0, avesActuales) == 0;
        if (soloLotesCerrados && !loteCerrado) return null;

        var fechaCierre = await FechaCierrePolloEngordeAsync(loteAveEngordeId: null, loteReproductoraId: rep.Id);
        var granjaNombre = await _context.Farms.Where(f => f.Id == granjaId).Select(f => f.Name).FirstOrDefaultAsync() ?? "";

        return new IndicadorEcuadorDto(
            granjaId,
            granjaNombre,
            rep.LoteAveEngordeId,
            rep.NombreLote,
            galponId,
            null,
            avesEncasetadas,
            avesSacrificadas,
            mortalidadTotal,
            mortalidadPorcentaje,
            supervivenciaPorcentaje,
            consumoTotal,
            consumoAveGramos,
            kgCarne,
            pesoPromedio,
            conversion,
            conversionAjustada,
            pesoAjuste,
            divisorAjuste,
            edadPromedio,
            metrosCuadrados,
            (decimal)avesPorM2,
            kgPorM2,
            eficienciaAmericana,
            eficienciaEuropea,
            indiceProductividad,
            gananciaDia,
            rep.FechaEncasetamiento,
            fechaCierre,
            loteCerrado
        );
    }

    // NOTA: la lógica de "lote padre cerrado" (traslados a reproductores + todos los
    // reproductores en cero) se consolidó en fn_indicadores_pollo_engorde (Parte A).

    private async Task<int> AvesSacrificadasPolloEngordeAsync(int? loteAveEngordeId, int? loteReproductoraId)
    {
        var q = _context.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null &&
                (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro"));
        if (loteAveEngordeId.HasValue)
            q = q.Where(m => m.LoteAveEngordeOrigenId == loteAveEngordeId.Value);
        else if (loteReproductoraId.HasValue)
            q = q.Where(m => m.LoteReproductoraAveEngordeOrigenId == loteReproductoraId.Value);
        else
            return 0;
        var total = await q.SumAsync(m => (int?)(m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas));
        return total ?? 0;
    }

    private async Task<(int mortalidad, int seleccion)> MortalidadSeleccionReproductoraAveEngordeAsync(int loteReproductoraId)
    {
        var seg = await _context.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
            .Where(s => s.LoteReproductoraAveEngordeId == loteReproductoraId)
            .GroupBy(s => 1)
            .Select(g => new { Mort = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0)), Sel = g.Sum(s => (s.SelH ?? 0) + (s.SelM ?? 0)) })
            .FirstOrDefaultAsync();
        return (seg?.Mort ?? 0, seg?.Sel ?? 0);
    }

    private async Task<decimal> ConsumoPolloEngordeAsync(int? loteAveEngordeId, int? loteReproductoraId)
    {
        if (loteAveEngordeId.HasValue)
        {
            var c = await _context.SeguimientoDiarioAvesEngorde.AsNoTracking()
                .Where(s => s.LoteAveEngordeId == loteAveEngordeId.Value)
                .SumAsync(s => (decimal?)((s.ConsumoKgHembras ?? 0) + (s.ConsumoKgMachos ?? 0)));
            return c ?? 0;
        }
        if (loteReproductoraId.HasValue)
        {
            var c = await _context.SeguimientoDiarioLoteReproductoraAvesEngorde.AsNoTracking()
                .Where(s => s.LoteReproductoraAveEngordeId == loteReproductoraId.Value)
                .SumAsync(s => (decimal?)((s.ConsumoKgHembras ?? 0) + (s.ConsumoKgMachos ?? 0)));
            return c ?? 0;
        }
        return 0;
    }

    private async Task<(decimal kgCarne, decimal edadPromedio)> KgCarneYEdadPolloEngordeAsync(int? loteAveEngordeId, int? loteReproductoraId)
    {
        var q = _context.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null &&
                (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro"));
        if (loteAveEngordeId.HasValue)
            q = q.Where(m => m.LoteAveEngordeOrigenId == loteAveEngordeId.Value);
        else if (loteReproductoraId.HasValue)
            q = q.Where(m => m.LoteReproductoraAveEngordeOrigenId == loteReproductoraId.Value);
        else
            return (0, 0);
        var movs = await q.ToListAsync();
        // FIX (Parte C / R3.1): usar el peso neto INDIVIDUAL prorrateado por lote.
        // peso_bruto/peso_tara guardan el peso GLOBAL del camión clonado en cada línea del
        // despacho ⇒ SUM(peso_bruto-peso_tara) sobrecuenta los kg en despachos multi-lote.
        // peso_neto ya es el individual (= bruto-tara en movimientos de 1 línea). Fallback para
        // movimientos antiguos sin peso_neto poblado.
        var kgCarne = (decimal)movs.Sum(m =>
            m.PesoNeto ?? ((m.PesoBruto.HasValue && m.PesoTara.HasValue) ? m.PesoBruto!.Value - m.PesoTara!.Value : 0d));
        var edades = movs.Where(m => m.EdadAves.HasValue).Select(m => (decimal)m.EdadAves!.Value).ToList();
        var edadPromedio = edades.Any() ? edades.Average() : 0;
        return (kgCarne, edadPromedio);
    }

    private async Task<DateTime?> FechaCierrePolloEngordeAsync(int? loteAveEngordeId, int? loteReproductoraId)
    {
        var q = _context.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null &&
                (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho" || m.TipoMovimiento == "Retiro"));
        if (loteAveEngordeId.HasValue)
            q = q.Where(m => m.LoteAveEngordeOrigenId == loteAveEngordeId.Value);
        else if (loteReproductoraId.HasValue)
            q = q.Where(m => m.LoteReproductoraAveEngordeOrigenId == loteReproductoraId.Value);
        else
            return null;
        // MaxAsync sobre vacío lanza; FirstOrDefault devuelve null si no hay ventas/despachos/retiros.
        return await q
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => (DateTime?)m.FechaMovimiento)
            .FirstOrDefaultAsync();
    }
}
