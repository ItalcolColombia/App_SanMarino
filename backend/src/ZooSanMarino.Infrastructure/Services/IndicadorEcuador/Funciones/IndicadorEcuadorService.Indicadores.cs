// Indicadores por lote de Levante/Producción (universo tabla `lotes`): obtención y filtrado de
// lotes, determinación de tipo, y cálculo por lote (aves sacrificadas, mortalidad/selección,
// consumo, kg carne, eficiencias, aves actuales y traslados netos).
// Partial de IndicadorEcuadorService.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class IndicadorEcuadorService
{
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
        var tieneLevante = await _context.SeguimientoDiario
            .AsNoTracking()
            .AnyAsync(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString());

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

        // Filtrar según TipoFiltroLotes
        switch (request.TipoFiltroLotes)
        {
            case "aves_cero":
                // Cierre físico estricto: solo lotes con saldo de aves exactamente 0
                if (!loteCerrado) return null;
                break;
            case "cerrados":
                // Cierre administrativo: saldo físico cero O producción formalmente finalizada
                var cerradoAdm = loteCerrado || loteEntity.FechaFinProduccion != null;
                if (!cerradoAdm) return null;
                break;
            // "todos": sin filtro — todos los lotes pasan
        }

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
        var levante = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString())
            .GroupBy(s => 1)
            .Select(g => new
            {
                Mortalidad = g.Sum(s => (int?)((s.MortalidadHembras ?? 0) + (s.MortalidadMachos ?? 0))) ?? 0,
                Seleccion = g.Sum(s => (int?)((s.SelH ?? 0) + (s.SelM ?? 0))) ?? 0
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
        var consumoLevante = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString())
            .SumAsync(s => (decimal?)((s.ConsumoKgHembras ?? 0m) + (s.ConsumoKgMachos ?? 0m)));

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
        var trasladoNeto = await CalcularTrasladosNetosAsync(loteId);

        // Aves actuales = iniciales - mortalidad - selección genuina - sacrificadas - traslado neto.
        // Convergencia Feature-13: el traslado sale de las columnas dedicadas
        // (traslado_salida/ingreso), NO de la selección. Las ventas/despachos siguen
        // restando vía 'sacrificadas' (movimiento_aves), una sola vez.
        var avesActuales = avesIniciales - mortalidad - seleccion - avesSacrificadas - trasladoNeto;
        return Math.Max(0, avesActuales);
    }

    /// <summary>
    /// Traslado NETO de levante (salida - ingreso) desde las columnas dedicadas de la
    /// tabla canónica (Feature-13). Positivo = más salidas que ingresos (resta del saldo).
    /// </summary>
    private async Task<int> CalcularTrasladosNetosAsync(int loteId)
    {
        var neto = await _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString())
            .GroupBy(s => 1)
            .Select(g => new
            {
                Salida  = g.Sum(s => (int?)(s.TrasladoSalidaHembras + s.TrasladoSalidaMachos)) ?? 0,
                Ingreso = g.Sum(s => (int?)(s.TrasladoIngresoHembras + s.TrasladoIngresoMachos)) ?? 0
            })
            .FirstOrDefaultAsync();

        return (neto?.Salida ?? 0) - (neto?.Ingreso ?? 0);
    }
}
