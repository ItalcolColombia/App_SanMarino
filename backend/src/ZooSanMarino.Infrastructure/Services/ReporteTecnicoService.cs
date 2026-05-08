// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ReporteTecnicoService : IReporteTecnicoService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public ReporteTecnicoService(
        ZooSanMarinoContext ctx, 
        ICurrentUser currentUser,
        IGuiaGeneticaService guiaGeneticaService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _guiaGeneticaService = guiaGeneticaService;
    }

    public async Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioSubloteAsync(
        int loteId, 
        DateTime? fechaInicio = null, 
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lote == null)
            throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");

        var infoLote = MapearInformacionLote(lote);
        var sublote = ExtraerSublote(lote.LoteNombre);
        infoLote.Sublote = sublote;
        infoLote.Etapa = "LEVANTE"; // Forzar etapa a LEVANTE para reporte de levante

        // Para reporte de levante, siempre usar datos de levante y filtrar por semana (1-25)
        // Esto permite ver datos históricos de levante incluso si el lote está en producción
        var datosDiarios = await ObtenerDatosDiariosLevanteAsync(loteId, lote.FechaEncaset, fechaInicio, fechaFin, ct);
        
        // Filtrar solo semanas de levante (1-25)
        datosDiarios = datosDiarios.Where(d => d.EdadSemanas <= 25).ToList();

        var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
        var datosSemanales = ConsolidarSemanales(datosDiarios, lote.FechaEncaset, avesIniciales);
        
        // Filtrar también las semanas consolidadas (solo semanas 1-25)
        datosSemanales = datosSemanales.Where(s => s.Semana <= 25).ToList();

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = infoLote,
            DatosDiarios = datosDiarios,
            DatosSemanales = datosSemanales,
            EsConsolidado = false,
            SublotesIncluidos = new List<string> { sublote ?? "Sin sublote" }
        };
    }

    public async Task<ReporteTecnicoCompletoDto> GenerarReporteDiarioConsolidadoAsync(
        string loteNombreBase,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
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

        var todosDatosDiarios = new List<ReporteTecnicoDiarioDto>();
        var sublotesIncluidos = new List<string>();

        foreach (var sublote in sublotes)
        {
            var subloteNombre = ExtraerSublote(sublote.LoteNombre) ?? "Sin sublote";
            sublotesIncluidos.Add(subloteNombre);

            // Para reporte de levante, siempre usar datos de levante (semanas 1-25)
            var datosSublote = await ObtenerDatosDiariosLevanteAsync(sublote.LoteId ?? 0, sublote.FechaEncaset, fechaInicio, fechaFin, ct);
            
            // Filtrar solo semanas de levante (1-25)
            datosSublote = datosSublote.Where(d => d.EdadSemanas <= 25).ToList();

            todosDatosDiarios.AddRange(datosSublote);
        }

        // Consolidar por fecha (sumar datos de todos los sublotes para la misma fecha)
        var datosConsolidados = ConsolidarDatosDiarios(todosDatosDiarios);
        
        // Filtrar solo semanas de levante (1-25)
        datosConsolidados = datosConsolidados.Where(d => d.EdadSemanas <= 25).ToList();

        // Usar información del primer sublote como base
        var loteBase = sublotes.First();
        var infoLote = MapearInformacionLote(loteBase);
        infoLote.Sublote = null; // Consolidado no tiene sublote específico
        infoLote.Etapa = "LEVANTE"; // Forzar etapa a LEVANTE para reporte de levante

        var avesInicialesConsolidado = sublotes.Sum(s => (s.HembrasL ?? 0) + (s.MachosL ?? 0));
        var datosSemanales = ConsolidarSemanales(datosConsolidados, loteBase.FechaEncaset, avesInicialesConsolidado);
        
        // Filtrar también las semanas consolidadas (solo semanas 1-25)
        datosSemanales = datosSemanales.Where(s => s.Semana <= 25).ToList();

        return new ReporteTecnicoCompletoDto
        {
            InformacionLote = infoLote,
            DatosDiarios = datosConsolidados.OrderBy(d => d.Fecha).ToList(),
            DatosSemanales = datosSemanales,
            EsConsolidado = true,
            SublotesIncluidos = sublotesIncluidos.Distinct().ToList()
        };
    }

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

    public async Task<ReporteTecnicoCompletoDto> GenerarReporteAsync(
        GenerarReporteTecnicoRequestDto request,
        CancellationToken ct = default)
    {
        if (request.ConsolidarSublotes)
        {
            // Reporte consolidado - usar loteId si está disponible, sino usar nombre
            if (request.IncluirSemanales)
            {
                return await GenerarReporteSemanalConsolidadoAsync(
                    request.LoteNombre ?? string.Empty, 
                    null, 
                    request.LoteId, 
                    ct);
            }
            else
            {
                return await GenerarReporteDiarioConsolidadoAsync(
                    request.LoteNombre ?? string.Empty, 
                    request.FechaInicio, 
                    request.FechaFin, 
                    request.LoteId, 
                    ct);
            }
        }
        else if (request.LoteId.HasValue)
        {
            // Reporte de sublote específico
            if (request.IncluirSemanales)
            {
                return await GenerarReporteSemanalSubloteAsync(request.LoteId.Value, null, ct);
            }
            else
            {
                return await GenerarReporteDiarioSubloteAsync(request.LoteId.Value, request.FechaInicio, request.FechaFin, ct);
            }
        }
        else
        {
            throw new ArgumentException("Debe proporcionar LoteId o LoteNombre para generar el reporte");
        }
    }

    public async Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, int? loteId = null, CancellationToken ct = default)
    {
        List<Lote> lotes;
        
        // Si se proporciona loteId, usar lógica de lote padre
        if (loteId.HasValue)
        {
            var loteSeleccionado = await _ctx.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteId.Value && 
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (loteSeleccionado == null)
                return new List<string>();
            
            // Si el lote seleccionado es un lote padre, traer todos sus hijos
            if (loteSeleccionado.LotePadreId == null)
            {
                // Es un lote padre, traer todos los lotes que tienen este como padre
                lotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == loteId.Value &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
                
                // Incluir también el lote padre
                lotes.Insert(0, loteSeleccionado);
            }
            else
            {
                // Es un lote hijo, traer el padre y todos sus hermanos (incluyendo el seleccionado)
                var padreId = loteSeleccionado.LotePadreId.Value;
                lotes = await _ctx.Lotes
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
            lotes = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LoteNombre.StartsWith(loteNombreBase) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }

        // Extraer los nombres de sublotes
        var sublotes = lotes
            .Select(l => ExtraerSublote(l.LoteNombre) ?? "Sin sublote")
            .Where(s => !string.IsNullOrEmpty(s))
            .Distinct()
            .ToList();

        return sublotes;
    }

    /// <summary>
    /// Obtiene todos los sublotes de un lote base levante (busca padre e hijos).
    /// Soporta lógica de lote padre: si es padre trae hijos, si es hijo trae padre + hermanos.
    /// </summary>
    private async Task<List<LotePosturaLevante>> ObtenerSublotesLevantePorLoteBaseAsync(
        int lotePosturaLevanteId,
        CancellationToken ct)
    {
        var loteSeleccionado = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId &&
                                     l.CompanyId == _currentUser.CompanyId &&
                                     l.DeletedAt == null, ct);

        if (loteSeleccionado == null)
            return new List<LotePosturaLevante>();

        List<LotePosturaLevante> sublotes;

        // Si el lote seleccionado es un lote padre (LotePosturaLevantePadreId == null)
        if (loteSeleccionado.LotePosturaLevantePadreId == null)
        {
            // Traer todos los lotes que tienen este como padre
            sublotes = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(l => l.LotePosturaLevantePadreId == lotePosturaLevanteId &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            // Incluir el lote padre siempre
            sublotes.Insert(0, loteSeleccionado);

            // Fallback: si no hay hijos vinculados por FK, buscar por prefijo de nombre
            if (sublotes.Count == 1)
            {
                var nombreBase = ExtraerNombreBase(loteSeleccionado.LoteNombre);
                var porNombre = await _ctx.LotePosturaLevante
                    .AsNoTracking()
                    .Where(l => l.LotePosturaLevanteId != lotePosturaLevanteId &&
                               l.LoteNombre.StartsWith(nombreBase) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);

                if (porNombre.Count > 0)
                    sublotes.AddRange(porNombre);
            }
        }
        else
        {
            // Es un lote hijo: traer el padre y todos sus hermanos
            var padreId = loteSeleccionado.LotePosturaLevantePadreId.Value;
            sublotes = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Where(l => (l.LotePosturaLevantePadreId == padreId || l.LotePosturaLevanteId == padreId) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);

            // Fallback por nombre si no se encontraron hermanos vía FK
            if (sublotes.Count <= 1)
            {
                var nombreBase = ExtraerNombreBase(loteSeleccionado.LoteNombre);
                sublotes = await _ctx.LotePosturaLevante
                    .AsNoTracking()
                    .Where(l => l.LoteNombre.StartsWith(nombreBase) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }

        return sublotes;
    }

    private static string ExtraerNombreBase(string loteNombre)
    {
        var partes = loteNombre.Trim().Split(' ');
        if (partes.Length > 1 && partes[^1].Length <= 2)
            return string.Join(' ', partes[..^1]);
        return loteNombre.Trim();
    }

    /// <summary>
    /// Consolida datos diarios de machos por fecha (suma valores de múltiples sublotes)
    /// </summary>
    private List<ReporteTecnicoDiarioMachosDto> ConsolidarDatosDiariosMachos(
        List<ReporteTecnicoDiarioMachosDto> datos)
    {
        if (!datos.Any())
            return datos;

        var datosConsolidados = datos
            .GroupBy(d => d.Fecha.Date)
            .Select(g => new ReporteTecnicoDiarioMachosDto
            {
                Fecha = g.Key,
                EdadDias = g.First().EdadDias,
                EdadSemanas = g.First().EdadSemanas,
                SaldoMachos = g.Sum(d => d.SaldoMachos),
                MortalidadMachos = g.Sum(d => d.MortalidadMachos),
                MortalidadMachosAcumulada = g.Sum(d => d.MortalidadMachosAcumulada),
                MortalidadMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.MortalidadMachosPorcentajeDiario) / g.Count() : 0,
                MortalidadMachosPorcentajeAcumulado = g.First().MortalidadMachosPorcentajeAcumulado,
                SeleccionMachos = g.Sum(d => d.SeleccionMachos),
                SeleccionMachosAcumulada = g.Sum(d => d.SeleccionMachosAcumulada),
                SeleccionMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.SeleccionMachosPorcentajeDiario) / g.Count() : 0,
                SeleccionMachosPorcentajeAcumulado = g.First().SeleccionMachosPorcentajeAcumulado,
                TrasladosMachos = g.Sum(d => d.TrasladosMachos),
                TrasladosMachosAcumulados = g.Sum(d => d.TrasladosMachosAcumulados),
                ErrorSexajeMachos = g.Sum(d => d.ErrorSexajeMachos),
                ErrorSexajeMachosAcumulado = g.Sum(d => d.ErrorSexajeMachosAcumulado),
                ErrorSexajeMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.ErrorSexajeMachosPorcentajeDiario) / g.Count() : 0,
                ErrorSexajeMachosPorcentajeAcumulado = g.First().ErrorSexajeMachosPorcentajeAcumulado,
                DescarteMachos = g.Sum(d => d.DescarteMachos),
                DescarteMachosAcumulado = g.Sum(d => d.DescarteMachosAcumulado),
                DescarteMachosPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.DescarteMachosPorcentajeDiario) / g.Count() : 0,
                DescarteMachosPorcentajeAcumulado = g.First().DescarteMachosPorcentajeAcumulado,
                ConsumoKgMachos = g.Sum(d => d.ConsumoKgMachos),
                ConsumoKgMachosAcumulado = g.Sum(d => d.ConsumoKgMachosAcumulado),
                ConsumoGramosPorAveMachos = g.Count() > 0 ? g.Sum(d => d.ConsumoGramosPorAveMachos) / g.Count() : 0,
                PesoPromedioMachos = g.Average(d => d.PesoPromedioMachos ?? 0),
                UniformidadMachos = g.Average(d => d.UniformidadMachos ?? 0),
                CoeficienteVariacionMachos = g.Average(d => d.CoeficienteVariacionMachos ?? 0),
                GananciaPesoMachos = g.Average(d => d.GananciaPesoMachos ?? 0),
                KcalAlMachos = g.Average(d => d.KcalAlMachos ?? 0),
                ProtAlMachos = g.Average(d => d.ProtAlMachos ?? 0),
                KcalAveMachos = g.Average(d => d.KcalAveMachos ?? 0),
                ProtAveMachos = g.Average(d => d.ProtAveMachos ?? 0),
                IngresosAlimentoKilos = g.Sum(d => d.IngresosAlimentoKilos),
                TrasladosAlimentoKilos = g.Sum(d => d.TrasladosAlimentoKilos),
                Observaciones = g.First().Observaciones
            })
            .ToList();

        return datosConsolidados;
    }

    /// <summary>
    /// Consolida datos diarios de hembras por fecha (suma valores de múltiples sublotes)
    /// </summary>
    private List<ReporteTecnicoDiarioHembrasDto> ConsolidarDatosDiariosHembras(
        List<ReporteTecnicoDiarioHembrasDto> datos)
    {
        if (!datos.Any())
            return datos;

        var datosConsolidados = datos
            .GroupBy(d => d.Fecha.Date)
            .Select(g => new ReporteTecnicoDiarioHembrasDto
            {
                Fecha = g.Key,
                EdadDias = g.First().EdadDias,
                EdadSemanas = g.First().EdadSemanas,
                SaldoHembras = g.Sum(d => d.SaldoHembras),
                MortalidadHembras = g.Sum(d => d.MortalidadHembras),
                MortalidadHembrasAcumulada = g.Sum(d => d.MortalidadHembrasAcumulada),
                MortalidadHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.MortalidadHembrasPorcentajeDiario) / g.Count() : 0,
                MortalidadHembrasPorcentajeAcumulado = g.First().MortalidadHembrasPorcentajeAcumulado,
                SeleccionHembras = g.Sum(d => d.SeleccionHembras),
                SeleccionHembrasAcumulada = g.Sum(d => d.SeleccionHembrasAcumulada),
                SeleccionHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.SeleccionHembrasPorcentajeDiario) / g.Count() : 0,
                SeleccionHembrasPorcentajeAcumulado = g.First().SeleccionHembrasPorcentajeAcumulado,
                TrasladosHembras = g.Sum(d => d.TrasladosHembras),
                TrasladosHembrasAcumulados = g.Sum(d => d.TrasladosHembrasAcumulados),
                ErrorSexajeHembras = g.Sum(d => d.ErrorSexajeHembras),
                ErrorSexajeHembrasAcumulado = g.Sum(d => d.ErrorSexajeHembrasAcumulado),
                ErrorSexajeHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.ErrorSexajeHembrasPorcentajeDiario) / g.Count() : 0,
                ErrorSexajeHembrasPorcentajeAcumulado = g.First().ErrorSexajeHembrasPorcentajeAcumulado,
                DescarteHembras = g.Sum(d => d.DescarteHembras),
                DescarteHembrasAcumulado = g.Sum(d => d.DescarteHembrasAcumulado),
                DescarteHembrasPorcentajeDiario = g.Count() > 0 ? g.Sum(d => d.DescarteHembrasPorcentajeDiario) / g.Count() : 0,
                DescarteHembrasPorcentajeAcumulado = g.First().DescarteHembrasPorcentajeAcumulado,
                ConsumoKgHembras = g.Sum(d => d.ConsumoKgHembras),
                ConsumoKgHembrasAcumulado = g.Sum(d => d.ConsumoKgHembrasAcumulado),
                ConsumoGramosPorAveHembras = g.Count() > 0 ? g.Sum(d => d.ConsumoGramosPorAveHembras) / g.Count() : 0,
                PesoPromedioHembras = g.Average(d => d.PesoPromedioHembras ?? 0),
                UniformidadHembras = g.Average(d => d.UniformidadHembras ?? 0),
                CoeficienteVariacionHembras = g.Average(d => d.CoeficienteVariacionHembras ?? 0),
                GananciaPesoHembras = g.Average(d => d.GananciaPesoHembras ?? 0),
                KcalAlHembras = g.Average(d => d.KcalAlHembras ?? 0),
                ProtAlHembras = g.Average(d => d.ProtAlHembras ?? 0),
                KcalAveHembras = g.Average(d => d.KcalAveHembras ?? 0),
                ProtAveHembras = g.Average(d => d.ProtAveHembras ?? 0),
                IngresosAlimentoKilos = g.Sum(d => d.IngresosAlimentoKilos),
                TrasladosAlimentoKilos = g.Sum(d => d.TrasladosAlimentoKilos),
                Observaciones = g.First().Observaciones
            })
            .ToList();

        return datosConsolidados;
    }

    #region Métodos Privados

    /// <summary>Registro de seguimiento levante leído desde la tabla unificada seguimiento_diario (TipoSeguimiento = levante).</summary>
    private sealed class SegLevanteParaReporte
    {
        public int Id { get; set; }
        public int LoteId { get; set; }
        public DateTime FechaRegistro { get; set; }
        public int MortalidadHembras { get; set; }
        public int MortalidadMachos { get; set; }
        public int SelH { get; set; }
        public int SelM { get; set; }
        public int ErrorSexajeHembras { get; set; }
        public int ErrorSexajeMachos { get; set; }
        public double ConsumoKgHembras { get; set; }
        public double? ConsumoKgMachos { get; set; }
        public double? PesoPromH { get; set; }
        public double? PesoPromM { get; set; }
        public double? UniformidadH { get; set; }
        public double? UniformidadM { get; set; }
        public double? CvH { get; set; }
        public double? CvM { get; set; }
        public double? KcalAlH { get; set; }
        public double? ProtAlH { get; set; }
        public double? KcalAveH { get; set; }
        public double? ProtAveH { get; set; }
        public string? Observaciones { get; set; }
    }

    private const string TipoLevante = "levante";

    /// <summary>Obtiene todos los seguimientos de levante del lote desde la tabla unificada seguimiento_diario (fase levante), por lote_id (legacy).</summary>
    private async Task<List<SegLevanteParaReporte>> ObtenerSeguimientosLevanteUnificadoAsync(int loteId, CancellationToken ct)
    {
        var loteIdStr = loteId.ToString();
        var list = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoLevante && s.LoteId == loteIdStr)
            .OrderBy(s => s.Fecha)
            .Select(s => new SegLevanteParaReporte
            {
                Id = (int)s.Id,
                LoteId = loteId,
                FechaRegistro = s.Fecha,
                MortalidadHembras = s.MortalidadHembras ?? 0,
                MortalidadMachos = s.MortalidadMachos ?? 0,
                SelH = s.SelH ?? 0,
                SelM = s.SelM ?? 0,
                ErrorSexajeHembras = s.ErrorSexajeHembras ?? 0,
                ErrorSexajeMachos = s.ErrorSexajeMachos ?? 0,
                ConsumoKgHembras = (double)(s.ConsumoKgHembras ?? 0),
                ConsumoKgMachos = s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : null,
                PesoPromH = s.PesoPromHembras,
                PesoPromM = s.PesoPromMachos,
                UniformidadH = s.UniformidadHembras,
                UniformidadM = s.UniformidadMachos,
                CvH = s.CvHembras,
                CvM = s.CvMachos,
                KcalAlH = s.KcalAlH,
                ProtAlH = s.ProtAlH,
                KcalAveH = s.KcalAveH,
                ProtAveH = s.ProtAveH,
                Observaciones = s.Observaciones
            })
            .ToListAsync(ct);
        return list;
    }

    /// <summary>Obtiene seguimientos de levante por lote_postura_levante_id (seguimiento_diario.lote_postura_levante_id).</summary>
    private async Task<List<SegLevanteParaReporte>> ObtenerSeguimientosLevantePorLPLAsync(int lotePosturaLevanteId, CancellationToken ct)
    {
        var list = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoLevante && s.LotePosturaLevanteId == lotePosturaLevanteId)
            .OrderBy(s => s.Fecha)
            .Select(s => new SegLevanteParaReporte
            {
                Id = (int)s.Id,
                LoteId = lotePosturaLevanteId,
                FechaRegistro = s.Fecha,
                MortalidadHembras = s.MortalidadHembras ?? 0,
                MortalidadMachos = s.MortalidadMachos ?? 0,
                SelH = s.SelH ?? 0,
                SelM = s.SelM ?? 0,
                ErrorSexajeHembras = s.ErrorSexajeHembras ?? 0,
                ErrorSexajeMachos = s.ErrorSexajeMachos ?? 0,
                ConsumoKgHembras = (double)(s.ConsumoKgHembras ?? 0),
                ConsumoKgMachos = s.ConsumoKgMachos.HasValue ? (double)s.ConsumoKgMachos.Value : null,
                PesoPromH = s.PesoPromHembras,
                PesoPromM = s.PesoPromMachos,
                UniformidadH = s.UniformidadHembras,
                UniformidadM = s.UniformidadMachos,
                CvH = s.CvHembras,
                CvM = s.CvMachos,
                KcalAlH = s.KcalAlH,
                ProtAlH = s.ProtAlH,
                KcalAveH = s.KcalAveH,
                ProtAveH = s.ProtAveH,
                Observaciones = s.Observaciones
            })
            .ToListAsync(ct);
        return list;
    }

    private async Task<bool> EsLoteEnLevanteAsync(int loteId, CancellationToken ct)
    {
        // Obtener información del lote para calcular la edad
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null || !lote.FechaEncaset.HasValue)
        {
            // Si no hay fecha de encaset, verificar por registros en tabla unificada (fase levante)
            var tieneRegistros = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .AnyAsync(s => s.TipoSeguimiento == TipoLevante && s.LoteId == loteId.ToString(), ct);
            return tieneRegistros;
        }

        // Calcular edad en días
        var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, DateTime.Now);
        
        // Levante es hasta 25 semanas (175 días)
        // Producción es desde la semana 26 (176 días en adelante)
        if (edadDias < 175)
        {
            // Está en levante por edad
            return true;
        }
        
        // Está en producción por edad, pero verificar si tiene registros en producción
        var tieneProduccion = await _ctx.SeguimientoProduccion
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId, ct);
        
        // Si tiene registros en producción, definitivamente está en producción
        if (tieneProduccion)
            return false;
        
        // Si tiene más de 175 días, está en producción aunque tenga registros históricos en levante
        return false; // Está en producción por edad
    }

    private async Task<List<ReporteTecnicoDiarioDto>> ObtenerDatosDiariosLevanteAsync(
        int loteId,
        DateTime? fechaEncaset,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        // IMPORTANTE: Para calcular correctamente aves actuales y acumulados,
        // necesitamos TODOS los registros desde el inicio (tabla unificada seguimiento_diario, fase levante)
        var todosSeguimientos = await ObtenerSeguimientosLevanteUnificadoAsync(loteId, ct);

        // Filtrar por edad/semana: solo semanas 1-25 (levante)
        // Calcular edad para cada registro y filtrar
        if (fechaEncaset.HasValue)
        {
            todosSeguimientos = todosSeguimientos.Where(seg =>
            {
                var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
                var edadSemanas = CalcularEdadSemanas(edadDias);
                return edadSemanas <= 25; // Solo levante (semanas 1-25)
            }).ToList();
        }

        // Aplicar filtros de fecha solo para los registros que se mostrarán
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);

        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);

        var seguimientos = queryFiltrado.ToList();

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var datosDiarios = new List<ReporteTecnicoDiarioDto>();
        var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
        
        // Calcular valores acumulados desde el INICIO del lote (todos los registros)
        var mortalidadAcumuladaTotal = todosSeguimientos.Sum(s => s.MortalidadHembras + s.MortalidadMachos);
        var consumoAcumuladoTotal = todosSeguimientos.Sum(s => (decimal)s.ConsumoKgHembras + (decimal)(s.ConsumoKgMachos ?? 0));
        var errorSexajeAcumuladoTotal = todosSeguimientos.Sum(s => s.ErrorSexajeHembras + s.ErrorSexajeMachos);
        
        // Calcular descarte acumulado (incluyendo traslados)
        var descarteAcumuladoTotal = 0;
        foreach (var seg in todosSeguimientos)
        {
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            descarteAcumuladoTotal += (int)(seleccionNormal + trasladosAbsoluto);
        }
        
        // Calcular aves actuales desde el inicio
        var avesActualesBase = avesIniciales;
        foreach (var seg in todosSeguimientos)
        {
            var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
            avesActualesBase -= mortalidadTotal;
            
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            
            avesActualesBase -= seleccionNormal;
            avesActualesBase -= trasladosAbsoluto;
        }
        
        // Variables para acumular solo hasta la fecha actual del registro que se está procesando
        decimal? pesoAnterior = null;

        // Procesar todos los registros desde el inicio para calcular acumulados correctamente
        // pero solo mostrar los que están en el rango de fechas
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);

            // Calcular acumulados hasta esta fecha (incluyendo todos los registros anteriores)
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();

            var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadHembras + s.MortalidadMachos);

            var errorSexaje = seg.ErrorSexajeHembras + seg.ErrorSexajeMachos;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeHembras + s.ErrorSexajeMachos);

            // Descarte incluye selecciones (SelH, SelM) que pueden ser negativas si son descuentos por traslado
            // Separar selección normal de traslados para calcular correctamente
            var seleccionH = seg.SelH;
            var seleccionM = seg.SelM;
            
            // Selección normal (valores positivos): aves retiradas por selección/descarte
            var seleccionNormal = Math.Max(0, seleccionH) + Math.Max(0, seleccionM);
            
            // Traslados (valores negativos): aves trasladadas a otro lote/granja
            // Los valores negativos representan aves que salieron, así que debemos restar el valor absoluto
            var traslados = Math.Min(0, seleccionH) + Math.Min(0, seleccionM);
            var trasladosAbsoluto = Math.Abs(traslados);
            
            // Descarte normal (valores positivos): selección/descarte normal
            var descarteNormal = seleccionNormal;
            
            // Traslados (valores negativos en valor absoluto)
            var trasladosNumero = (int)trasladosAbsoluto;
            
            // Total descarte = selección normal + traslados (en valor absoluto para acumulación)
            // Este campo se mantiene para compatibilidad, pero ahora tenemos campos separados
            var descarte = seleccionH + seleccionM;
            
            // Calcular descarte acumulado hasta esta fecha (solo valores positivos)
            var descarteAcumulado = 0;
            var trasladosAcumulado = 0;
            foreach (var reg in registrosHastaFecha)
            {
                var selH = reg.SelH;
                var selM = reg.SelM;
                var selNormal = Math.Max(0, selH) + Math.Max(0, selM);
                var tras = Math.Min(0, selH) + Math.Min(0, selM);
                var trasAbs = Math.Abs(tras);
                descarteAcumulado += (int)selNormal;
                trasladosAcumulado += (int)trasAbs;
            }
            
            // Calcular aves actuales hasta esta fecha
            // IMPORTANTE: Para calcular el porcentaje de mortalidad diario correctamente,
            // necesitamos las aves ANTES de aplicar la mortalidad del día actual
            var avesActuales = avesIniciales;
            var avesAntesMortalidad = avesIniciales; // Aves antes de aplicar la mortalidad del día actual
            
            foreach (var reg in registrosHastaFecha)
            {
                var mortTotal = reg.MortalidadHembras + reg.MortalidadMachos;
                
                // Si este es el registro actual, guardar aves antes de aplicar mortalidad
                if (reg.Id == seg.Id)
                {
                    avesAntesMortalidad = avesActuales;
                }
                
                avesActuales -= mortTotal;
                
                var selH = reg.SelH;
                var selM = reg.SelM;
                var selNormal = Math.Max(0, selH) + Math.Max(0, selM);
                var tras = Math.Min(0, selH) + Math.Min(0, selM);
                var trasAbs = Math.Abs(tras);
                
                avesActuales -= selNormal;
                avesActuales -= trasAbs;
            }

            var consumoKilos = (decimal)seg.ConsumoKgHembras + (decimal)(seg.ConsumoKgMachos ?? 0);
            var consumoAcumulado = registrosHastaFecha.Sum(s => (decimal)s.ConsumoKgHembras + (decimal)(s.ConsumoKgMachos ?? 0));

            var consumoGramosPorAve = avesActuales > 0 ? (consumoKilos * 1000) / avesActuales : 0;

            var pesoActual = (decimal?)(seg.PesoPromH ?? seg.PesoPromM);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;

            var dto = new ReporteTecnicoDiarioDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                NumeroAves = avesActuales,
                MortalidadTotal = mortalidadTotal,
                // CORRECCIÓN: El porcentaje de mortalidad diario debe calcularse sobre las aves ANTES de la mortalidad del día
                MortalidadPorcentajeDiario = avesAntesMortalidad > 0 ? (decimal)mortalidadTotal / avesAntesMortalidad * 100 : 0,
                MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (decimal)mortalidadAcumulada / avesIniciales * 100 : 0,
                ErrorSexajeNumero = errorSexaje,
                ErrorSexajePorcentaje = avesActuales > 0 ? (decimal)errorSexaje / avesActuales * 100 : 0,
                ErrorSexajePorcentajeAcumulado = avesIniciales > 0 ? (decimal)errorSexajeAcumulado / avesIniciales * 100 : 0,
                DescarteNumero = descarteNormal, // Solo descarte normal (valores positivos)
                DescartePorcentajeDiario = avesActuales > 0 ? (decimal)descarteNormal / avesActuales * 100 : 0,
                DescartePorcentajeAcumulado = avesIniciales > 0 ? (decimal)descarteAcumulado / avesIniciales * 100 : 0,
                TrasladosNumero = trasladosNumero, // Traslados (valores negativos en valor absoluto)
                ConsumoBultos = CalcularBultos(consumoKilos), // Asumiendo 40kg por bulto estándar
                ConsumoKilos = consumoKilos,
                ConsumoKilosAcumulado = consumoAcumulado,
                ConsumoGramosPorAve = consumoGramosPorAve,
                IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct),
                TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(lote.GranjaId, seg.FechaRegistro, ct),
                PesoActual = pesoActual,
                Uniformidad = (decimal?)(seg.UniformidadH ?? seg.UniformidadM),
                GananciaPeso = gananciaPeso,
                CoeficienteVariacion = (decimal?)(seg.CvH ?? seg.CvM),
                SeleccionVentasNumero = descarte,
                SeleccionVentasPorcentaje = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0
            };

            // Actualizar peso anterior para el siguiente cálculo
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;

            datosDiarios.Add(dto);
        }

        return datosDiarios;
    }

    private async Task<List<ReporteTecnicoDiarioDto>> ObtenerDatosDiariosProduccionAsync(
        string loteId,
        DateTime? fechaEncaset,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var loteIdInt = int.TryParse(loteId, out var id) ? id : 0;
        var lote = await _ctx.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.LoteId == loteIdInt, ct);
        var loteProd = lote != null && lote.Fase != "Produccion"
            ? await _ctx.Lotes.AsNoTracking().FirstOrDefaultAsync(l => l.LotePadreId == loteIdInt && l.Fase == "Produccion" && l.DeletedAt == null, ct)
            : lote;
        var loteIdSeguimiento = (loteProd ?? lote)?.LoteId ?? loteIdInt;
        var query = _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdSeguimiento);

        if (fechaInicio.HasValue)
            query = query.Where(s => s.Fecha >= fechaInicio.Value);

        if (fechaFin.HasValue)
            query = query.Where(s => s.Fecha <= fechaFin.Value);

        var seguimientos = await query
            .OrderBy(s => s.Fecha)
            .ToListAsync(ct);

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var avesIniciales = loteProd != null
            ? (loteProd.HembrasInicialesProd ?? 0) + (loteProd.MachosInicialesProd ?? 0)
            : (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);

        var datosDiarios = new List<ReporteTecnicoDiarioDto>();
        var avesActuales = avesIniciales;
        var mortalidadAcumulada = 0;
        var consumoAcumulado = 0m;
        var descarteAcumulado = 0;
        decimal? pesoAnterior = null;

        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.Fecha);
            var edadSemanas = CalcularEdadSemanas(edadDias);

            var mortalidadTotal = seg.MortalidadH + seg.MortalidadM;
            mortalidadAcumulada += mortalidadTotal;
            avesActuales -= mortalidadTotal;

            var descarte = seg.SelH;
            descarteAcumulado += descarte;
            avesActuales -= descarte;

            var consumoKilos = seg.ConsKgH + seg.ConsKgM;
            consumoAcumulado += consumoKilos;

            var consumoGramosPorAve = avesActuales > 0 ? (consumoKilos * 1000) / avesActuales : 0;

            var pesoActual = seg.PesoH;
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;

            var dto = new ReporteTecnicoDiarioDto
            {
                Fecha = seg.Fecha,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                NumeroAves = avesActuales,
                MortalidadTotal = mortalidadTotal,
                MortalidadPorcentajeDiario = avesActuales > 0 ? (decimal)mortalidadTotal / avesActuales * 100 : 0,
                MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (decimal)mortalidadAcumulada / avesIniciales * 100 : 0,
                ErrorSexajeNumero = 0, // No aplica en producción
                ErrorSexajePorcentaje = 0,
                ErrorSexajePorcentajeAcumulado = 0,
                DescarteNumero = descarte,
                DescartePorcentajeDiario = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0,
                DescartePorcentajeAcumulado = avesIniciales > 0 ? (decimal)descarteAcumulado / avesIniciales * 100 : 0,
                ConsumoBultos = CalcularBultos(consumoKilos), // Asumiendo 40kg por bulto estándar
                ConsumoKilos = consumoKilos,
                ConsumoKilosAcumulado = consumoAcumulado,
                ConsumoGramosPorAve = consumoGramosPorAve,
                IngresosAlimentoKilos = await ObtenerIngresosAlimentoAsync(lote.GranjaId, seg.Fecha, ct),
                TrasladosAlimentoKilos = await ObtenerTrasladosAlimentoAsync(lote.GranjaId, seg.Fecha, ct),
                PesoActual = pesoActual,
                Uniformidad = seg.Uniformidad,
                GananciaPeso = gananciaPeso,
                CoeficienteVariacion = seg.CoeficienteVariacion,
                SeleccionVentasNumero = descarte,
                SeleccionVentasPorcentaje = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0
            };

            // Actualizar peso anterior para el siguiente cálculo
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;

            datosDiarios.Add(dto);
        }

        return datosDiarios;
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

    private List<ReporteTecnicoDiarioDto> ConsolidarDatosDiarios(List<ReporteTecnicoDiarioDto> todosDatos)
    {
        return todosDatos
            .GroupBy(d => d.Fecha.Date)
            .Select(g =>
            {
                var datosFecha = g.ToList();
                var primero = datosFecha.First();

                return new ReporteTecnicoDiarioDto
                {
                    Fecha = primero.Fecha,
                    EdadDias = (int)datosFecha.Average(d => d.EdadDias),
                    EdadSemanas = (int)datosFecha.Average(d => d.EdadSemanas),
                    NumeroAves = datosFecha.Sum(d => d.NumeroAves),
                    MortalidadTotal = datosFecha.Sum(d => d.MortalidadTotal),
                    MortalidadPorcentajeDiario = datosFecha.Average(d => d.MortalidadPorcentajeDiario),
                    MortalidadPorcentajeAcumulado = datosFecha.Average(d => d.MortalidadPorcentajeAcumulado),
                    ErrorSexajeNumero = datosFecha.Sum(d => d.ErrorSexajeNumero),
                    ErrorSexajePorcentaje = datosFecha.Average(d => d.ErrorSexajePorcentaje),
                    ErrorSexajePorcentajeAcumulado = datosFecha.Average(d => d.ErrorSexajePorcentajeAcumulado),
                    DescarteNumero = datosFecha.Sum(d => d.DescarteNumero),
                    DescartePorcentajeDiario = datosFecha.Average(d => d.DescartePorcentajeDiario),
                    DescartePorcentajeAcumulado = datosFecha.Average(d => d.DescartePorcentajeAcumulado),
                    TrasladosNumero = datosFecha.Sum(d => d.TrasladosNumero),
                    ConsumoBultos = datosFecha.Sum(d => d.ConsumoBultos),
                    ConsumoKilos = datosFecha.Sum(d => d.ConsumoKilos),
                    ConsumoKilosAcumulado = datosFecha.Sum(d => d.ConsumoKilosAcumulado),
                    ConsumoGramosPorAve = datosFecha.Average(d => d.ConsumoGramosPorAve),
                    IngresosAlimentoKilos = datosFecha.Sum(d => d.IngresosAlimentoKilos),
                    TrasladosAlimentoKilos = datosFecha.Sum(d => d.TrasladosAlimentoKilos),
                    PesoActual = datosFecha.Where(d => d.PesoActual.HasValue).Select(d => d.PesoActual!.Value).DefaultIfEmpty(0).Average(),
                    Uniformidad = datosFecha.Where(d => d.Uniformidad.HasValue).Select(d => d.Uniformidad!.Value).DefaultIfEmpty(0).Average(),
                    GananciaPeso = null, // TODO: Calcular ganancia
                    CoeficienteVariacion = datosFecha.Where(d => d.CoeficienteVariacion.HasValue).Select(d => d.CoeficienteVariacion!.Value).DefaultIfEmpty(0).Average(),
                    SeleccionVentasNumero = datosFecha.Sum(d => d.SeleccionVentasNumero),
                    SeleccionVentasPorcentaje = datosFecha.Average(d => d.SeleccionVentasPorcentaje)
                };
            })
            .OrderBy(d => d.Fecha)
            .ToList();
    }

    private ReporteTecnicoLoteInfoDto MapearInformacionLote(Lote lote)
    {
        // Determinar etapa basado en edad
        var etapa = "LEVANTE"; // Por defecto
        if (lote.FechaEncaset.HasValue)
        {
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, DateTime.Now);
            if (edadDias >= 175) // 25 semanas * 7 días
                etapa = "PRODUCCION";
        }

        return new ReporteTecnicoLoteInfoDto
        {
            LoteId = lote.LoteId ?? 0,
            LoteNombre = lote.LoteNombre,
            Raza = lote.Raza,
            Linea = lote.Linea,
            Etapa = etapa,
            FechaEncaset = lote.FechaEncaset,
            NumeroHembras = lote.HembrasL,
            NumeroMachos = lote.MachosL,
            Galpon = int.TryParse(lote.GalponId, out var galponId) ? galponId : null,
            Tecnico = lote.Tecnico,
            GranjaNombre = lote.Farm?.Name,
            NucleoNombre = lote.Nucleo?.NucleoNombre
        };
    }

    private ReporteTecnicoLoteInfoDto MapearInformacionLoteFromLPL(LotePosturaLevante lpl)
    {
        return new ReporteTecnicoLoteInfoDto
        {
            LoteId = lpl.LotePosturaLevanteId ?? 0,
            LoteNombre = lpl.LoteNombre,
            Raza = lpl.Raza,
            Linea = lpl.Linea,
            Etapa = "LEVANTE",
            FechaEncaset = lpl.FechaEncaset,
            NumeroHembras = lpl.HembrasL,
            NumeroMachos = lpl.MachosL,
            Galpon = int.TryParse(lpl.GalponId, out var gid) ? gid : null,
            Tecnico = lpl.Tecnico,
            GranjaNombre = lpl.Farm?.Name,
            NucleoNombre = lpl.Nucleo?.NucleoNombre
        };
    }

    private string? ExtraerSublote(string loteNombre)
    {
        // Extraer el sublote del nombre del lote
        // Ejemplo: "K326 A" -> "A", "K326 B" -> "B", "K326" -> null
        var partes = loteNombre.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (partes.Length > 1)
        {
            var ultimaParte = partes.Last();
            // Verificar si la última parte es una letra (sublote)
            if (ultimaParte.Length == 1 && char.IsLetter(ultimaParte[0]))
                return ultimaParte.ToUpper();
        }
        return null;
    }

    private int CalcularEdadDias(DateTime fechaEncaset, DateTime fechaRegistro)
    {
        // Normalizar ambas fechas a la misma zona horaria (local) y obtener solo la fecha
        // Esto evita problemas con zonas horarias diferentes
        var fechaEncasetLocal = fechaEncaset.Kind == DateTimeKind.Utc 
            ? fechaEncaset.ToLocalTime() 
            : fechaEncaset;
            
        var fechaRegistroLocal = fechaRegistro.Kind == DateTimeKind.Utc 
            ? fechaRegistro.ToLocalTime() 
            : fechaRegistro;
        
        // Obtener solo la fecha (sin hora) para comparar días completos
        var fechaEncasetDate = fechaEncasetLocal.Date;
        var fechaRegistroDate = fechaRegistroLocal.Date;
        
        // Calcular diferencia en días
        var diff = fechaRegistroDate - fechaEncasetDate;
        var diasDiferencia = diff.Days;
        
        // En avicultura: día 1 = día del encasetamiento
        // Si el registro es el mismo día del encaset = día 1
        // Si el registro es 1 día después = día 2
        // Por lo tanto: edad = diferencia + 1
        // Ejemplo: 
        // - Encaset: 28 enero, Registro: 28 enero → diferencia = 0 → edad = 1 día
        // - Encaset: 28 enero, Registro: 29 enero → diferencia = 1 → edad = 2 días
        return Math.Max(1, diasDiferencia + 1);
    }

    private int CalcularEdadSemanas(int edadDias)
    {
        // 7 días = 1 semana
        // Semana 1 = días 1-7
        // Semana 2 = días 8-14
        // etc.
        return (int)Math.Ceiling(edadDias / 7.0);
    }

    private decimal CalcularBultos(decimal kilos)
    {
        // Asumiendo que un bulto estándar pesa 40kg
        const decimal pesoBulto = 40m;
        return kilos / pesoBulto;
    }

    private async Task<decimal> ObtenerIngresosAlimentoAsync(int granjaId, DateTime fecha, CancellationToken ct)
    {
        // Obtener ingresos de alimentos (Entry, TransferIn) del día
        // Filtrar por nombre que contenga "alimento" o códigos comunes de alimentos
        try
        {
            var ingresos = await _ctx.FarmInventoryMovements
                .AsNoTracking()
                .Include(m => m.CatalogItem)
                .Where(m => m.FarmId == granjaId &&
                           m.CreatedAt.Date == fecha.Date &&
                           (m.MovementType == Domain.Enums.InventoryMovementType.Entry ||
                            m.MovementType == Domain.Enums.InventoryMovementType.TransferIn) &&
                           m.CatalogItem != null &&
                           (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                            m.CatalogItem.Nombre.ToLower().Contains("food") ||
                            (m.CatalogItem.Codigo != null && m.CatalogItem.Codigo.ToLower().StartsWith("al"))))
                .SumAsync(m => m.Quantity, ct);

            return ingresos;
        }
        catch
        {
            return 0; // Si hay error, retornar 0
        }
    }

    private async Task<decimal> ObtenerTrasladosAlimentoAsync(int granjaId, DateTime fecha, CancellationToken ct)
    {
        // Obtener traslados de alimentos (TransferOut) del día
        try
        {
            var traslados = await _ctx.FarmInventoryMovements
                .AsNoTracking()
                .Include(m => m.CatalogItem)
                .Where(m => m.FarmId == granjaId &&
                           m.CreatedAt.Date == fecha.Date &&
                           m.MovementType == Domain.Enums.InventoryMovementType.TransferOut &&
                           m.CatalogItem != null &&
                           (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                            m.CatalogItem.Nombre.ToLower().Contains("food") ||
                            (m.CatalogItem.Codigo != null && m.CatalogItem.Codigo.ToLower().StartsWith("al"))))
                .SumAsync(m => m.Quantity, ct);

            return traslados;
        }
        catch
        {
            return 0; // Si hay error, retornar 0
        }
    }

    public async Task<ReporteTecnicoLevanteCompletoDto> GenerarReporteLevanteCompletoAsync(
        int lotePosturaLevanteId,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        var lpl = await _ctx.LotePosturaLevante
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lpl == null)
            throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");

        if (!lpl.FechaEncaset.HasValue)
            throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");

        // Determinar lotes a procesar (consolidado o solo el actual)
        List<LotePosturaLevante> lotesAProcesar;
        var sublotesIncluidos = new List<string>();

        if (consolidarSublotes)
        {
            lotesAProcesar = await ObtenerSublotesLevantePorLoteBaseAsync(lotePosturaLevanteId, ct);
            if (!lotesAProcesar.Any())
            {
                lotesAProcesar = new List<LotePosturaLevante> { lpl };
            }

            // Agregar nombres de sublotes
            foreach (var lote in lotesAProcesar)
            {
                var nombreSublote = ExtraerSublote(lote.LoteNombre) ?? "Sin sublote";
                if (!sublotesIncluidos.Contains(nombreSublote))
                {
                    sublotesIncluidos.Add(nombreSublote);
                }
            }
        }
        else
        {
            lotesAProcesar = new List<LotePosturaLevante> { lpl };
            var sublote = ExtraerSublote(lpl.LoteNombre) ?? "Sin sublote";
            sublotesIncluidos.Add(sublote);
        }

        var infoLote = MapearInformacionLoteFromLPL(lpl);
        infoLote.Sublote = consolidarSublotes ? null : ExtraerSublote(lpl.LoteNombre);

        // Obtener seguimientos consolidados desde tabla unificada
        var todosSeguimientos = new List<SegLevanteParaReporte>();
        foreach (var lote in lotesAProcesar)
        {
            var seguimientosLote = await ObtenerSeguimientosLevantePorLPLAsync(lote.LotePosturaLevanteId ?? 0, ct);
            todosSeguimientos.AddRange(seguimientosLote);
        }

        // Filtrar solo semanas de levante (1-25)
        var seguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();

        // Obtener guía genética del lote (desde produccion_avicola_raw)
        // El lote levante tiene Raza y AnoTablaGenetica que se usan para buscar la guía
        Dictionary<int, Domain.Entities.ProduccionAvicolaRaw> guiasRaw = new();
        Dictionary<int, GuiaGeneticaDto> guiasGenetica = new();
        
        if (!string.IsNullOrWhiteSpace(lpl.Raza) && lpl.AnoTablaGenetica.HasValue)
        {
            try
            {
                var razaNorm = lpl.Raza.Trim().ToLower();
                var ano = lpl.AnoTablaGenetica.Value.ToString();
                
                // Obtener datos raw directamente para tener acceso a ConsAcH, ConsAcM, etc.
                var guiasRawList = await _ctx.ProduccionAvicolaRaw
                    .AsNoTracking()
                    .Where(p =>
                        p.Raza != null && p.AnioGuia != null &&
                        EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                        p.AnioGuia.Trim() == ano &&
                        p.CompanyId == _currentUser.CompanyId &&
                        p.DeletedAt == null
                    )
                    .ToListAsync(ct);
                
                // Parsear edades y crear diccionario
                foreach (var guia in guiasRawList)
                {
                    var edadStr = guia.Edad;
                    if (int.TryParse(edadStr?.Trim().Replace(",", ".").Split('.')[0], out var edad))
                    {
                        if (edad >= 1 && edad <= 25)
                        {
                            guiasRaw[edad] = guia;
                        }
                    }
                }
                
                // También obtener los DTOs procesados para usar los métodos de parseo
                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(
                    lpl.Raza, 
                    lpl.AnoTablaGenetica.Value, 
                    edadDesde: 1, 
                    edadHasta: 25);
                
                guiasGenetica = guias.ToDictionary(g => g.Edad, g => g);
            }
            catch
            {
                // Si no se encuentra la guía, continuar sin valores GUIA
                // Los valores GUIA quedarán como null
            }
        }

        // Obtener traslados del lote para verificar reducciones (LoteOrigenId = lotes.lote_id si existe)
        var loteIdParaTraslados = lpl.LoteId ?? lotePosturaLevanteId;
        var traslados = await _ctx.Set<Domain.Entities.MovimientoAves>()
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteIdParaTraslados && 
                       m.Estado == "Completado" &&
                       m.DeletedAt == null)
            .OrderBy(m => m.FechaMovimiento)
            .ToListAsync(ct);

        // Helper para parsear valores de la guía raw
        static double ParseGuiaRaw(string? value) => 
            double.TryParse(value?.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any, 
                System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;

        // Calcular datos semanales (semanas 1-25)
        var datosSemanales = new List<ReporteTecnicoLevanteSemanalDto>();

        // Sumar aves iniciales: si es consolidado de todos los lotes, si no solo del lote actual
        var hembraIni = consolidarSublotes
            ? lotesAProcesar.Sum(l => l.HembrasL ?? 0)
            : lpl.HembrasL ?? 0;
        var machoIni = consolidarSublotes
            ? lotesAProcesar.Sum(l => l.MachosL ?? 0)
            : lpl.MachosL ?? 0;

        // Variables acumuladas
        int acMortH = 0, acSelH = 0, acErrH = 0;
        int acMortM = 0, acSelM = 0, acErrM = 0;
        double acConsH = 0, acConsM = 0;
        double acKcalSemH = 0, acKcalSemM = 0;
        double acProtSemH = 0, acProtSemM = 0;
        double? consAcGrHAnterior = null;
        double? consAcGrMAnterior = null;
        // Variables para calcular incrementos de la guía genética
        double? consAcGrHGUIAAnterior = null;
        double? consAcGrMGUIAAnterior = null;
        double? pesoHGUIAAnterior = null;
        double? pesoMGUIAAnterior = null;

        for (int semana = 1; semana <= 25; semana++)
        {
            // Calcular rango de fechas para la semana
            var fechaInicioSemana = lpl.FechaEncaset!.Value.AddDays((semana - 1) * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            // Obtener registros de esta semana
            var registrosSemana = seguimientos.Where(s =>
            {
                var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, s.FechaRegistro);
                var edadSemanas = CalcularEdadSemanas(edadDias);
                return edadSemanas == semana;
            }).ToList();

            if (!registrosSemana.Any() && semana > 1)
            {
                // Si no hay registros y no es la primera semana, podemos saltarla o crear registro vacío
                // Por ahora, saltamos semanas sin datos
                continue;
            }

            // Calcular valores de la semana
            var mortH = registrosSemana.Sum(s => s.MortalidadHembras);
            var mortM = registrosSemana.Sum(s => s.MortalidadMachos);
            var selH = registrosSemana.Sum(s => Math.Max(0, s.SelH)); // Solo valores positivos
            var selM = registrosSemana.Sum(s => Math.Max(0, s.SelM)); // Solo valores positivos
            var errorH = registrosSemana.Sum(s => s.ErrorSexajeHembras);
            var errorM = registrosSemana.Sum(s => s.ErrorSexajeMachos);
            var consKgH = registrosSemana.Sum(s => s.ConsumoKgHembras);
            var consKgM = registrosSemana.Sum(s => s.ConsumoKgMachos ?? 0);

            // Calcular traslados de la semana (valores negativos de SelH/SelM)
            var trasladosSemana = registrosSemana.Sum(s => 
                Math.Abs(Math.Min(0, s.SelH)) + Math.Abs(Math.Min(0, s.SelM)));

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acSelH += selH;
            acSelM += selM;
            acErrH += errorH;
            acErrM += errorM;
            acConsH += consKgH;
            acConsM += consKgM;

            // Calcular saldos actuales
            var hembra = hembraIni - acMortH - acSelH - acErrH;
            var saldoMacho = machoIni - acMortM - acSelM - acErrM;

            // Obtener valores promedio de peso y uniformidad de la semana
            var pesoH = registrosSemana.Where(s => s.PesoPromH.HasValue)
                .Select(s => s.PesoPromH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var pesoM = registrosSemana.Where(s => s.PesoPromM.HasValue)
                .Select(s => s.PesoPromM!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var uniformH = registrosSemana.Where(s => s.UniformidadH.HasValue)
                .Select(s => s.UniformidadH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var uniformM = registrosSemana.Where(s => s.UniformidadM.HasValue)
                .Select(s => s.UniformidadM!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var cvH = registrosSemana.Where(s => s.CvH.HasValue)
                .Select(s => s.CvH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var cvM = registrosSemana.Where(s => s.CvM.HasValue)
                .Select(s => s.CvM!.Value)
                .DefaultIfEmpty(0)
                .Average();

            // Obtener valores nutricionales (promedio de la semana)
            // Nota: La entidad solo tiene KcalAlH y ProtAlH, usamos los mismos valores para machos
            var kcalAlH = registrosSemana.Where(s => s.KcalAlH.HasValue)
                .Select(s => s.KcalAlH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            var protAlH = registrosSemana.Where(s => s.ProtAlH.HasValue)
                .Select(s => s.ProtAlH!.Value)
                .DefaultIfEmpty(0)
                .Average();
            // Usar los mismos valores nutricionales de hembras para machos (mismo tipo de alimento)
            var kcalAlM = kcalAlH;
            var protAlM = protAlH;

            // Obtener guía genética para esta semana (desde produccion_avicola_raw)
            var guiaGenetica = guiasGenetica.TryGetValue(semana, out var guia) ? guia : null;
            var guiaRaw = guiasRaw.TryGetValue(semana, out var raw) ? raw : null;

            // Calcular campos según fórmulas Excel
            var dto = new ReporteTecnicoLevanteSemanalDto
            {
                CodGuia = lpl.CodigoGuiaGenetica,
                IdLoteRAP = null,
                Regional = lpl.Regional,
                Granja = lpl.Farm?.Name,
                Lote = lpl.LoteNombre,
                Raza = lpl.Raza,
                AnoG = lpl.AnoTablaGenetica,
                HembraIni = hembraIni,
                MachoIni = machoIni,
                Traslado = null,
                NucleoL = lpl.Nucleo?.NucleoNombre,
                Anon = null,
                Edad = CalcularEdadDias(lpl.FechaEncaset!.Value, fechaInicioSemana),
                Fecha = fechaInicioSemana,
                SemAno = GetSemanaAno(fechaInicioSemana),
                Semana = semana,

                // Datos hembras
                Hembra = hembra,
                MortH = mortH,
                SelH = selH,
                ErrorH = errorH,
                ConsKgH = consKgH,
                PesoH = pesoH > 0 ? pesoH : null,
                UniformH = uniformH > 0 ? uniformH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,

                // Datos machos
                SaldoMacho = saldoMacho,
                MortM = mortM,
                SelM = selM,
                ErrorM = errorM,
                ConsKgM = consKgM,
                PesoM = pesoM > 0 ? pesoM : null,
                UniformM = uniformM > 0 ? uniformM : null,
                CvM = cvM > 0 ? cvM : null,
                KcalAlM = kcalAlM > 0 ? kcalAlM : null,
                ProtAlM = protAlM > 0 ? protAlM : null,

                // Cálculos de eficiencia
                KcalAveH = hembra > 0 && kcalAlH > 0 ? (kcalAlH * consKgH) / hembra : null,
                ProtAveH = hembra > 0 && protAlH > 0 ? (protAlH * consKgH) / hembra : null,
                KcalAveM = saldoMacho > 0 && kcalAlM > 0 ? (kcalAlM * consKgM) / saldoMacho : null,
                ProtAveM = saldoMacho > 0 && protAlM > 0 ? (protAlM * consKgM) / saldoMacho : null,

                RelMH = hembra > 0 ? (saldoMacho / (double)hembra * 100) : null,
                PorcMortH = hembraIni > 0 ? (mortH / (double)hembraIni * 100) : null,
                PorcMortHGUIA = guiaGenetica != null ? guiaGenetica.MortalidadHembras : null,
                DifMortH = guiaGenetica != null && hembraIni > 0 
                    ? (mortH / (double)hembraIni * 100) - guiaGenetica.MortalidadHembras 
                    : null,
                ACMortH = acMortH,

                PorcSelH = hembraIni > 0 ? (selH / (double)hembraIni * 100) : null,
                ACSelH = acSelH,
                PorcErrH = hembraIni > 0 ? (errorH / (double)hembraIni * 100) : null,
                ACErrH = acErrH,

                MSEH = mortH + selH + errorH,
                RetAcH = acMortH + acSelH + acErrH,
                PorcRetiroH = hembraIni > 0 ? ((acMortH + acSelH + acErrH) / (double)hembraIni * 100) : null,
                RetiroHGUIA = guiaGenetica != null ? guiaGenetica.RetiroAcumuladoHembras : null,

                AcConsH = acConsH,
                ConsAcGrH = hembraIni > 0 ? (acConsH * 1000) / hembraIni : null,
                ConsAcGrHGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcH) : null, // ConsAcH de la guía (consumo acumulado en gramos)
                GrAveDiaH = hembra > 0 ? (consKgH * 1000) / hembra / 7 : null,
                GrAveDiaGUIAH = guiaGenetica != null ? guiaGenetica.ConsumoHembras : null, // GrAveDiaH de la guía (gramos por ave por día)
                IncrConsH = consAcGrHAnterior.HasValue 
                    ? ((acConsH * 1000) / hembraIni) - consAcGrHAnterior.Value 
                    : null,
                IncrConsHGUIA = consAcGrHGUIAAnterior.HasValue && guiaRaw != null
                    ? ParseGuiaRaw(guiaRaw.ConsAcH) - consAcGrHGUIAAnterior.Value
                    : (semana == 1 && guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcH) : null), // Primera semana: el valor es el incremento inicial
                PorcDifConsH = guiaRaw != null && ParseGuiaRaw(guiaRaw.ConsAcH) > 0
                    ? (((acConsH * 1000) / hembraIni) - ParseGuiaRaw(guiaRaw.ConsAcH)) / ParseGuiaRaw(guiaRaw.ConsAcH) * 100
                    : null,

                PesoHGUIA = guiaGenetica != null ? guiaGenetica.PesoHembras / 1000.0 : null, // Convertir de gramos a kg
                PorcDifPesoH = guiaGenetica != null && guiaGenetica.PesoHembras > 0 && pesoH > 0
                    ? (pesoH - (guiaGenetica.PesoHembras / 1000.0)) / (guiaGenetica.PesoHembras / 1000.0) * 100
                    : null,
                UnifHGUIA = guiaGenetica != null ? guiaGenetica.Uniformidad : null,

                PorcMortM = machoIni > 0 ? (mortM / (double)machoIni * 100) : null,
                PorcMortMGUIA = guiaGenetica != null ? guiaGenetica.MortalidadMachos : null,
                DifMortM = guiaGenetica != null && machoIni > 0
                    ? (mortM / (double)machoIni * 100) - guiaGenetica.MortalidadMachos
                    : null,
                ACMortM = acMortM,

                PorcSelM = machoIni > 0 ? (selM / (double)machoIni * 100) : null,
                ACSelM = acSelM,
                PorcErrM = machoIni > 0 ? (errorM / (double)machoIni * 100) : null,
                ACErrM = acErrM,

                MSEM = mortM + selM + errorM,
                RetAcM = acMortM + acSelM + acErrM,
                PorcRetAcM = machoIni > 0 ? ((acMortM + acSelM + acErrM) / (double)machoIni * 100) : null,
                RetiroMGUIA = guiaGenetica != null ? guiaGenetica.RetiroAcumuladoMachos : null,

                AcConsM = acConsM,
                ConsAcGrM = machoIni > 0 ? (acConsM * 1000) / machoIni : null,
                ConsAcGrMGUIA = guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcM) : null, // ConsAcM de la guía (consumo acumulado en gramos)
                GrAveDiaM = saldoMacho > 0 ? (consKgM * 1000) / saldoMacho / 7 : null,
                GrAveDiaMGUIA = guiaGenetica != null ? guiaGenetica.ConsumoMachos : null, // GrAveDiaM de la guía (gramos por ave por día)
                IncrConsM = consAcGrMAnterior.HasValue
                    ? ((acConsM * 1000) / machoIni) - consAcGrMAnterior.Value
                    : null,
                IncrConsMGUIA = consAcGrMGUIAAnterior.HasValue && guiaRaw != null
                    ? ParseGuiaRaw(guiaRaw.ConsAcM) - consAcGrMGUIAAnterior.Value
                    : (semana == 1 && guiaRaw != null ? ParseGuiaRaw(guiaRaw.ConsAcM) : null), // Primera semana: el valor es el incremento inicial
                DifConsM = guiaRaw != null
                    ? ((acConsM * 1000) / machoIni) - ParseGuiaRaw(guiaRaw.ConsAcM)
                    : null,

                PesoMGUIA = guiaGenetica != null ? guiaGenetica.PesoMachos / 1000.0 : null, // Convertir de gramos a kg
                PorcDifPesoM = guiaGenetica != null && guiaGenetica.PesoMachos > 0 && pesoM > 0
                    ? (pesoM - (guiaGenetica.PesoMachos / 1000.0)) / (guiaGenetica.PesoMachos / 1000.0) * 100
                    : null,
                UnifMGUIA = guiaGenetica != null ? guiaGenetica.Uniformidad : null,

                ErrSexAcH = null, // No está en la guía genética, se puede agregar manualmente si es necesario
                PorcErrSxAcH = null,
                ErrSexAcM = null, // No está en la guía genética, se puede agregar manualmente si es necesario
                PorcErrSxAcM = null,

                DifConsAcH = guiaRaw != null
                    ? acConsH - (ParseGuiaRaw(guiaRaw.ConsAcH) * hembraIni / 1000)
                    : null,
                DifConsAcM = guiaRaw != null
                    ? acConsM - (ParseGuiaRaw(guiaRaw.ConsAcM) * machoIni / 1000)
                    : null,

                // Datos nutricionales
                // Nota: Los valores nutricionales (Kcal, Prot) no están en la guía genética estándar
                // Se pueden agregar manualmente o desde otra fuente si es necesario
                AlimHGUIA = null, // Tipo de alimento (se puede obtener del seguimiento)
                KcalSemH = kcalAlH > 0 ? kcalAlH * consKgH : null,
                KcalSemAcH = acKcalSemH + (kcalAlH > 0 ? kcalAlH * consKgH : 0),
                KcalSemHGUIA = null, // No disponible en guía genética estándar
                KcalSemAcHGUIA = null,
                ProtSemH = protAlH > 0 ? (protAlH / 100) * consKgH : null,
                ProtSemAcH = acProtSemH + (protAlH > 0 ? (protAlH / 100) * consKgH : 0),
                ProtSemHGUIA = null, // No disponible en guía genética estándar
                ProtSemAcHGUIA = null,

                AlimMGUIA = null, // Tipo de alimento (se puede obtener del seguimiento)
                KcalSemM = kcalAlM > 0 ? kcalAlM * consKgM : null,
                KcalSemAcM = acKcalSemM + (kcalAlM > 0 ? kcalAlM * consKgM : 0),
                KcalSemMGUIA = null, // No disponible en guía genética estándar
                KcalSemAcMGUIA = null,
                ProtSemM = protAlM > 0 ? (protAlM / 100) * consKgM : null,
                ProtSemAcM = acProtSemM + (protAlM > 0 ? (protAlM / 100) * consKgM : 0),
                ProtSemMGUIA = null, // No disponible en guía genética estándar
                ProtSemAcMGUIA = null,

                Observaciones = string.Join("; ", registrosSemana
                    .Where(s => !string.IsNullOrEmpty(s.Observaciones))
                    .Select(s => s.Observaciones)
                    .Distinct())
            };

            // Actualizar acumulados nutricionales
            if (dto.KcalSemH.HasValue)
                acKcalSemH += dto.KcalSemH.Value;
            if (dto.KcalSemM.HasValue)
                acKcalSemM += dto.KcalSemM.Value;
            if (dto.ProtSemH.HasValue)
                acProtSemH += dto.ProtSemH.Value;
            if (dto.ProtSemM.HasValue)
                acProtSemM += dto.ProtSemM.Value;

            // Actualizar valores anteriores para siguiente semana
            if (dto.ConsAcGrH.HasValue)
                consAcGrHAnterior = dto.ConsAcGrH.Value;
            if (dto.ConsAcGrM.HasValue)
                consAcGrMAnterior = dto.ConsAcGrM.Value;
            
            // Actualizar valores anteriores de la guía genética para calcular incrementos
            if (dto.ConsAcGrHGUIA.HasValue)
                consAcGrHGUIAAnterior = dto.ConsAcGrHGUIA.Value;
            if (dto.ConsAcGrMGUIA.HasValue)
                consAcGrMGUIAAnterior = dto.ConsAcGrMGUIA.Value;
            if (dto.PesoHGUIA.HasValue)
                pesoHGUIAAnterior = dto.PesoHGUIA.Value;
            if (dto.PesoMGUIA.HasValue)
                pesoMGUIAAnterior = dto.PesoMGUIA.Value;

            datosSemanales.Add(dto);
        }

        return new ReporteTecnicoLevanteCompletoDto
        {
            InformacionLote = infoLote,
            DatosSemanales = datosSemanales,
            EsConsolidado = consolidarSublotes,
            SublotesIncluidos = sublotesIncluidos
        };
    }

    private int GetSemanaAno(DateTime fecha)
    {
        var calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return calendar.GetWeekOfYear(fecha, 
            System.Globalization.CalendarWeekRule.FirstDay, 
            DayOfWeek.Monday);
    }

    /// <summary>
    /// Genera reporte diario específico de MACHOS desde el seguimiento diario de levante.
    /// lotePosturaLevanteId = id de lote_postura_levante (seguimiento_diario.lote_postura_levante_id).
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");
            
            if (!lpl.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");
            
            var machosIniciales = lpl.MachosL ?? 0;
            var granjaId = lpl.GranjaId;
        
        var todosSeguimientos = await ObtenerSeguimientosLevantePorLPLAsync(lotePosturaLevanteId, ct);
        
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();
        
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);
        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);
        
        var seguimientos = queryFiltrado.ToList();
        
        var datosMachos = new List<ReporteTecnicoDiarioMachosDto>();
        decimal? pesoAnterior = null;
        
        decimal porcMortalidadAcumuladaAnterior = 0;
        decimal porcSeleccionAcumuladaAnterior = 0;
        decimal porcDescarteAcumuladaAnterior = 0;
        decimal porcErrorSexajeAcumuladaAnterior = 0;
        decimal consumoAcumuladoAnterior = 0;
        
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad_diaria - seleccion_diaria - descarte_diaria
            // Donde: seleccion_diaria = traslados, descarte_diaria = seleccion_normal + error_sexaje
            var machosActuales = machosIniciales;
            
            foreach (var reg in registrosHastaFecha)
            {
                // Aplicar mortalidad
                machosActuales -= reg.MortalidadMachos;
                
                // Separar selección normal de traslados
                var selMReg = reg.SelM;
                var seleccionNormalReg = Math.Max(0, selMReg);
                var trasladosReg = Math.Abs(Math.Min(0, selMReg));
                
                // Descarte = selección normal + error de sexaje
                var descarteReg = seleccionNormalReg + reg.ErrorSexajeMachos;
                
                // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad - seleccion (traslados) - descarte
                machosActuales -= trasladosReg; // seleccion_diaria (traslados)
                machosActuales -= descarteReg;  // descarte_diaria (seleccion_normal + error_sexaje)
            }
            
            // Calcular valores del día actual
            var mortalidad = seg.MortalidadMachos;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadMachos);
            
            var selM = seg.SelM;
            var seleccionNormal = Math.Max(0, selM);
            var traslados = Math.Abs(Math.Min(0, selM));
            var seleccionAcumulada = registrosHastaFecha.Sum(s => Math.Max(0, s.SelM));
            var trasladosAcumulados = registrosHastaFecha.Sum(s => Math.Abs(Math.Min(0, s.SelM)));
            
            var errorSexaje = seg.ErrorSexajeMachos;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeMachos);
            
            // Descarte = selección + error de sexaje
            var descarteDiaria = seleccionNormal + errorSexaje;
            var descarteAcumulada = seleccionAcumulada + errorSexajeAcumulado;
            
            // FÓRMULAS SEGÚN EXCEL:
            // porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
            var porcMortalidadDiaria = machosIniciales > 0 
                ? (decimal)mortalidad / machosIniciales * 100 
                : 0;
            
            // porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
            var porcMortalidadAcumulada = porcMortalidadAcumuladaAnterior + porcMortalidadDiaria;
            
            // porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
            var porcSeleccionDiaria = machosIniciales > 0 
                ? (decimal)seleccionNormal / machosIniciales * 100 
                : 0;
            
            // porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
            var porcSeleccionAcumulada = porcSeleccionAcumuladaAnterior + porcSeleccionDiaria;
            
            // porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
            var porcDescarteDiario = machosIniciales > 0 
                ? (decimal)descarteDiaria / machosIniciales * 100 
                : 0;
            
            // porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
            var porcDescarteAcumulada = porcDescarteAcumuladaAnterior + porcDescarteDiario;
            
            // CONSUMO según fórmulas Excel:
            // consumo_diario = consumo_semanal / 40 (en bultos, asumiendo 40kg por bulto)
            // Nota: En el seguimiento tenemos consumo diario en kg, así que:
            // consumo_diario_bultos = consumo_kg / 40
            var consumoKg = (decimal)(seg.ConsumoKgMachos ?? 0);
            var consumoDiarioBultos = consumoKg / 40;
            
            // consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
            // Nota: consumo_semanal en kg, así que acumulamos en kg
            var consumoAcumulado = consumoAcumuladoAnterior + consumoKg;
            
            // consumo_por_ave = (consumo_diario * 40000) / aves_vivas
            // consumo_diario está en bultos, entonces (bultos * 40000g) / aves = gramos por ave
            var consumoGramosPorAve = machosActuales > 0 
                ? (consumoDiarioBultos * 40000) / machosActuales 
                : 0;
            
            // consumo_total_kg = (aves_vivas * consumo_unitario_gramos) / 1000
            // consumo_unitario_gramos es el consumo por ave en gramos
            var consumoTotalKg = (machosActuales * consumoGramosPorAve) / 1000;
            
            // Peso y ganancia
            var pesoActual = (decimal?)(seg.PesoPromM);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;
            
            // Valores nutricionales
            var kcalAl = seg.KcalAlH; // Mismo alimento para machos y hembras
            var protAl = seg.ProtAlH;
            var kcalAve = machosActuales > 0 && kcalAl.HasValue 
                ? (kcalAl.Value * (double)consumoKg) / machosActuales 
                : (double?)null;
            var protAve = machosActuales > 0 && protAl.HasValue 
                ? (protAl.Value * (double)consumoKg) / machosActuales 
                : (double?)null;
            
            // Ingresos y traslados de alimento
            var ingresosAlimento = granjaId > 0 
                ? await ObtenerIngresosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            var trasladosAlimento = granjaId > 0 
                ? await ObtenerTrasladosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            
            var dto = new ReporteTecnicoDiarioMachosDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoMachos = machosActuales,
                MortalidadMachos = mortalidad,
                MortalidadMachosAcumulada = mortalidadAcumulada,
                // FÓRMULA EXCEL: porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
                MortalidadMachosPorcentajeDiario = porcMortalidadDiaria,
                // FÓRMULA EXCEL: porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
                MortalidadMachosPorcentajeAcumulado = porcMortalidadAcumulada,
                SeleccionMachos = seleccionNormal,
                SeleccionMachosAcumulada = seleccionAcumulada,
                // FÓRMULA EXCEL: porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
                SeleccionMachosPorcentajeDiario = porcSeleccionDiaria,
                // FÓRMULA EXCEL: porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
                SeleccionMachosPorcentajeAcumulado = porcSeleccionAcumulada,
                TrasladosMachos = traslados,
                TrasladosMachosAcumulados = trasladosAcumulados,
                ErrorSexajeMachos = errorSexaje,
                ErrorSexajeMachosAcumulado = errorSexajeAcumulado,
                // Error de sexaje también sobre total_inicial_aves
                // porc_error_diario = (error_diario / total_inicial_aves) * 100
                ErrorSexajeMachosPorcentajeDiario = machosIniciales > 0 
                    ? (decimal)errorSexaje / machosIniciales * 100 
                    : 0,
                // porc_error_acumulado = porc_error_acumulado_anterior + porc_error_diario
                ErrorSexajeMachosPorcentajeAcumulado = porcErrorSexajeAcumuladaAnterior + (machosIniciales > 0 
                    ? (decimal)errorSexaje / machosIniciales * 100 
                    : 0),
                // DESCARTE (Selección + Error Sexaje)
                DescarteMachos = descarteDiaria,
                DescarteMachosAcumulado = descarteAcumulada,
                // FÓRMULA EXCEL: porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
                DescarteMachosPorcentajeDiario = porcDescarteDiario,
                // FÓRMULA EXCEL: porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
                DescarteMachosPorcentajeAcumulado = porcDescarteAcumulada,
                // FÓRMULA EXCEL: consumo_diario = consumo_semanal / 40 (en bultos)
                // Guardamos consumo en kg (consumoKg), pero el cálculo de gramos/ave usa la fórmula Excel
                ConsumoKgMachos = consumoKg,
                // FÓRMULA EXCEL: consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
                ConsumoKgMachosAcumulado = consumoAcumulado,
                // FÓRMULA EXCEL: consumo_por_ave = (consumo_diario * 40000) / aves_vivas
                ConsumoGramosPorAveMachos = consumoGramosPorAve,
                PesoPromedioMachos = pesoActual,
                UniformidadMachos = (decimal?)(seg.UniformidadM),
                CoeficienteVariacionMachos = (decimal?)(seg.CvM),
                GananciaPesoMachos = gananciaPeso,
                KcalAlMachos = kcalAl,
                ProtAlMachos = protAl,
                KcalAveMachos = kcalAve,
                ProtAveMachos = protAve,
                IngresosAlimentoKilos = ingresosAlimento,
                TrasladosAlimentoKilos = trasladosAlimento,
                Observaciones = seg.Observaciones
            };
            
            // Actualizar valores acumulados para la siguiente iteración
            porcMortalidadAcumuladaAnterior = porcMortalidadAcumulada;
            porcSeleccionAcumuladaAnterior = porcSeleccionAcumulada;
            porcDescarteAcumuladaAnterior = porcDescarteAcumulada;
            porcErrorSexajeAcumuladaAnterior = dto.ErrorSexajeMachosPorcentajeAcumulado;
            consumoAcumuladoAnterior = consumoAcumulado;
            
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;
            
            datosMachos.Add(dto);
        }
        
        return datosMachos;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte diario de machos para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Genera reporte diario específico de HEMBRAS desde el seguimiento diario de levante.
    /// lotePosturaLevanteId = id de lote_postura_levante.
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioHembrasDto>> GenerarReporteDiarioHembrasAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");
            
            if (!lpl.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote levante {lotePosturaLevanteId} no tiene fecha de encaset");
            
            var hembrasIniciales = lpl.HembrasL ?? 0;
            var granjaId = lpl.GranjaId;
        
        var todosSeguimientos = await ObtenerSeguimientosLevantePorLPLAsync(lotePosturaLevanteId, ct);
        
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();
        
        // Aplicar filtros de fecha
        var queryFiltrado = todosSeguimientos.AsQueryable();
        if (fechaInicio.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro >= fechaInicio.Value);
        if (fechaFin.HasValue)
            queryFiltrado = queryFiltrado.Where(s => s.FechaRegistro <= fechaFin.Value);
        
        var seguimientos = queryFiltrado.ToList();
        
        // Procesar cada registro diario
        var datosHembras = new List<ReporteTecnicoDiarioHembrasDto>();
        decimal? pesoAnterior = null;
        
        // Variables para acumular porcentajes (según fórmulas Excel)
        decimal porcMortalidadAcumuladaAnterior = 0;
        decimal porcSeleccionAcumuladaAnterior = 0;
        decimal porcDescarteAcumuladaAnterior = 0;
        decimal porcErrorSexajeAcumuladaAnterior = 0;
        decimal consumoAcumuladoAnterior = 0;
        
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(lpl.FechaEncaset!.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad_diaria - seleccion_diaria - descarte_diaria
            // Donde: seleccion_diaria = traslados, descarte_diaria = seleccion_normal + error_sexaje
            var hembrasActuales = hembrasIniciales;
            
            foreach (var reg in registrosHastaFecha)
            {
                // Aplicar mortalidad
                hembrasActuales -= reg.MortalidadHembras;
                
                // Separar selección normal de traslados
                var selHReg = reg.SelH;
                var seleccionNormalReg = Math.Max(0, selHReg);
                var trasladosReg = Math.Abs(Math.Min(0, selHReg));
                
                // Descarte = selección normal + error de sexaje
                var descarteReg = seleccionNormalReg + reg.ErrorSexajeHembras;
                
                // FÓRMULA EXCEL: aves_vivas = aves_vivas_anterior - mortalidad - seleccion (traslados) - descarte
                hembrasActuales -= trasladosReg; // seleccion_diaria (traslados)
                hembrasActuales -= descarteReg;  // descarte_diaria (seleccion_normal + error_sexaje)
            }
            
            // Calcular valores del día actual
            var mortalidad = seg.MortalidadHembras;
            var mortalidadAcumulada = registrosHastaFecha.Sum(s => s.MortalidadHembras);
            
            var selH = seg.SelH;
            var seleccionNormal = Math.Max(0, selH);
            var traslados = Math.Abs(Math.Min(0, selH));
            var seleccionAcumulada = registrosHastaFecha.Sum(s => Math.Max(0, s.SelH));
            var trasladosAcumulados = registrosHastaFecha.Sum(s => Math.Abs(Math.Min(0, s.SelH)));
            
            var errorSexaje = seg.ErrorSexajeHembras;
            var errorSexajeAcumulado = registrosHastaFecha.Sum(s => s.ErrorSexajeHembras);
            
            // Descarte = selección + error de sexaje
            var descarteDiaria = seleccionNormal + errorSexaje;
            var descarteAcumulada = seleccionAcumulada + errorSexajeAcumulado;
            
            // FÓRMULAS SEGÚN EXCEL:
            // porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
            var porcMortalidadDiaria = hembrasIniciales > 0 
                ? (decimal)mortalidad / hembrasIniciales * 100 
                : 0;
            
            // porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
            var porcMortalidadAcumulada = porcMortalidadAcumuladaAnterior + porcMortalidadDiaria;
            
            // porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
            var porcSeleccionDiaria = hembrasIniciales > 0 
                ? (decimal)seleccionNormal / hembrasIniciales * 100 
                : 0;
            
            // porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
            var porcSeleccionAcumulada = porcSeleccionAcumuladaAnterior + porcSeleccionDiaria;
            
            // porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
            var porcDescarteDiario = hembrasIniciales > 0 
                ? (decimal)descarteDiaria / hembrasIniciales * 100 
                : 0;
            
            // porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
            var porcDescarteAcumulada = porcDescarteAcumuladaAnterior + porcDescarteDiario;
            
            // CONSUMO según fórmulas Excel:
            // consumo_diario = consumo_semanal / 40 (en bultos, asumiendo 40kg por bulto)
            // Nota: En el seguimiento tenemos consumo diario en kg, así que:
            // consumo_diario_bultos = consumo_kg / 40
            var consumoKg = (decimal)seg.ConsumoKgHembras;
            var consumoDiarioBultos = consumoKg / 40;
            
            // consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
            // Nota: consumo_semanal en kg, así que acumulamos en kg
            var consumoAcumulado = consumoAcumuladoAnterior + consumoKg;
            
            // consumo_por_ave = (consumo_diario * 40000) / aves_vivas
            // consumo_diario está en bultos, entonces (bultos * 40000g) / aves = gramos por ave
            var consumoGramosPorAve = hembrasActuales > 0 
                ? (consumoDiarioBultos * 40000) / hembrasActuales 
                : 0;
            
            // consumo_total_kg = (aves_vivas * consumo_unitario_gramos) / 1000
            // consumo_unitario_gramos es el consumo por ave en gramos
            var consumoTotalKg = (hembrasActuales * consumoGramosPorAve) / 1000;
            
            // Peso y ganancia
            var pesoActual = (decimal?)(seg.PesoPromH);
            var gananciaPeso = pesoActual.HasValue && pesoAnterior.HasValue 
                ? pesoActual.Value - pesoAnterior.Value 
                : (decimal?)null;
            
            // Valores nutricionales
            var kcalAl = seg.KcalAlH;
            var protAl = seg.ProtAlH;
            // KcalAveH y ProtAveH pueden venir del seguimiento o calcularse
            var kcalAve = seg.KcalAveH ?? (hembrasActuales > 0 && kcalAl.HasValue 
                ? (kcalAl.Value * (double)consumoKg) / hembrasActuales 
                : (double?)null);
            var protAve = seg.ProtAveH ?? (hembrasActuales > 0 && protAl.HasValue 
                ? (protAl.Value * (double)consumoKg) / hembrasActuales 
                : (double?)null);
            
            // Ingresos y traslados de alimento
            var ingresosAlimento = granjaId > 0 
                ? await ObtenerIngresosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            var trasladosAlimento = granjaId > 0 
                ? await ObtenerTrasladosAlimentoAsync(granjaId, seg.FechaRegistro, ct) 
                : 0;
            
            var dto = new ReporteTecnicoDiarioHembrasDto
            {
                Fecha = seg.FechaRegistro,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoHembras = hembrasActuales,
                MortalidadHembras = mortalidad,
                MortalidadHembrasAcumulada = mortalidadAcumulada,
                // FÓRMULA EXCEL: porc_mortalidad_diaria = (mortalidad_diaria / total_inicial_aves) * 100
                MortalidadHembrasPorcentajeDiario = porcMortalidadDiaria,
                // FÓRMULA EXCEL: porc_mortalidad_acumulada = porc_mortalidad_acumulada_anterior + porc_mortalidad_diaria
                MortalidadHembrasPorcentajeAcumulado = porcMortalidadAcumulada,
                SeleccionHembras = seleccionNormal,
                SeleccionHembrasAcumulada = seleccionAcumulada,
                // FÓRMULA EXCEL: porc_seleccion_diaria = (seleccion_diaria / total_inicial_aves) * 100
                SeleccionHembrasPorcentajeDiario = porcSeleccionDiaria,
                // FÓRMULA EXCEL: porc_seleccion_acumulada = porc_seleccion_acumulada_anterior + porc_seleccion_diaria
                SeleccionHembrasPorcentajeAcumulado = porcSeleccionAcumulada,
                TrasladosHembras = traslados,
                TrasladosHembrasAcumulados = trasladosAcumulados,
                ErrorSexajeHembras = errorSexaje,
                ErrorSexajeHembrasAcumulado = errorSexajeAcumulado,
                // Error de sexaje también sobre total_inicial_aves
                // porc_error_diario = (error_diario / total_inicial_aves) * 100
                ErrorSexajeHembrasPorcentajeDiario = hembrasIniciales > 0 
                    ? (decimal)errorSexaje / hembrasIniciales * 100 
                    : 0,
                // porc_error_acumulado = porc_error_acumulado_anterior + porc_error_diario
                ErrorSexajeHembrasPorcentajeAcumulado = porcErrorSexajeAcumuladaAnterior + (hembrasIniciales > 0 
                    ? (decimal)errorSexaje / hembrasIniciales * 100 
                    : 0),
                // DESCARTE (Selección + Error Sexaje)
                DescarteHembras = descarteDiaria,
                DescarteHembrasAcumulado = descarteAcumulada,
                // FÓRMULA EXCEL: porc_descarte_diario = (descarte_diaria / total_inicial_aves) * 100
                DescarteHembrasPorcentajeDiario = porcDescarteDiario,
                // FÓRMULA EXCEL: porc_descarte_acumulada = porc_descarte_acumulada_anterior + porc_descarte_diario
                DescarteHembrasPorcentajeAcumulado = porcDescarteAcumulada,
                // FÓRMULA EXCEL: consumo_diario = consumo_semanal / 40 (en bultos)
                // Guardamos consumo en kg (consumoKg), pero el cálculo de gramos/ave usa la fórmula Excel
                ConsumoKgHembras = consumoKg,
                // FÓRMULA EXCEL: consumo_acumulado = consumo_acumulado_anterior + consumo_semanal
                ConsumoKgHembrasAcumulado = consumoAcumulado,
                // FÓRMULA EXCEL: consumo_por_ave = (consumo_diario * 40000) / aves_vivas
                ConsumoGramosPorAveHembras = consumoGramosPorAve,
                PesoPromedioHembras = pesoActual,
                UniformidadHembras = (decimal?)(seg.UniformidadH),
                CoeficienteVariacionHembras = (decimal?)(seg.CvH),
                GananciaPesoHembras = gananciaPeso,
                KcalAlHembras = kcalAl,
                ProtAlHembras = protAl,
                KcalAveHembras = kcalAve,
                ProtAveHembras = protAve,
                IngresosAlimentoKilos = ingresosAlimento,
                TrasladosAlimentoKilos = trasladosAlimento,
                Observaciones = seg.Observaciones
            };
            
            // Actualizar valores acumulados para la siguiente iteración
            porcMortalidadAcumuladaAnterior = porcMortalidadAcumulada;
            porcSeleccionAcumuladaAnterior = porcSeleccionAcumulada;
            porcDescarteAcumuladaAnterior = porcDescarteAcumulada;
            porcErrorSexajeAcumuladaAnterior = dto.ErrorSexajeHembrasPorcentajeAcumulado;
            consumoAcumuladoAnterior = consumoAcumulado;
            
            if (pesoActual.HasValue)
                pesoAnterior = pesoActual;
            
            datosHembras.Add(dto);
        }
        
        return datosHembras;
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte diario de hembras para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Genera reporte técnico de Levante con estructura de tabs
    /// Incluye datos diarios separados (machos y hembras) y datos semanales completos
    /// </summary>
    public async Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
        int lotePosturaLevanteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
        {
            var lpl = await _ctx.LotePosturaLevante
                .AsNoTracking()
                .Include(l => l.Farm)
                .Include(l => l.Nucleo)
                .FirstOrDefaultAsync(l => l.LotePosturaLevanteId == lotePosturaLevanteId && l.CompanyId == _currentUser.CompanyId, ct);

            if (lpl == null)
                throw new InvalidOperationException($"Lote Postura Levante con ID {lotePosturaLevanteId} no encontrado");

            // Determinar lista de lotes a procesar (consolidado o solo el actual)
            List<LotePosturaLevante> lotesAProcesar;
            if (consolidarSublotes)
            {
                lotesAProcesar = await ObtenerSublotesLevantePorLoteBaseAsync(lotePosturaLevanteId, ct);
                if (!lotesAProcesar.Any())
                {
                    lotesAProcesar = new List<LotePosturaLevante> { lpl };
                }
            }
            else
            {
                lotesAProcesar = new List<LotePosturaLevante> { lpl };
            }

            // Generar datos diarios consolidados (machos y hembras)
            var todosDatosDiariosMachos = new List<ReporteTecnicoDiarioMachosDto>();
            var todosDatosDiariosHembras = new List<ReporteTecnicoDiarioHembrasDto>();

            foreach (var lote in lotesAProcesar)
            {
                var datosMachos = await GenerarReporteDiarioMachosAsync(lote.LotePosturaLevanteId ?? 0, fechaInicio, fechaFin, ct);
                var datosHembras = await GenerarReporteDiarioHembrasAsync(lote.LotePosturaLevanteId ?? 0, fechaInicio, fechaFin, ct);

                todosDatosDiariosMachos.AddRange(datosMachos);
                todosDatosDiariosHembras.AddRange(datosHembras);
            }

            // Consolidar datos diarios por fecha si es necesario (sumando valores)
            var datosDiariosMachosFinales = consolidarSublotes
                ? ConsolidarDatosDiariosMachos(todosDatosDiariosMachos)
                : todosDatosDiariosMachos;

            var datosDiariosHembrasFinales = consolidarSublotes
                ? ConsolidarDatosDiariosHembras(todosDatosDiariosHembras)
                : todosDatosDiariosHembras;

            // Generar reporte semanal consolidado
            var reporteCompleto = await GenerarReporteLevanteCompletoAsync(lotePosturaLevanteId, consolidarSublotes, ct);

            var infoLote = MapearInformacionLoteFromLPL(lpl);
            var sublote = ExtraerSublote(lpl.LoteNombre);
            infoLote.Sublote = consolidarSublotes ? null : sublote;
            infoLote.Etapa = "LEVANTE";

            return new ReporteTecnicoLevanteConTabsDto
            {
                InformacionLote = infoLote,
                DatosDiariosMachos = datosDiariosMachosFinales.OrderBy(d => d.Fecha).ToList(),
                DatosDiariosHembras = datosDiariosHembrasFinales.OrderBy(d => d.Fecha).ToList(),
                DatosSemanales = reporteCompleto.DatosSemanales,
                EsConsolidado = consolidarSublotes,
                SublotesIncluidos = reporteCompleto.SublotesIncluidos
            };
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte con tabs para lote levante {lotePosturaLevanteId}: {ex.Message}", ex);
        }
    }

    // -------------------------------------------------------------------------
    // ObtenerReporteLevanteAsync
    // Navega: lote_postura_base → lotes → lote_postura_levante → seguimiento_diario
    // -------------------------------------------------------------------------
    public async Task<ReporteTecnicoLevanteCompletoDto> ObtenerReporteLevanteAsync(
        ObtenerReporteLevanteRequestDto request,
        CancellationToken ct = default)
    {
        // --- 1. Validar existencia del LotePosturaBase ---
        _ = await _ctx.LotePosturaBases
            .AsNoTracking()
            .FirstOrDefaultAsync(
                lpb => lpb.LotePosturaBaseId == request.LotePosturaBaseId
                    && lpb.CompanyId == _currentUser.CompanyId, ct)
            ?? throw new InvalidOperationException(
                $"LotePosturaBase con ID {request.LotePosturaBaseId} no encontrado.");

        // --- 2. Navegar a lotes intermedios ---
        var lotesIds = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePosturaBaseId == request.LotePosturaBaseId
                     && l.CompanyId == _currentUser.CompanyId
                     && l.DeletedAt == null)
            .Select(l => (int?)l.LoteId)
            .ToListAsync(ct);

        if (!lotesIds.Any())
            throw new InvalidOperationException(
                $"No hay lotes asociados al LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 3. Obtener lotes levante ---
        var lotesLevanteQuery = _ctx.LotePosturaLevante
            .AsNoTracking()
            .Include(lpl => lpl.Farm)
            .Include(lpl => lpl.Nucleo)
            .Where(lpl => lotesIds.Contains(lpl.LoteId)
                       && lpl.CompanyId == _currentUser.CompanyId
                       && lpl.DeletedAt == null
                       && lpl.Etapa == "Levante");

        if (request.LoteLevanteId.HasValue)
            lotesLevanteQuery = lotesLevanteQuery
                .Where(lpl => lpl.LotePosturaLevanteId == request.LoteLevanteId.Value);

        var lotesLevante = await lotesLevanteQuery
            .OrderBy(lpl => lpl.FechaEncaset)
            .ThenBy(lpl => lpl.LoteNombre)
            .ToListAsync(ct);

        if (!lotesLevante.Any())
            throw new InvalidOperationException(
                $"No se encontraron lotes levante para LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 4. Recopilar seguimientos + acumular aves iniciales (Opción A) ---
        // Clave: lotePosturaLevanteId → (seguimientos, fechaEncaset)
        var seguimientosPorLpl = new List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)>();
        var sublotesIncluidos = new List<string>();
        var avesHInicialesTotal = 0;
        var avesMInicialesTotal = 0;

        foreach (var lpl in lotesLevante)
        {
            if (!lpl.LotePosturaLevanteId.HasValue || !lpl.FechaEncaset.HasValue)
                continue;

            var segs = await ObtenerSeguimientosLevantePorLPLAsync(lpl.LotePosturaLevanteId.Value, ct);

            // Restricción: solo semanas 1-25
            segs = segs.Where(s =>
            {
                var dias = CalcularEdadDias(lpl.FechaEncaset.Value, s.FechaRegistro);
                return CalcularEdadSemanas(dias) <= 25;
            }).ToList();

            // Filtro de rango de fechas (opcional)
            if (request.FechaInicio.HasValue)
                segs = segs.Where(s => s.FechaRegistro >= request.FechaInicio.Value).ToList();
            if (request.FechaFin.HasValue)
                segs = segs.Where(s => s.FechaRegistro <= request.FechaFin.Value).ToList();

            seguimientosPorLpl.Add((lpl, segs));
            sublotesIncluidos.Add(lpl.LoteNombre);

            // Denominador para porcentajes: aves vivas al inicio de levante (después de mortalidad en caja)
            avesHInicialesTotal += lpl.AvesHInicial ?? lpl.HembrasL ?? 0;
            avesMInicialesTotal += lpl.AvesMInicial ?? lpl.MachosL ?? 0;
        }

        if (!seguimientosPorLpl.Any())
            throw new InvalidOperationException(
                $"No hay seguimientos de levante para LotePosturaBase {request.LotePosturaBaseId}.");

        // --- 5. Cargar guía genética desde ProduccionAvicolaRaw (mismo patrón que GenerarReporteLevanteCompletoAsync) ---
        var primerLpl = lotesLevante.First();
        var guiasRaw = new Dictionary<int, Domain.Entities.ProduccionAvicolaRaw>();
        var guiasGenetica = new Dictionary<int, GuiaGeneticaDto>();

        if (!string.IsNullOrWhiteSpace(primerLpl.Raza) && primerLpl.AnoTablaGenetica.HasValue)
        {
            try
            {
                var razaNorm = primerLpl.Raza.Trim().ToLower();
                var ano = primerLpl.AnoTablaGenetica.Value.ToString();

                var guiasRawList = await _ctx.ProduccionAvicolaRaw
                    .AsNoTracking()
                    .Where(p =>
                        p.Raza != null && p.AnioGuia != null &&
                        EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                        p.AnioGuia.Trim() == ano &&
                        p.CompanyId == _currentUser.CompanyId &&
                        p.DeletedAt == null)
                    .ToListAsync(ct);

                foreach (var guia in guiasRawList)
                {
                    if (int.TryParse(guia.Edad?.Trim().Replace(",", ".").Split('.')[0], out var edad)
                        && edad >= 1 && edad <= 25)
                        guiasRaw[edad] = guia;
                }

                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaRangoAsync(
                    primerLpl.Raza, primerLpl.AnoTablaGenetica.Value, edadDesde: 1, edadHasta: 25);
                guiasGenetica = guias.ToDictionary(g => g.Edad, g => g);
            }
            catch { /* Si no hay guía genética, los campos GUIA quedan null */ }
        }

        var infoLote = MapearInformacionLoteFromLPL(lotesLevante.First());
        infoLote.Etapa = "LEVANTE";
        var esConsolidado = !request.LoteLevanteId.HasValue && lotesLevante.Count > 1;

        // --- 6. Ramificar según periodicidad ---
        if (request.FiltroPeriodicidad.Equals("Semanal", StringComparison.OrdinalIgnoreCase))
        {
            var datosSemanales = GenerarSemanalesConsolidados(
                seguimientosPorLpl, avesHInicialesTotal, avesMInicialesTotal,
                guiasGenetica, guiasRaw);

            return new ReporteTecnicoLevanteCompletoDto
            {
                InformacionLote = infoLote,
                DatosSemanales = datosSemanales,
                DatosDiarios = new List<ReporteTecnicoDiarioLevanteDto>(),
                EsConsolidado = esConsolidado,
                SublotesIncluidos = sublotesIncluidos
            };
        }
        else // Diario
        {
            var datosDiarios = GenerarDiariosConsolidados(
                seguimientosPorLpl, avesHInicialesTotal, avesMInicialesTotal);

            return new ReporteTecnicoLevanteCompletoDto
            {
                InformacionLote = infoLote,
                DatosSemanales = new List<ReporteTecnicoLevanteSemanalDto>(),
                DatosDiarios = datosDiarios,
                EsConsolidado = esConsolidado,
                SublotesIncluidos = sublotesIncluidos
            };
        }
    }

    /// <summary>
    /// Construye la lista de datos DIARIOS consolidados desde múltiples lotes levante.
    /// Agrupa por fecha de calendario. Porcentajes recalculados sobre avesH/M iniciales totales.
    /// </summary>
    private List<ReporteTecnicoDiarioLevanteDto> GenerarDiariosConsolidados(
        List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)> fuentes,
        int avesHInicialesTotal,
        int avesMInicialesTotal)
    {
        // Aplanar todos los seguimientos con su lpl de origen para calcular edad
        var filasBruto = fuentes
            .SelectMany(f => f.Segs.Select(s => (f.Lpl, Seg: s)))
            .OrderBy(x => x.Seg.FechaRegistro)
            .ToList();

        if (!filasBruto.Any())
            return new List<ReporteTecnicoDiarioLevanteDto>();

        // Agrupar por fecha de calendario (consolidado multi-lote)
        var porFecha = filasBruto
            .GroupBy(x => x.Seg.FechaRegistro.Date)
            .OrderBy(g => g.Key)
            .ToList();

        var resultado = new List<ReporteTecnicoDiarioLevanteDto>();
        int acMortH = 0, acMortM = 0;
        double acConsH = 0, acConsM = 0;
        int saldoH = avesHInicialesTotal;
        int saldoM = avesMInicialesTotal;

        foreach (var grupo in porFecha)
        {
            var items = grupo.ToList();

            // Edad calculada desde el primer lpl del grupo (referencia temporal)
            var lplRef = items.First().Lpl;
            var edadDias = lplRef.FechaEncaset.HasValue
                ? CalcularEdadDias(lplRef.FechaEncaset.Value, grupo.Key)
                : 0;
            var edadSemanas = CalcularEdadSemanas(edadDias);

            // Sumas del día
            var mortH = items.Sum(x => x.Seg.MortalidadHembras);
            var mortM = items.Sum(x => x.Seg.MortalidadMachos);
            var selH = items.Sum(x => Math.Max(0, x.Seg.SelH));
            var selM = items.Sum(x => Math.Max(0, x.Seg.SelM));
            var errH = items.Sum(x => x.Seg.ErrorSexajeHembras);
            var errM = items.Sum(x => x.Seg.ErrorSexajeMachos);
            var consH = items.Sum(x => x.Seg.ConsumoKgHembras);
            var consM = items.Sum(x => x.Seg.ConsumoKgMachos ?? 0);

            // Promedios ponderados (promedio simple; lotes con datos null se excluyen)
            var pesoH = items.Where(x => x.Seg.PesoPromH.HasValue).Select(x => x.Seg.PesoPromH!.Value).DefaultIfEmpty().Average();
            var pesoM = items.Where(x => x.Seg.PesoPromM.HasValue).Select(x => x.Seg.PesoPromM!.Value).DefaultIfEmpty().Average();
            var unifH = items.Where(x => x.Seg.UniformidadH.HasValue).Select(x => x.Seg.UniformidadH!.Value).DefaultIfEmpty().Average();
            var unifM = items.Where(x => x.Seg.UniformidadM.HasValue).Select(x => x.Seg.UniformidadM!.Value).DefaultIfEmpty().Average();
            var cvH = items.Where(x => x.Seg.CvH.HasValue).Select(x => x.Seg.CvH!.Value).DefaultIfEmpty().Average();
            var cvM = items.Where(x => x.Seg.CvM.HasValue).Select(x => x.Seg.CvM!.Value).DefaultIfEmpty().Average();
            var kcalAlH = items.Where(x => x.Seg.KcalAlH.HasValue).Select(x => x.Seg.KcalAlH!.Value).DefaultIfEmpty().Average();
            var protAlH = items.Where(x => x.Seg.ProtAlH.HasValue).Select(x => x.Seg.ProtAlH!.Value).DefaultIfEmpty().Average();
            var kcalAveH = items.Where(x => x.Seg.KcalAveH.HasValue).Select(x => x.Seg.KcalAveH!.Value).DefaultIfEmpty().Average();
            var protAveH = items.Where(x => x.Seg.ProtAveH.HasValue).Select(x => x.Seg.ProtAveH!.Value).DefaultIfEmpty().Average();

            // Actualizar saldos (antes de mortalidad del día = denominador porcentaje diario)
            var saldoHAntesMort = saldoH;
            var saldoMAntesMort = saldoM;
            saldoH -= mortH + selH;
            saldoM -= mortM + selM;

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acConsH += consH;
            acConsM += consM;

            // Recalcular porcentajes sobre total unificado (Opción A)
            var porcMortH = saldoHAntesMort > 0 ? (double)mortH / saldoHAntesMort * 100 : 0;
            var porcMortM = saldoMAntesMort > 0 ? (double)mortM / saldoMAntesMort * 100 : 0;
            var porcMortHAc = avesHInicialesTotal > 0 ? (double)acMortH / avesHInicialesTotal * 100 : 0;
            var porcMortMAc = avesMInicialesTotal > 0 ? (double)acMortM / avesMInicialesTotal * 100 : 0;

            resultado.Add(new ReporteTecnicoDiarioLevanteDto
            {
                Fecha = grupo.Key,
                EdadDias = edadDias,
                EdadSemanas = edadSemanas,
                SaldoHembras = Math.Max(0, saldoH),
                MortalidadHembras = mortH,
                MortalidadHembrasAcumulada = acMortH,
                PorcMortH = Math.Round(porcMortH, 4),
                PorcMortHAcumulado = Math.Round(porcMortHAc, 4),
                SelH = selH,
                ErrorSexajeH = errH,
                ConsumoKgH = Math.Round(consH, 3),
                ConsumoKgHAcumulado = Math.Round(acConsH, 3),
                PesoPromH = pesoH > 0 ? pesoH : null,
                UniformidadH = unifH > 0 ? unifH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,
                KcalAveH = kcalAveH > 0 ? kcalAveH : null,
                ProtAveH = protAveH > 0 ? protAveH : null,
                SaldoMachos = Math.Max(0, saldoM),
                MortalidadMachos = mortM,
                MortalidadMachosAcumulada = acMortM,
                PorcMortM = Math.Round(porcMortM, 4),
                PorcMortMAcumulado = Math.Round(porcMortMAc, 4),
                SelM = selM,
                ErrorSexajeM = errM,
                ConsumoKgM = Math.Round(consM, 3),
                ConsumoKgMAcumulado = Math.Round(acConsM, 3),
                PesoPromM = pesoM > 0 ? pesoM : null,
                UniformidadM = unifM > 0 ? unifM : null,
                CvM = cvM > 0 ? cvM : null,
                Observaciones = items.FirstOrDefault(x => x.Seg.Observaciones != null).Seg?.Observaciones
            });
        }

        return resultado;
    }

    /// <summary>
    /// Construye la lista de datos SEMANALES consolidados (semanas de levante 1-25).
    /// Semanas calculadas relativas a cada lote. Porcentajes recalculados sobre avesH/M iniciales totales.
    /// </summary>
    private List<ReporteTecnicoLevanteSemanalDto> GenerarSemanalesConsolidados(
        List<(LotePosturaLevante Lpl, List<SegLevanteParaReporte> Segs)> fuentes,
        int avesHInicialesTotal,
        int avesMInicialesTotal,
        Dictionary<int, GuiaGeneticaDto>? guiasGenetica = null,
        Dictionary<int, Domain.Entities.ProduccionAvicolaRaw>? guiasRaw = null)
    {
        static double ParseGuiaV(string? value) =>
            double.TryParse(value?.Trim().Replace(",", "."), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var r) ? r : 0;

        // Variables acumuladas a lo largo de las semanas (persisten entre iteraciones)
        int acMortH = 0, acSelH = 0, acErrH = 0;
        int acMortM = 0, acSelM = 0, acErrM = 0;
        double acConsH = 0, acConsM = 0;
        double acKcalSemH = 0;
        double acProtSemH = 0;
        double? consAcGrHAnterior = null;
        double? consAcGrMAnterior = null;
        double? consAcGrHGUIAAnterior = null;
        double? consAcGrMGUIAAnterior = null;

        var resultado = new List<ReporteTecnicoLevanteSemanalDto>();

        for (int semana = 1; semana <= 25; semana++)
        {
            // Recopilar todos los registros de esta semana de todos los lotes
            var registrosSemana = fuentes
                .SelectMany(f =>
                    f.Segs
                     .Where(s =>
                     {
                         if (!f.Lpl.FechaEncaset.HasValue) return false;
                         var dias = CalcularEdadDias(f.Lpl.FechaEncaset.Value, s.FechaRegistro);
                         return CalcularEdadSemanas(dias) == semana;
                     })
                     .Select(s => (f.Lpl, Seg: s))
                )
                .ToList();

            if (!registrosSemana.Any())
                continue;

            // Cruce genético con ProduccionAvicolaRaw (Tarea 1.6)
            var guiaGenetica = guiasGenetica != null && guiasGenetica.TryGetValue(semana, out var gg) ? gg : null;
            var guiaRaw = guiasRaw != null && guiasRaw.TryGetValue(semana, out var gr) ? gr : null;

            // Calcular valores de la semana (sumas)
            var mortH = registrosSemana.Sum(x => x.Seg.MortalidadHembras);
            var mortM = registrosSemana.Sum(x => x.Seg.MortalidadMachos);
            var selH = registrosSemana.Sum(x => Math.Max(0, x.Seg.SelH));
            var selM = registrosSemana.Sum(x => Math.Max(0, x.Seg.SelM));
            var errH = registrosSemana.Sum(x => x.Seg.ErrorSexajeHembras);
            var errM = registrosSemana.Sum(x => x.Seg.ErrorSexajeMachos);
            var consKgH = registrosSemana.Sum(x => x.Seg.ConsumoKgHembras);
            var consKgM = registrosSemana.Sum(x => x.Seg.ConsumoKgMachos ?? 0);

            // Promedios ponderados para pesos y uniformidades
            var pesoH = registrosSemana.Where(x => x.Seg.PesoPromH.HasValue)
                                       .Select(x => x.Seg.PesoPromH!.Value)
                                       .DefaultIfEmpty().Average();
            var pesoM = registrosSemana.Where(x => x.Seg.PesoPromM.HasValue)
                                       .Select(x => x.Seg.PesoPromM!.Value)
                                       .DefaultIfEmpty().Average();
            var unifH = registrosSemana.Where(x => x.Seg.UniformidadH.HasValue)
                                       .Select(x => x.Seg.UniformidadH!.Value)
                                       .DefaultIfEmpty().Average();
            var unifM = registrosSemana.Where(x => x.Seg.UniformidadM.HasValue)
                                       .Select(x => x.Seg.UniformidadM!.Value)
                                       .DefaultIfEmpty().Average();
            var cvH = registrosSemana.Where(x => x.Seg.CvH.HasValue)
                                     .Select(x => x.Seg.CvH!.Value)
                                     .DefaultIfEmpty().Average();
            var cvM = registrosSemana.Where(x => x.Seg.CvM.HasValue)
                                     .Select(x => x.Seg.CvM!.Value)
                                     .DefaultIfEmpty().Average();
            var kcalAlH = registrosSemana.Where(x => x.Seg.KcalAlH.HasValue)
                                         .Select(x => x.Seg.KcalAlH!.Value)
                                         .DefaultIfEmpty().Average();
            var protAlH = registrosSemana.Where(x => x.Seg.ProtAlH.HasValue)
                                         .Select(x => x.Seg.ProtAlH!.Value)
                                         .DefaultIfEmpty().Average();
            var kcalAveH = registrosSemana.Where(x => x.Seg.KcalAveH.HasValue)
                                          .Select(x => x.Seg.KcalAveH!.Value)
                                          .DefaultIfEmpty().Average();
            var protAveH = registrosSemana.Where(x => x.Seg.ProtAveH.HasValue)
                                          .Select(x => x.Seg.ProtAveH!.Value)
                                          .DefaultIfEmpty().Average();

            // Fecha de referencia: último día registrado en esta semana
            var fechaSemana = registrosSemana.Max(x => x.Seg.FechaRegistro);
            var lplRef = registrosSemana.First().Lpl;
            var edadDias = lplRef.FechaEncaset.HasValue
                ? CalcularEdadDias(lplRef.FechaEncaset.Value, fechaSemana)
                : semana * 7;

            // Actualizar acumulados
            acMortH += mortH;
            acMortM += mortM;
            acSelH += selH;
            acSelM += selM;
            acErrH += errH;
            acErrM += errM;
            acConsH += consKgH;
            acConsM += consKgM;

            // Saldos actuales (aves vivas al cierre de la semana)
            var hembraActual = avesHInicialesTotal - acMortH - acSelH;
            var machoActual = avesMInicialesTotal - acMortM - acSelM;

            // ---- Recálculo de porcentajes sobre total unificado (Opción A) ----
            var porcMortH = avesHInicialesTotal > 0
                ? (double)mortH / avesHInicialesTotal * 100 : 0;
            var porcMortM = avesMInicialesTotal > 0
                ? (double)mortM / avesMInicialesTotal * 100 : 0;
            var porcSelH = avesHInicialesTotal > 0
                ? (double)selH / avesHInicialesTotal * 100 : 0;
            var porcSelM = avesMInicialesTotal > 0
                ? (double)selM / avesMInicialesTotal * 100 : 0;
            var porcErrH = avesHInicialesTotal > 0
                ? (double)errH / avesHInicialesTotal * 100 : 0;
            var porcErrM = avesMInicialesTotal > 0
                ? (double)errM / avesMInicialesTotal * 100 : 0;

            // Relación machos/hembras
            var relMH = hembraActual > 0
                ? (double)machoActual / hembraActual * 100 : 0;

            // Consumo acumulado en g/ave
            var consAcGrH = avesHInicialesTotal > 0
                ? acConsH * 1000 / avesHInicialesTotal : 0;
            var consAcGrM = avesMInicialesTotal > 0
                ? acConsM * 1000 / avesMInicialesTotal : 0;

            // g/ave/día semana
            var grAveDiaH = hembraActual > 0 ? consKgH * 1000 / hembraActual / 7 : 0;
            var grAveDiaM = machoActual > 0 ? consKgM * 1000 / machoActual / 7 : 0;

            // Incremento de consumo acumulado vs semana anterior
            var incrConsH = consAcGrHAnterior.HasValue
                ? consAcGrH - consAcGrHAnterior.Value : 0;
            var incrConsM = consAcGrMAnterior.HasValue
                ? consAcGrM - consAcGrMAnterior.Value : 0;

            consAcGrHAnterior = consAcGrH;
            consAcGrMAnterior = consAcGrM;

            // Campos GUIA desde ProduccionAvicolaRaw (consumo acumulado en g/ave)
            var consAcGrHGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.ConsAcH) : null;
            var consAcGrMGUIA = guiaRaw != null ? (double?)ParseGuiaV(guiaRaw.ConsAcM) : null;
            var incrConsHGUIA = consAcGrHGUIAAnterior.HasValue && consAcGrHGUIA.HasValue
                ? consAcGrHGUIA.Value - consAcGrHGUIAAnterior.Value
                : (semana == 1 ? consAcGrHGUIA : null);
            var incrConsMGUIA = consAcGrMGUIAAnterior.HasValue && consAcGrMGUIA.HasValue
                ? consAcGrMGUIA.Value - consAcGrMGUIAAnterior.Value
                : (semana == 1 ? consAcGrMGUIA : null);
            if (consAcGrHGUIA.HasValue) consAcGrHGUIAAnterior = consAcGrHGUIA.Value;
            if (consAcGrMGUIA.HasValue) consAcGrMGUIAAnterior = consAcGrMGUIA.Value;

            // Métricas nutricionales acumuladas (machos no tienen KcalAl en el esquema actual)
            var kcalSemH = kcalAlH * consKgH;
            var protSemH = protAlH > 0 ? (protAlH / 100) * consKgH : 0;
            acKcalSemH += kcalSemH;
            acProtSemH += protSemH;

            // Retiros acumulados (M+S+E)
            var msEH = mortH + selH + errH;
            var msEM = mortM + selM + errM;
            var retAcH = acMortH + acSelH + acErrH;
            var retAcM = acMortM + acSelM + acErrM;
            var porcRetH = avesHInicialesTotal > 0
                ? (double)retAcH / avesHInicialesTotal * 100 : 0;
            var porcRetM = avesMInicialesTotal > 0
                ? (double)retAcM / avesMInicialesTotal * 100 : 0;

            // Semana del año (ISO)
            var semAno = System.Globalization.ISOWeek.GetWeekOfYear(fechaSemana);

            resultado.Add(new ReporteTecnicoLevanteSemanalDto
            {
                Semana = semana,
                Edad = edadDias,
                SemAno = semAno,
                Fecha = fechaSemana,

                // Datos de encasetamiento
                HembraIni = avesHInicialesTotal,
                MachoIni = avesMInicialesTotal,

                // Raza y línea del primer lote de referencia
                Raza = lplRef.Raza,
                AnoG = lplRef.AnoTablaGenetica,
                Granja = lplRef.Farm?.Name,
                Regional = lplRef.Regional,
                CodGuia = lplRef.CodigoGuiaGenetica,
                NucleoL = lplRef.NucleoId,

                // Hembras
                Hembra = Math.Max(0, hembraActual),
                MortH = mortH,
                SelH = selH,
                ErrorH = errH,
                ConsKgH = Math.Round(consKgH, 3),
                PesoH = pesoH > 0 ? pesoH : null,
                UniformH = unifH > 0 ? unifH : null,
                CvH = cvH > 0 ? cvH : null,
                KcalAlH = kcalAlH > 0 ? kcalAlH : null,
                ProtAlH = protAlH > 0 ? protAlH : null,
                KcalAveH = kcalAveH > 0 ? kcalAveH : null,
                ProtAveH = protAveH > 0 ? protAveH : null,

                // Machos
                SaldoMacho = Math.Max(0, machoActual),
                MortM = mortM,
                SelM = selM,
                ErrorM = errM,
                ConsKgM = Math.Round(consKgM, 3),
                PesoM = pesoM > 0 ? pesoM : null,
                UniformM = unifM > 0 ? unifM : null,
                CvM = cvM > 0 ? cvM : null,

                // ---- Porcentajes recalculados sobre total unificado ----
                PorcMortH = Math.Round(porcMortH, 4),
                ACMortH = acMortH,
                PorcSelH = Math.Round(porcSelH, 4),
                ACSelH = acSelH,
                PorcErrH = Math.Round(porcErrH, 4),
                ACErrH = acErrH,
                MSEH = msEH,
                RetAcH = retAcH,
                PorcRetiroH = Math.Round(porcRetH, 4),

                PorcMortM = Math.Round(porcMortM, 4),
                ACMortM = acMortM,
                PorcSelM = Math.Round(porcSelM, 4),
                ACSelM = acSelM,
                PorcErrM = Math.Round(porcErrM, 4),
                ACErrM = acErrM,
                MSEM = msEM,
                RetAcM = retAcM,
                PorcRetAcM = Math.Round(porcRetM, 4),

                RelMH = Math.Round(relMH, 4),

                // Consumos acumulados
                AcConsH = Math.Round(acConsH, 3),
                ConsAcGrH = Math.Round(consAcGrH, 2),
                GrAveDiaH = Math.Round(grAveDiaH, 2),
                IncrConsH = Math.Round(incrConsH, 2),

                AcConsM = Math.Round(acConsM, 3),
                ConsAcGrM = Math.Round(consAcGrM, 2),
                GrAveDiaM = Math.Round(grAveDiaM, 2),
                IncrConsM = Math.Round(incrConsM, 2),

                // Nutricional
                KcalSemH = Math.Round(kcalSemH, 2),
                KcalSemAcH = Math.Round(acKcalSemH, 2),
                ProtSemH = Math.Round(protSemH, 4),
                ProtSemAcH = Math.Round(acProtSemH, 4),

                // ---- Cruce Genético (Tarea 1.6): campos GUIA desde ProduccionAvicolaRaw ----
                PorcMortHGUIA = guiaGenetica?.MortalidadHembras,
                DifMortH = guiaGenetica != null ? Math.Round(porcMortH - guiaGenetica.MortalidadHembras, 4) : null,
                RetiroHGUIA = guiaGenetica?.RetiroAcumuladoHembras,
                ConsAcGrHGUIA = consAcGrHGUIA.HasValue ? Math.Round(consAcGrHGUIA.Value, 2) : null,
                GrAveDiaGUIAH = guiaGenetica?.ConsumoHembras,
                IncrConsHGUIA = incrConsHGUIA.HasValue ? Math.Round(incrConsHGUIA.Value, 2) : null,
                PorcDifConsH = consAcGrHGUIA.HasValue && consAcGrHGUIA.Value > 0
                    ? Math.Round((consAcGrH - consAcGrHGUIA.Value) / consAcGrHGUIA.Value * 100, 2) : null,
                PesoHGUIA = guiaGenetica != null ? guiaGenetica.PesoHembras / 1000.0 : null,
                PorcDifPesoH = guiaGenetica != null && guiaGenetica.PesoHembras > 0 && pesoH > 0
                    ? Math.Round((pesoH - guiaGenetica.PesoHembras / 1000.0) / (guiaGenetica.PesoHembras / 1000.0) * 100, 2) : null,
                UnifHGUIA = guiaGenetica?.Uniformidad,

                PorcMortMGUIA = guiaGenetica?.MortalidadMachos,
                DifMortM = guiaGenetica != null ? Math.Round(porcMortM - guiaGenetica.MortalidadMachos, 4) : null,
                RetiroMGUIA = guiaGenetica?.RetiroAcumuladoMachos,
                ConsAcGrMGUIA = consAcGrMGUIA.HasValue ? Math.Round(consAcGrMGUIA.Value, 2) : null,
                GrAveDiaMGUIA = guiaGenetica?.ConsumoMachos,
                IncrConsMGUIA = incrConsMGUIA.HasValue ? Math.Round(incrConsMGUIA.Value, 2) : null,
                DifConsM = consAcGrMGUIA.HasValue
                    ? Math.Round(consAcGrM - consAcGrMGUIA.Value, 2) : null,
                PesoMGUIA = guiaGenetica != null ? guiaGenetica.PesoMachos / 1000.0 : null,
                PorcDifPesoM = guiaGenetica != null && guiaGenetica.PesoMachos > 0 && pesoM > 0
                    ? Math.Round((pesoM - guiaGenetica.PesoMachos / 1000.0) / (guiaGenetica.PesoMachos / 1000.0) * 100, 2) : null,
                UnifMGUIA = guiaGenetica?.Uniformidad,

                DifConsAcH = consAcGrHGUIA.HasValue
                    ? Math.Round(acConsH - (consAcGrHGUIA.Value * avesHInicialesTotal / 1000.0), 3) : null,
                DifConsAcM = consAcGrMGUIA.HasValue
                    ? Math.Round(acConsM - (consAcGrMGUIA.Value * avesMInicialesTotal / 1000.0), 3) : null,
            });
        }

        return resultado;
    }

    #endregion
}

