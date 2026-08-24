// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoService.Semanal.cs
// Reporte tecnico SEMANAL: por sublote y consolidado, agregando los datos diarios por semana de vida.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService
{
    public async Task<ReporteTecnicoCompletoDto> GenerarReporteSemanalSubloteAsync(
        int loteId,
        int? semana = null,
        CancellationToken ct = default)
    {
        var reporteDiario = await GenerarReporteDiarioSubloteAsync(loteId, null, null, ct);
        
        var datosSemanales = semana.HasValue
            ? reporteDiario.DatosSemanales.Where(s => s.Semana == semana.Value && s.Semana <= 25).ToList()
            : reporteDiario.DatosSemanales.Where(s => s.Semana <= 25).ToList();

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = reporteDiario.InformacionLote,
            DatosDiarios = new List<ReporteTecnicoDiarioDto>(), // No incluir diarios en reporte semanal
            DatosSemanales = datosSemanales,
            EsConsolidado = false,
            SublotesIncluidos = reporteDiario.SublotesIncluidos
        };
    }

    public async Task<ReporteTecnicoCompletoDto> GenerarReporteSemanalConsolidadoAsync(
        string loteNombreBase,
        int? semana = null,
        int? loteId = null,
        CancellationToken ct = default)
    {
        List<Lote> sublotes;
        
        // Si se proporciona loteId, usar lógica de lote padre
        if (loteId.HasValue)
        {
            var loteSeleccionado = await _ctx.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && 
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (loteSeleccionado == null)
                throw new InvalidOperationException($"Lote con ID {loteId.Value} no encontrado");
            
            // Si el lote seleccionado es un lote padre, traer todos sus hijos
            if (loteSeleccionado.LotePadreId == null)
            {
                // Es un lote padre, traer todos los lotes que tienen este como padre
                sublotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == loteId.Value &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
                
                // Incluir también el lote padre
                sublotes.Insert(0, loteSeleccionado);
            }
            else
            {
                // Es un lote hijo, traer el padre y todos sus hermanos (incluyendo el seleccionado)
                var padreId = loteSeleccionado.LotePadreId.Value;
                sublotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => (l.LotePadreId == padreId || l.LoteId == padreId) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }
        else
        {
            // Lógica antigua: buscar por nombre base (compatibilidad hacia atrás)
            sublotes = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LoteNombre.StartsWith(loteNombreBase) && 
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }

        if (!sublotes.Any())
            throw new InvalidOperationException($"No se encontraron sublotes para el lote {loteNombreBase}");

        // Obtener datos semanales de cada sublote
        var datosSemanalesPorSublote = new Dictionary<string, List<ReporteTecnicoSemanalDto>>();

        foreach (var sublote in sublotes)
        {
            var subloteNombre = ExtraerSublote(sublote.LoteNombre) ?? "Sin sublote";
            
            // Para reporte de levante, siempre usar datos de levante (semanas 1-25)
            var datosDiarios = await ObtenerDatosDiariosLevanteAsync(sublote.LoteId ?? 0, sublote.FechaEncaset, null, null, ct);
            
            // Filtrar solo semanas de levante (1-25)
            datosDiarios = datosDiarios.Where(d => d.EdadSemanas <= 25).ToList();

            var avesInicialesSublote = (sublote.HembrasL ?? 0) + (sublote.MachosL ?? 0);
            var datosSemanales = ConsolidarSemanales(datosDiarios, sublote.FechaEncaset, avesInicialesSublote);
            
            // Filtrar también las semanas consolidadas (solo semanas 1-25)
            datosSemanales = datosSemanales.Where(s => s.Semana <= 25).ToList();
            
            datosSemanalesPorSublote[subloteNombre] = datosSemanales;
        }

        // Consolidar semanas completas (solo si todos los sublotes tienen la semana completa)
        var semanasConsolidadas = ConsolidarSemanasCompletas(datosSemanalesPorSublote, semana);
        
        // Filtrar solo semanas de levante (1-25)
        semanasConsolidadas = semanasConsolidadas.Where(s => s.Semana <= 25).ToList();

        var loteBase = sublotes.First();
        var infoLote = MapearInformacionLote(loteBase);
        infoLote.Sublote = null;
        infoLote.Etapa = "LEVANTE"; // Forzar etapa a LEVANTE para reporte de levante

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = infoLote,
            DatosDiarios = new List<ReporteTecnicoDiarioDto>(),
            DatosSemanales = semanasConsolidadas,
            EsConsolidado = true,
            SublotesIncluidos = datosSemanalesPorSublote.Keys.ToList()
        };
    }

    private List<ReporteTecnicoSemanalDto> ConsolidarSemanales(
        List<ReporteTecnicoDiarioDto> datosDiarios,
        DateTime? fechaEncaset,
        int avesIniciales = 0)
    {
        if (!fechaEncaset.HasValue || !datosDiarios.Any())
            return new List<ReporteTecnicoSemanalDto>();

        var semanas = datosDiarios
            .GroupBy(d => d.EdadSemanas)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var datosSemana = g.OrderBy(d => d.Fecha).ToList();
                var primeraFecha = datosSemana.First().Fecha;
                var ultimaFecha = datosSemana.Last().Fecha;

                // Verificar si la semana está completa (7 días)
                var diasEnSemana = datosSemana.Count;
                var semanaCompleta = diasEnSemana >= 7;

                // Obtener valores semanales primero
                var avesFinSemana = datosSemana.Last().NumeroAves;
                var mortalidadTotalSemana = datosSemana.Sum(d => d.MortalidadTotal);
                var seleccionVentasSemana = datosSemana.Sum(d => d.SeleccionVentasNumero);
                var descarteTotalSemana = datosSemana.Sum(d => d.DescarteNumero); // Solo descarte normal (valores positivos)
                var trasladosTotalSemana = datosSemana.Sum(d => d.TrasladosNumero); // Traslados (valores negativos en valor absoluto)
                var errorSexajeTotalSemana = datosSemana.Sum(d => d.ErrorSexajeNumero);
                
                // VALIDACIÓN Y CORRECCIÓN: Calcular avesInicioSemana usando la fórmula inversa para garantizar coherencia
                // Fórmula: avesFinSemana = avesInicioSemana - mortalidad - descarte - traslados + errorSexaje
                // Por lo tanto: avesInicioSemana = avesFinSemana + mortalidad + descarte + traslados - errorSexaje
                var avesInicioSemanaCalculado = avesFinSemana + mortalidadTotalSemana + descarteTotalSemana + trasladosTotalSemana - errorSexajeTotalSemana;
                
                // Inicializar avesInicioSemana desde el primer día
                var avesInicioSemana = datosSemana.First().NumeroAves;
                
                // CORRECCIÓN: Para semana 1, intentar usar avesIniciales si es razonable
                if (g.Key == 1 && datosSemana.Any() && avesIniciales > 0)
                {
                    var primerDia = datosSemana.First();
                    // Calcular aves al inicio de la semana 1 desde el primer día
                    var avesInicioDesdePrimerDia = primerDia.NumeroAves + primerDia.MortalidadTotal + primerDia.DescarteNumero + primerDia.TrasladosNumero - primerDia.ErrorSexajeNumero;
                    
                    // Si el cálculo desde el primer día está más cerca de avesIniciales, usarlo
                    if (Math.Abs(avesInicioDesdePrimerDia - avesIniciales) < Math.Abs(avesInicioSemanaCalculado - avesIniciales))
                    {
                        avesInicioSemana = avesInicioDesdePrimerDia;
                    }
                    else
                    {
                        // Priorizar la coherencia de la fórmula
                        avesInicioSemana = avesInicioSemanaCalculado;
                    }
                }
                else
                {
                    // Para semanas siguientes, usar el cálculo basado en la fórmula para garantizar coherencia
                    avesInicioSemana = avesInicioSemanaCalculado;
                }
                
                // Calcular porcentaje de mortalidad semanal correctamente
                // El porcentaje debe ser sobre las aves al inicio de la semana
                var mortalidadPorcentajeSemana = avesInicioSemana > 0 
                    ? (decimal)mortalidadTotalSemana / avesInicioSemana * 100 
                    : 0;

                return new ReporteTecnicoSemanalDto
                {
                    Semana = g.Key,
                    FechaInicio = primeraFecha,
                    FechaFin = ultimaFecha,
                    EdadInicioSemanas = g.Key,
                    EdadFinSemanas = g.Key,
                    AvesInicioSemana = avesInicioSemana,
                    AvesFinSemana = avesFinSemana,
                    MortalidadTotalSemana = mortalidadTotalSemana,
                    MortalidadPorcentajeSemana = mortalidadPorcentajeSemana,
                    ConsumoKilosSemana = datosSemana.Sum(d => d.ConsumoKilos),
                    ConsumoGramosPorAveSemana = datosSemana.Average(d => d.ConsumoGramosPorAve),
                    PesoPromedioSemana = datosSemana.Where(d => d.PesoActual.HasValue).Select(d => d.PesoActual!.Value).DefaultIfEmpty(0).Average(),
                    UniformidadPromedioSemana = datosSemana.Where(d => d.Uniformidad.HasValue).Select(d => d.Uniformidad!.Value).DefaultIfEmpty(0).Average(),
                    SeleccionVentasSemana = seleccionVentasSemana,
                    DescarteTotalSemana = descarteTotalSemana,
                    TrasladosTotalSemana = trasladosTotalSemana,
                    ErrorSexajeTotalSemana = errorSexajeTotalSemana,
                    IngresosAlimentoKilosSemana = datosSemana.Sum(d => d.IngresosAlimentoKilos),
                    TrasladosAlimentoKilosSemana = datosSemana.Sum(d => d.TrasladosAlimentoKilos),
                    DetalleDiario = semanaCompleta ? datosSemana : new List<ReporteTecnicoDiarioDto>()
                };
            })
            .ToList();

        return semanas;
    }

    private List<ReporteTecnicoSemanalDto> ConsolidarSemanasCompletas(
        Dictionary<string, List<ReporteTecnicoSemanalDto>> datosPorSublote,
        int? semanaFiltro = null)
    {
        // Obtener todas las semanas únicas de todos los sublotes
        var todasSemanas = datosPorSublote.Values
            .SelectMany(s => s.Select(sem => sem.Semana))
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (semanaFiltro.HasValue)
            todasSemanas = todasSemanas.Where(s => s == semanaFiltro.Value).ToList();

        var semanasConsolidadas = new List<ReporteTecnicoSemanalDto>();

        foreach (var semana in todasSemanas)
        {
            // Verificar que todos los sublotes tengan esta semana completa
            var todosTienenSemanaCompleta = datosPorSublote.Values
                .All(semanas => semanas.Any(s => s.Semana == semana && s.DetalleDiario.Count >= 7));

            if (!todosTienenSemanaCompleta)
                continue; // Saltar semanas incompletas

            // Consolidar datos de todos los sublotes para esta semana
            var datosSemanaPorSublote = datosPorSublote
                .SelectMany(kvp => kvp.Value.Where(s => s.Semana == semana))
                .ToList();

            if (!datosSemanaPorSublote.Any())
                continue;

            var primeraFecha = datosSemanaPorSublote.Min(s => s.FechaInicio);
            var ultimaFecha = datosSemanaPorSublote.Max(s => s.FechaFin);

            var avesInicioSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.AvesInicioSemana);
            var avesFinSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.AvesFinSemana);
            var mortalidadTotalSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.MortalidadTotalSemana);
            var descarteTotalSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.DescarteTotalSemana);
            var trasladosTotalSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.TrasladosTotalSemana);
            var errorSexajeTotalSemanaConsolidado = datosSemanaPorSublote.Sum(s => s.ErrorSexajeTotalSemana);
            
            // Calcular porcentaje de mortalidad semanal consolidado correctamente
            var mortalidadPorcentajeSemanaConsolidado = avesInicioSemanaConsolidado > 0 
                ? (decimal)mortalidadTotalSemanaConsolidado / avesInicioSemanaConsolidado * 100 
                : 0;

            var consolidado = new ReporteTecnicoSemanalDto
            {
                Semana = semana,
                FechaInicio = primeraFecha,
                FechaFin = ultimaFecha,
                EdadInicioSemanas = semana,
                EdadFinSemanas = semana,
                AvesInicioSemana = avesInicioSemanaConsolidado,
                AvesFinSemana = avesFinSemanaConsolidado,
                MortalidadTotalSemana = mortalidadTotalSemanaConsolidado,
                MortalidadPorcentajeSemana = mortalidadPorcentajeSemanaConsolidado,
                ConsumoKilosSemana = datosSemanaPorSublote.Sum(s => s.ConsumoKilosSemana),
                ConsumoGramosPorAveSemana = datosSemanaPorSublote.Average(s => s.ConsumoGramosPorAveSemana),
                PesoPromedioSemana = datosSemanaPorSublote.Where(s => s.PesoPromedioSemana.HasValue).Select(s => s.PesoPromedioSemana!.Value).DefaultIfEmpty(0).Average(),
                UniformidadPromedioSemana = datosSemanaPorSublote.Where(s => s.UniformidadPromedioSemana.HasValue).Select(s => s.UniformidadPromedioSemana!.Value).DefaultIfEmpty(0).Average(),
                SeleccionVentasSemana = datosSemanaPorSublote.Sum(s => s.SeleccionVentasSemana),
                DescarteTotalSemana = descarteTotalSemanaConsolidado,
                TrasladosTotalSemana = trasladosTotalSemanaConsolidado,
                ErrorSexajeTotalSemana = errorSexajeTotalSemanaConsolidado,
                IngresosAlimentoKilosSemana = datosSemanaPorSublote.Sum(s => s.IngresosAlimentoKilosSemana),
                TrasladosAlimentoKilosSemana = datosSemanaPorSublote.Sum(s => s.TrasladosAlimentoKilosSemana),
                DetalleDiario = new List<ReporteTecnicoDiarioDto>() // No incluir detalle en consolidado
            };

            semanasConsolidadas.Add(consolidado);
        }

        return semanasConsolidadas;
    }
}
