// src/ZooSanMarino.Infrastructure/Services/IndicadorEcuadorService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class IndicadorEcuadorService : IIndicadorEcuadorService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    
    // Variables configurables para conversión ajustada
    private const decimal PesoAjusteDefault = 2.7m;
    private const decimal DivisorAjusteDefault = 4.5m;

    public IndicadorEcuadorService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<IEnumerable<IndicadorEcuadorDto>> CalcularIndicadoresAsync(IndicadorEcuadorRequest request)
    {
        try
        {
            var lotes = await ObtenerLotesAsync(request);
            var resultados = new List<IndicadorEcuadorDto>();

            foreach (var lote in lotes)
            {
                try
                {
                    var indicador = await CalcularIndicadorPorLoteAsync(lote, request);
                    if (indicador != null)
                    {
                        resultados.Add(indicador);
                    }
                }
                catch (Exception ex)
                {
                    // Log error pero continúa con el siguiente lote
                    System.Diagnostics.Debug.WriteLine($"Error calculando indicador para lote {lote.LoteId}: {ex.Message}");
                    continue;
                }
            }

            return resultados;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en CalcularIndicadoresAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    public async Task<IndicadorEcuadorConsolidadoDto> CalcularConsolidadoAsync(IndicadorEcuadorRequest request)
    {
        var indicadores = await CalcularIndicadoresAsync(request);
        var indicadoresList = indicadores.ToList();

        if (!indicadoresList.Any())
        {
            return new IndicadorEcuadorConsolidadoDto(
                DateTime.UtcNow,  // FechaCalculo
                0,  // TotalGranjas
                0,  // TotalLotes
                0,  // TotalLotesCerrados
                0,  // TotalAvesEncasetadas
                0,  // TotalAvesSacrificadas
                0,  // TotalMortalidad
                0m, // PromedioMortalidadPorcentaje
                0m, // PromedioSupervivenciaPorcentaje
                0m, // TotalConsumoAlimentoKg
                0m, // PromedioConsumoAveGramos
                0m, // TotalKgCarnePollos
                0m, // PromedioPesoKilos
                0m, // PromedioConversion
                0m, // PromedioConversionAjustada
                0m, // PromedioEdad
                0m, // TotalMetrosCuadrados
                0m, // PromedioAvesPorMetroCuadrado
                0m, // PromedioKgPorMetroCuadrado
                0m, // PromedioEficienciaAmericana
                0m, // PromedioEficienciaEuropea
                0m, // PromedioIndiceProductividad
                0m, // PromedioGananciaDia
                Enumerable.Empty<IndicadorEcuadorDto>() // IndicadoresPorGranja
            );
        }

        var totalGranjas = indicadoresList.Select(i => i.GranjaId).Distinct().Count();
        var totalLotes = indicadoresList.Count;
        var totalLotesCerrados = indicadoresList.Count(i => i.LoteCerrado);

        // Totales (conglomerado de todas las granjas)
        var totalAvesEncasetadas = indicadoresList.Sum(i => i.AvesEncasetadas);
        var totalAvesSacrificadas = indicadoresList.Sum(i => i.AvesSacrificadas);
        var totalMortalidad = indicadoresList.Sum(i => i.Mortalidad);
        var totalConsumoAlimento = indicadoresList.Sum(i => i.ConsumoTotalAlimentoKg);
        var totalKgCarne = indicadoresList.Sum(i => i.KgCarnePollos);
        var totalMetrosCuadrados = indicadoresList.Sum(i => i.MetrosCuadrados);

        // Indicadores consolidados calculados desde totales (fórmulas LIQUIDACIÓN TÉCNICA)
        var promedioMortalidad = totalAvesEncasetadas > 0
            ? (decimal)totalMortalidad / totalAvesEncasetadas * 100
            : 0;
        var supervivenciaPorcentajeConsolidado = totalAvesEncasetadas > 0
            ? (decimal)(totalAvesEncasetadas - totalMortalidad) / totalAvesEncasetadas * 100
            : 0;
        var promedioConsumoAve = totalAvesSacrificadas > 0
            ? totalConsumoAlimento / totalAvesSacrificadas * 1000  // Consumo ave (g)
            : 0;
        var promedioPeso = totalAvesSacrificadas > 0
            ? totalKgCarne / totalAvesSacrificadas  // Peso promedio Kilos
            : 0;
        var conversionConsolidada = totalKgCarne > 0
            ? totalConsumoAlimento / totalKgCarne  // Conversion = Consumo total / Kg Carne
            : 0;
        var pesoAjuste = indicadoresList.FirstOrDefault()?.PesoAjusteVariable ?? PesoAjusteDefault;
        var divisorAjuste = indicadoresList.FirstOrDefault()?.DivisorAjusteVariable ?? DivisorAjusteDefault;
        var promedioConversionAjustada = CalcularConversionAjustada(conversionConsolidada, promedioPeso, pesoAjuste, divisorAjuste);
        // Edad = promedio de los saques de pollo (galpones/lotes)
        var lotesConEdad = indicadoresList.Where(i => i.EdadPromedio > 0).ToList();
        var promedioEdad = lotesConEdad.Any()
            ? lotesConEdad.Average(i => i.EdadPromedio)
            : 0;
        var promedioAvesPorM2 = totalMetrosCuadrados > 0
            ? totalAvesSacrificadas / totalMetrosCuadrados  // Aves / M²
            : 0;
        var promedioKgPorM2 = totalMetrosCuadrados > 0
            ? totalKgCarne / totalMetrosCuadrados  // KG/M²
            : 0;
        var promedioEficienciaAmericana = conversionConsolidada > 0
            ? (promedioPeso / conversionConsolidada) * 100  // (Peso Promedio / Conversion) * 100
            : 0;
        var promedioEficienciaEuropea = (conversionConsolidada > 0 && promedioEdad > 0)
            ? ((promedioPeso * supervivenciaPorcentajeConsolidado) / (promedioEdad * conversionConsolidada)) * 100  // Eficiencia Europea
            : 0;
        var promedioIndiceProductividad = conversionConsolidada > 0
            ? (promedioPeso / conversionConsolidada) / conversionConsolidada * 100  // I. Productividad
            : 0;
        var promedioGananciaDia = promedioEdad > 0
            ? (promedioPeso / promedioEdad) * 1000  // Ganancia Día
            : 0;

        return new IndicadorEcuadorConsolidadoDto(
            DateTime.UtcNow,
            totalGranjas,
            totalLotes,
            totalLotesCerrados,
            totalAvesEncasetadas,
            totalAvesSacrificadas,
            totalMortalidad,
            promedioMortalidad,
            supervivenciaPorcentajeConsolidado,
            totalConsumoAlimento,
            promedioConsumoAve,
            totalKgCarne,
            promedioPeso,
            conversionConsolidada,
            promedioConversionAjustada,
            promedioEdad,
            totalMetrosCuadrados,
            promedioAvesPorM2,
            promedioKgPorM2,
            promedioEficienciaAmericana,
            promedioEficienciaEuropea,
            promedioIndiceProductividad,
            promedioGananciaDia,
            indicadoresList
        );
    }

    /// <summary>
    /// Liquidación por período: solo granjas que finalizaron lote (aves = 0) en el rango.
    /// Semanal (viernes) o Mensual (primeros días de la semana). Solo lotes cerrados cuya fecha de cierre está en [fechaInicio, fechaFin].
    /// </summary>
    public async Task<LiquidacionPeriodoDto> CalcularLiquidacionPeriodoAsync(
        DateTime fechaInicio,
        DateTime fechaFin,
        string tipoPeriodo,
        int? granjaId = null)
    {
        var request = new IndicadorEcuadorRequest(
            GranjaId: granjaId,
            FechaDesde: null,
            FechaHasta: null,
            SoloLotesCerrados: true
        );

        var indicadores = await CalcularIndicadoresAsync(request);
        var indicadoresList = indicadores.ToList();

        // Solo lotes que cerraron (último despacho) dentro del período
        var fechaFinInclusive = fechaFin.Date.AddDays(1).AddTicks(-1);
        var enPeriodo = indicadoresList
            .Where(i => i.FechaCierreLote.HasValue &&
                        i.FechaCierreLote.Value >= fechaInicio &&
                        i.FechaCierreLote.Value <= fechaFinInclusive)
            .ToList();

        return new LiquidacionPeriodoDto(
            fechaInicio,
            fechaFin,
            tipoPeriodo,
            enPeriodo.Select(i => i.GranjaId).Distinct().Count(),
            enPeriodo.Count,
            enPeriodo
        );
    }

    public async Task<IEnumerable<IndicadorEcuadorDto>> ObtenerLotesCerradosAsync(
        DateTime fechaDesde,
        DateTime fechaHasta,
        int? granjaId = null,
        bool soloCerrados = true)
    {
        if (soloCerrados)
        {
            // Solo lotes cerrados (aves = 0). La franja fecha inicio/fin aplica a FECHA DE CIERRE (último despacho),
            // no a fecha de encaset (misma idea que liquidación por período).
            var request = new IndicadorEcuadorRequest(
                GranjaId: granjaId,
                FechaDesde: null,
                FechaHasta: null,
                SoloLotesCerrados: true
            );

            var indicadores = (await CalcularIndicadoresAsync(request).ConfigureAwait(false)).ToList();
            var fechaFinInclusive = fechaHasta.Date.AddDays(1).AddTicks(-1);
            return indicadores.Where(i =>
                i.FechaCierreLote.HasValue &&
                i.FechaCierreLote.Value >= fechaDesde.Date &&
                i.FechaCierreLote.Value <= fechaFinInclusive);
        }

        // Franja por fecha de encaset; incluye lotes abiertos en ese rango
        var requestAbierto = new IndicadorEcuadorRequest(
            GranjaId: granjaId,
            FechaDesde: fechaDesde,
            FechaHasta: fechaHasta,
            SoloLotesCerrados: false
        );

        return await CalcularIndicadoresAsync(requestAbierto).ConfigureAwait(false);
    }

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

                var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, true, pesoAjuste, divisorAjuste).ConfigureAwait(false);
                if (ind == null)
                    throw new InvalidOperationException("El lote no está liquidado (aún tiene aves o no cumple criterios de liquidación). Solo se muestran lotes con aves en cero.");

                var id = lote.LoteAveEngordeId ?? 0;
                items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
                return new LiquidacionPolloEngordeReporteDto("UnLote", items);
            }

            // Varios lotes liquidados: granja y opcionalmente núcleo / galpón (sin id de lote)
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

                foreach (var lote in lotes)
                {
                    var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, true, pesoAjuste, divisorAjuste).ConfigureAwait(false);
                    if (ind == null)
                        continue;
                    var id = lote.LoteAveEngordeId ?? 0;
                    items.Add(new LiquidacionPolloEngordeItemDto(id, lote.LoteNombre ?? "", ind));
                }

                if (items.Count == 0)
                    throw new InvalidOperationException(
                        "No hay lotes liquidados en el alcance seleccionado (aves = 0). Ajuste granja, núcleo o galpón, o elija un lote en la lista.");

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

            foreach (var lote in lotes)
            {
                var ind = await CalcularIndicadorLoteAveEngordeAsync(lote, null, null, true, pesoAjuste, divisorAjuste).ConfigureAwait(false);
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

        throw new InvalidOperationException("Modo debe ser UnLote o Rango.");
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
        decimal divisorAjuste)
    {
        var loteId = lote.LoteAveEngordeId ?? 0;
        var avesEncasetadas = lote.AvesEncasetadas ?? (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0);

        var avesSacrificadas = await AvesSacrificadasPolloEngordeAsync(loteAveEngordeId: loteId, loteReproductoraId: null);
        var (mortalidad, seleccion) = await MortalidadSeleccionAvesEngordeAsync(loteAveEngordeId: loteId, loteReproductoraId: null);
        var mortalidadTotal = mortalidad + seleccion;
        var mortalidadPorcentaje = avesEncasetadas > 0 ? (decimal)mortalidadTotal / avesEncasetadas * 100 : 0;
        var supervivenciaPorcentaje = avesEncasetadas > 0 ? (decimal)(avesEncasetadas - mortalidadTotal) / avesEncasetadas * 100 : 0;

        var consumoTotal = await ConsumoPolloEngordeAsync(loteAveEngordeId: loteId, loteReproductoraId: null);
        var consumoAveGramos = avesSacrificadas > 0 ? consumoTotal / avesSacrificadas * 1000 : 0;

        var (kgCarne, edadPromedio) = await KgCarneYEdadPolloEngordeAsync(loteAveEngordeId: loteId, loteReproductoraId: null);
        var pesoPromedio = avesSacrificadas > 0 ? kgCarne / avesSacrificadas : 0;
        var conversion = kgCarne > 0 ? consumoTotal / kgCarne : 0;
        var conversionAjustada = CalcularConversionAjustada(conversion, pesoPromedio, pesoAjuste, divisorAjuste);

        var metrosCuadrados = await CalcularMetrosCuadradosAsync(lote.GalponId, lote.GranjaId);
        var avesPorM2 = metrosCuadrados > 0 ? avesSacrificadas / metrosCuadrados : 0;
        var kgPorM2 = metrosCuadrados > 0 ? kgCarne / metrosCuadrados : 0;

        var eficienciaAmericana = conversion > 0 ? (pesoPromedio / conversion) * 100 : 0;
        var eficienciaEuropea = (conversion > 0 && edadPromedio > 0) ? ((pesoPromedio * supervivenciaPorcentaje) / (edadPromedio * conversion)) * 100 : 0;
        var indiceProductividad = conversion > 0 ? (pesoPromedio / conversion) / conversion * 100 : 0;
        var gananciaDia = edadPromedio > 0 ? (pesoPromedio / edadPromedio) * 1000 : 0;

        // Aves que salieron por Traslado a reproductores también cuentan como "salidas" del padre
        var avesTrasladadasAReproductores = await AvesTrasladadasDesdePadreHaciaReproductoresAsync(loteId);
        var avesActuales = avesEncasetadas - mortalidadTotal - avesSacrificadas - avesTrasladadasAReproductores;
        // Lote padre cerrado: aves actuales = 0, o bien todas las aves están en reproductores y todos los reproductores ya vendieron (0 aves)
        var cerradoPorAvesCero = Math.Max(0, avesActuales) == 0;
        var cerradoPorReproductoresVendidos = !cerradoPorAvesCero && avesSacrificadas == 0 && mortalidadTotal == 0 &&
            await TodosReproductoresConCeroAvesAsync(loteId);
        var loteCerrado = cerradoPorAvesCero || cerradoPorReproductoresVendidos;
        if (soloLotesCerrados && !loteCerrado) return null;

        var fechaCierre = await FechaCierrePolloEngordeAsync(loteAveEngordeId: loteId, loteReproductoraId: null);
        // Sin Venta/Despacho/Retiro la fecha puede ser null aunque el lote esté en cero (p. ej. solo mortalidad/seguimiento).
        if (!fechaCierre.HasValue && loteCerrado)
            fechaCierre = await FechaUltimaActividadLotePadreAveEngordeAsync(loteId, lote.FechaEncaset);

        return new IndicadorEcuadorDto(
            lote.GranjaId,
            lote.Farm?.Name ?? "",
            loteId,
            lote.LoteNombre,
            lote.GalponId,
            lote.Galpon?.GalponNombre ?? "",
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
            lote.FechaEncaset,
            fechaCierre,
            loteCerrado
        );
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

    /// <summary>
    /// Aves que salieron del lote padre por Traslado hacia lotes reproductores (descuento para considerar lote padre cerrado cuando ya no tiene aves).
    /// </summary>
    private async Task<int> AvesTrasladadasDesdePadreHaciaReproductoresAsync(int loteAveEngordeId)
    {
        var total = await _context.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.Estado != "Cancelado" && m.DeletedAt == null &&
                m.TipoMovimiento == "Traslado" &&
                m.LoteAveEngordeOrigenId == loteAveEngordeId &&
                m.LoteReproductoraAveEngordeDestinoId != null)
            .SumAsync(m => (int?)(m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas));
        return total ?? 0;
    }

    /// <summary>
    /// True si el lote padre tiene reproductores y todos ellos tienen 0 aves actuales (vendidas/cerrados).
    /// Usado para considerar el lote padre "cerrado" cuando todas las aves se distribuyeron a reproductores y estos ya vendieron todo.
    /// </summary>
    private async Task<bool> TodosReproductoresConCeroAvesAsync(int loteAveEngordeId)
    {
        var reps = await _context.LoteReproductoraAveEngorde.AsNoTracking()
            .Where(r => r.LoteAveEngordeId == loteAveEngordeId)
            .ToListAsync();
        if (reps.Count == 0) return false;
        foreach (var rep in reps)
        {
            var encaset = (rep.AvesInicioHembras ?? 0) + (rep.AvesInicioMachos ?? 0) + (rep.Mixtas ?? 0);
            if (encaset == 0) encaset = (rep.H ?? 0) + (rep.M ?? 0) + (rep.Mixtas ?? 0);
            var ventas = await AvesSacrificadasPolloEngordeAsync(loteAveEngordeId: null, loteReproductoraId: rep.Id);
            var (mort, sel) = await MortalidadSeleccionReproductoraAveEngordeAsync(rep.Id);
            var actuales = encaset - mort - sel - ventas;
            if (actuales > 0) return false;
        }
        return true;
    }

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

    private async Task<(int mortalidad, int seleccion)> MortalidadSeleccionAvesEngordeAsync(int loteAveEngordeId, int? loteReproductoraId)
    {
        var seg = await _context.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId)
            .GroupBy(s => 1)
            .Select(g => new { Mort = g.Sum(s => (s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0)), Sel = g.Sum(s => (s.SelH ?? 0) + (s.SelM ?? 0)) })
            .FirstOrDefaultAsync();
        return (seg?.Mort ?? 0, seg?.Sel ?? 0);
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
        var kgCarne = (decimal)movs.Where(m => m.PesoBruto.HasValue && m.PesoTara.HasValue).Sum(m => m.PesoBruto!.Value - m.PesoTara!.Value);
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

    /// <summary>
    /// Última fecha útil para reporte/filtro cuando no hay fecha de último despacho:
    /// último seguimiento diario, o último movimiento de pollo engorde, o fecha de encaset.
    /// </summary>
    private async Task<DateTime?> FechaUltimaActividadLotePadreAveEngordeAsync(int loteAveEngordeId, DateTime? fechaEncaset)
    {
        var ultSeg = await _context.SeguimientoDiarioAvesEngorde.AsNoTracking()
            .Where(s => s.LoteAveEngordeId == loteAveEngordeId)
            .OrderByDescending(s => s.Fecha)
            .Select(s => (DateTime?)s.Fecha)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (ultSeg.HasValue) return ultSeg;

        var ultMov = await _context.MovimientoPolloEngorde.AsNoTracking()
            .Where(m => m.LoteAveEngordeOrigenId == loteAveEngordeId && m.Estado != "Cancelado" && m.DeletedAt == null)
            .OrderByDescending(m => m.FechaMovimiento)
            .Select(m => (DateTime?)m.FechaMovimiento)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        if (ultMov.HasValue) return ultMov;

        return fechaEncaset;
    }

    // ========== Métodos privados ==========

    private async Task<List<LoteInfo>> ObtenerLotesAsync(IndicadorEcuadorRequest request)
    {
        try
        {
            var query = _context.Lotes
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Galpon)
                .Where(l => l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null);

            if (request.GranjaId.HasValue)
                query = query.Where(l => l.GranjaId == request.GranjaId.Value);

            if (!string.IsNullOrEmpty(request.NucleoId))
                query = query.Where(l => l.NucleoId == request.NucleoId);

            if (!string.IsNullOrEmpty(request.GalponId))
                query = query.Where(l => l.GalponId == request.GalponId);

            if (request.LoteId.HasValue)
                query = query.Where(l => l.LoteId == request.LoteId.Value);

            if (request.FechaDesde.HasValue)
                query = query.Where(l => l.FechaEncaset >= request.FechaDesde.Value);

            if (request.FechaHasta.HasValue)
                query = query.Where(l => l.FechaEncaset <= request.FechaHasta.Value);

            var lotes = await query.ToListAsync();

            // Filtrar por tipo de lote si es necesario
            if (request.TipoLote != "Todos")
            {
                // Determinar tipo de lote basado en seguimientos
                var lotesFiltrados = new List<LoteInfo>();
                foreach (var lote in lotes)
                {
                    var tipo = await DeterminarTipoLoteAsync(lote.LoteId ?? 0);
                    if (request.TipoLote == tipo || tipo == "Mixto")
                    {
                        lotesFiltrados.Add(new LoteInfo
                        {
                            LoteId = lote.LoteId ?? 0,
                            LoteNombre = lote.LoteNombre,
                            GranjaId = lote.GranjaId,
                            GranjaNombre = lote.Farm?.Name ?? "",
                            GalponId = lote.GalponId,
                            GalponNombre = lote.Galpon?.GalponNombre ?? "",
                            FechaEncaset = lote.FechaEncaset
                        });
                    }
                }
                return lotesFiltrados;
            }

            return lotes.Select(l => new LoteInfo
            {
                LoteId = l.LoteId ?? 0,
                LoteNombre = l.LoteNombre,
                GranjaId = l.GranjaId,
                GranjaNombre = l.Farm?.Name ?? "",
                GalponId = l.GalponId,
                GalponNombre = l.Galpon?.GalponNombre ?? "",
                FechaEncaset = l.FechaEncaset
            }).ToList();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error en ObtenerLotesAsync: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    private async Task<string> DeterminarTipoLoteAsync(int loteId)
    {
        // Verificar si tiene seguimiento de levante
        var tieneLevante = await _context.SeguimientoLoteLevante
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId);

        // Verificar si tiene seguimiento de producción
        var tieneProduccion = await _context.SeguimientoProduccion
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId);

        // Verificar si tiene seguimiento de reproductora (LoteSeguimiento.LoteId es string)
        var tieneReproductora = await _context.LoteSeguimientos
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId.ToString());

        if (tieneLevante && tieneProduccion) return "Mixto";
        if (tieneLevante) return "Levante";
        if (tieneProduccion) return "Produccion";
        if (tieneReproductora) return "Reproductora";
        return "Desconocido";
    }

    private async Task<IndicadorEcuadorDto?> CalcularIndicadorPorLoteAsync(LoteInfo lote, IndicadorEcuadorRequest request)
    {
        var loteId = lote.LoteId;

        // 1. Aves encasetadas
        var loteEntity = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId);
        
        if (loteEntity == null) return null;

        var avesEncasetadas = loteEntity.AvesEncasetadas ?? 
                             (loteEntity.HembrasL ?? 0) + (loteEntity.MachosL ?? 0) + (loteEntity.Mixtas ?? 0);

        // 2. Aves sacrificadas (despachos/ventas)
        var avesSacrificadas = await CalcularAvesSacrificadasAsync(loteId);

        // 3. Mortalidad y selección
        var (mortalidad, seleccion) = await CalcularMortalidadYSeleccionAsync(loteId);
        var mortalidadTotal = mortalidad + seleccion;
        var mortalidadPorcentaje = avesEncasetadas > 0 
            ? (decimal)mortalidadTotal / avesEncasetadas * 100 
            : 0;
        var supervivenciaPorcentaje = avesEncasetadas > 0 
            ? (decimal)(avesEncasetadas - mortalidadTotal) / avesEncasetadas * 100 
            : 0;

        // 4. Consumo total alimento
        var consumoTotal = await CalcularConsumoTotalAsync(loteId);
        var consumoAveGramos = avesSacrificadas > 0 
            ? consumoTotal / avesSacrificadas * 1000 
            : 0;

        // 5. Kg Carne de Pollos (desde movimientos de despacho)
        var (kgCarne, edadPromedio) = await CalcularKgCarneYEdadAsync(loteId);
        var pesoPromedio = avesSacrificadas > 0 
            ? kgCarne / avesSacrificadas 
            : 0;

        // 6. Conversión
        var conversion = kgCarne > 0 
            ? consumoTotal / kgCarne 
            : 0;

        // 7. Conversión ajustada (variables 2.7 y 4.5 por defecto)
        var pesoAjuste = request.PesoAjusteVariable ?? PesoAjusteDefault;
        var divisorAjuste = request.DivisorAjusteVariable ?? DivisorAjusteDefault;
        var conversionAjustada = CalcularConversionAjustada(conversion, pesoPromedio, pesoAjuste, divisorAjuste);

        // 8. Metros cuadrados (del galpón)
        var metrosCuadrados = await CalcularMetrosCuadradosAsync(lote.GalponId, lote.GranjaId);
        var avesPorM2 = metrosCuadrados > 0 
            ? avesSacrificadas / metrosCuadrados 
            : 0;
        var kgPorM2 = metrosCuadrados > 0 
            ? kgCarne / metrosCuadrados 
            : 0;

        // 9. Eficiencias
        var eficienciaAmericana = conversion > 0 
            ? (pesoPromedio / conversion) * 100 
            : 0;
        var eficienciaEuropea = (conversion > 0 && edadPromedio > 0) 
            ? ((pesoPromedio * supervivenciaPorcentaje) / (edadPromedio * conversion)) * 100 
            : 0;
        var indiceProductividad = conversion > 0 
            ? (pesoPromedio / conversion) / conversion * 100 
            : 0;
        var gananciaDia = edadPromedio > 0 
            ? (pesoPromedio / edadPromedio) * 1000 
            : 0;

        // 10. Verificar si el lote está cerrado (aves = 0)
        var avesActuales = await CalcularAvesActualesAsync(loteId);
        var loteCerrado = avesActuales == 0;

        // Si SoloLotesCerrados = true y el lote no está cerrado, retornar null
        if (request.SoloLotesCerrados && !loteCerrado)
            return null;

        return new IndicadorEcuadorDto(
            lote.GranjaId,
            lote.GranjaNombre,
            loteId,
            lote.LoteNombre,
            lote.GalponId,
            lote.GalponNombre,
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
            avesPorM2,
            kgPorM2,
            eficienciaAmericana,
            eficienciaEuropea,
            indiceProductividad,
            gananciaDia,
            lote.FechaEncaset,
            await ObtenerFechaCierreLoteAsync(loteId),
            loteCerrado
        );
    }

    /// <summary>Fecha del último despacho (Venta) del lote; null si no hay ventas.</summary>
    private async Task<DateTime?> ObtenerFechaCierreLoteAsync(int loteId)
    {
        var fecha = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteId &&
                       (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho") &&
                       m.Estado != "Cancelado" &&
                       m.DeletedAt == null)
            .MaxAsync(m => (DateTime?)m.FechaMovimiento);
        return fecha;
    }

    private async Task<int> CalcularAvesSacrificadasAsync(int loteId)
    {
        // Sumar aves de movimientos tipo "Venta" o "Despacho" (saque del galpón)
        var total = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteId &&
                       (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho") &&
                       m.Estado != "Cancelado" &&
                       m.DeletedAt == null)
            .SumAsync(m => (int?)(m.CantidadHembras + m.CantidadMachos + m.CantidadMixtas));

        return total ?? 0;
    }

    private async Task<(int mortalidad, int seleccion)> CalcularMortalidadYSeleccionAsync(int loteId)
    {
        // Mortalidad y selección de levante
        var levante = await _context.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .GroupBy(s => 1)
            .Select(g => new
            {
                Mortalidad = g.Sum(s => (int?)(s.MortalidadHembras + s.MortalidadMachos)) ?? 0,
                Seleccion = g.Sum(s => (int?)(s.SelH + s.SelM)) ?? 0
            })
            .FirstOrDefaultAsync();

        // Mortalidad y selección de producción
        var produccion = await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .GroupBy(s => 1)
            .Select(g => new
            {
                Mortalidad = g.Sum(s => (int?)(s.MortalidadH + s.MortalidadM)) ?? 0,
                Seleccion = g.Sum(s => (int?)s.SelH) ?? 0
            })
            .FirstOrDefaultAsync();

        // Mortalidad y selección de reproductora (LoteSeguimiento.LoteId es string)
        var reproductora = await _context.LoteSeguimientos
            .AsNoTracking()
            .Where(s => s.LoteId == loteId.ToString())
            .GroupBy(s => 1)
            .Select(g => new
            {
                Mortalidad = g.Sum(s => (s.MortalidadH ?? 0) + (s.MortalidadM ?? 0)),
                Seleccion = g.Sum(s => (s.SelH ?? 0) + (s.SelM ?? 0))
            })
            .FirstOrDefaultAsync();

        var mortalidad = (levante?.Mortalidad ?? 0) + 
                         (produccion?.Mortalidad ?? 0) + 
                         (reproductora?.Mortalidad ?? 0);
        var seleccion = (levante?.Seleccion ?? 0) + 
                       (produccion?.Seleccion ?? 0) + 
                       (reproductora?.Seleccion ?? 0);

        return (mortalidad, seleccion);
    }

    private async Task<decimal> CalcularConsumoTotalAsync(int loteId)
    {
        // Consumo de levante
        var consumoLevante = await _context.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .SumAsync(s => (decimal?)(s.ConsumoKgHembras + (s.ConsumoKgMachos ?? 0)));

        // Consumo de producción
        var consumoProduccion = await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .SumAsync(s => (decimal?)(s.ConsKgH + s.ConsKgM));

        // Consumo de reproductora (LoteSeguimiento.LoteId es string)
        var consumoReproductora = await _context.LoteSeguimientos
            .AsNoTracking()
            .Where(s => s.LoteId == loteId.ToString())
            .SumAsync(s => (decimal?)((s.ConsumoAlimento ?? 0m) + (s.ConsumoKgMachos ?? 0m)));

        return (consumoLevante ?? 0m) + (consumoProduccion ?? 0m) + (consumoReproductora ?? 0m);
    }

    private async Task<(decimal kgCarne, decimal edadPromedio)> CalcularKgCarneYEdadAsync(int loteId)
    {
        var movimientos = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteId &&
                       (m.TipoMovimiento == "Venta" || m.TipoMovimiento == "Despacho") &&
                       m.Estado != "Cancelado" &&
                       m.DeletedAt == null)
            .ToListAsync();

        var kgCarne = movimientos
            .Where(m => m.PesoBruto.HasValue && m.PesoTara.HasValue)
            .Sum(m => (decimal)(m.PesoBruto!.Value - m.PesoTara!.Value));

        var edades = movimientos
            .Where(m => m.EdadAves.HasValue)
            .Select(m => (decimal)m.EdadAves!.Value)
            .ToList();

        var edadPromedio = edades.Any() ? edades.Average() : 0;

        return (kgCarne, edadPromedio);
    }

    private decimal CalcularConversionAjustada(decimal conversion, decimal pesoPromedio, decimal pesoAjuste, decimal divisorAjuste)
    {
        if (conversion <= 0) return 0;
        return conversion + ((pesoAjuste - pesoPromedio) / divisorAjuste);
    }

    private async Task<decimal> CalcularMetrosCuadradosAsync(string? galponId, int granjaId)
    {
        if (string.IsNullOrEmpty(galponId))
        {
            // Si no hay galpón específico, sumar área de todos los galpones de la granja
            var galpones = await _context.Galpones
                .AsNoTracking()
                .Where(g => g.GranjaId == granjaId && g.DeletedAt == null)
                .ToListAsync();

            decimal totalArea = 0;
            foreach (var galpon in galpones)
            {
                if (decimal.TryParse(galpon.Ancho, out var ancho) && 
                    decimal.TryParse(galpon.Largo, out var largo))
                {
                    totalArea += ancho * largo;
                }
            }
            return totalArea;
        }
        else
        {
            var galpon = await _context.Galpones
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.GalponId == galponId && g.GranjaId == granjaId);

            if (galpon != null && 
                decimal.TryParse(galpon.Ancho, out var ancho) && 
                decimal.TryParse(galpon.Largo, out var largo))
            {
                return ancho * largo;
            }
        }

        return 0;
    }

    private async Task<int> CalcularAvesActualesAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId);

        if (lote == null) return 0;

        var avesIniciales = lote.AvesEncasetadas ?? 
                           (lote.HembrasL ?? 0) + (lote.MachosL ?? 0) + (lote.Mixtas ?? 0);

        var (mortalidad, seleccion) = await CalcularMortalidadYSeleccionAsync(loteId);
        var avesSacrificadas = await CalcularAvesSacrificadasAsync(loteId);

        // Aves actuales = iniciales - mortalidad - selección - sacrificadas
        var avesActuales = avesIniciales - mortalidad - seleccion - avesSacrificadas;
        return Math.Max(0, avesActuales);
    }

    private class LoteInfo
    {
        public int LoteId { get; set; }
        public string LoteNombre { get; set; } = "";
        public int GranjaId { get; set; }
        public string GranjaNombre { get; set; } = "";
        public string? GalponId { get; set; }
        public string GalponNombre { get; set; } = "";
        public DateTime? FechaEncaset { get; set; }
    }
}
