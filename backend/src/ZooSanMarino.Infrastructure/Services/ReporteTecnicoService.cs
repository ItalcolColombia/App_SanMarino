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

    #region Métodos Privados

    private async Task<bool> EsLoteEnLevanteAsync(int loteId, CancellationToken ct)
    {
        // Obtener información del lote para calcular la edad
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null || !lote.FechaEncaset.HasValue)
        {
            // Si no hay fecha de encaset, verificar por registros
            var tieneRegistros = await _ctx.SeguimientoLoteLevante
                .AsNoTracking()
                .AnyAsync(s => s.LoteId == loteId, ct);
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
            .AnyAsync(s => s.LoteId == loteId.ToString(), ct);
        
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
        // necesitamos TODOS los registros desde el inicio, no solo los filtrados
        var queryTodos = _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .OrderBy(s => s.FechaRegistro);

        // Obtener todos los registros para cálculos acumulados
        var todosSeguimientos = await queryTodos.ToListAsync(ct);

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
        var query = _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteId);

        if (fechaInicio.HasValue)
            query = query.Where(s => s.Fecha >= fechaInicio.Value);

        if (fechaFin.HasValue)
            query = query.Where(s => s.Fecha <= fechaFin.Value);

        var seguimientos = await query
            .OrderBy(s => s.Fecha)
            .ToListAsync(ct);

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        // Obtener lote para información inicial
        var loteIdInt = int.TryParse(loteId, out var id) ? id : 0;
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteIdInt, ct);

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var produccionLote = await _ctx.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId == loteId, ct);

        var avesIniciales = produccionLote != null 
            ? produccionLote.AvesInicialesH + produccionLote.AvesInicialesM
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
        int loteId,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lote == null)
            throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");

        if (!lote.FechaEncaset.HasValue)
            throw new InvalidOperationException($"El lote {loteId} no tiene fecha de encaset");

        var infoLote = MapearInformacionLote(lote);
        var sublote = ExtraerSublote(lote.LoteNombre);
        infoLote.Sublote = sublote;

        // Obtener todos los registros de seguimiento de levante (solo semanas 1-25)
        var todosSeguimientos = await _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .OrderBy(s => s.FechaRegistro)
            .ToListAsync(ct);

        // Filtrar solo semanas de levante (1-25)
        var seguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            return edadSemanas <= 25;
        }).ToList();

        // Obtener guía genética del lote (desde produccion_avicola_raw)
        // El lote tiene Raza y AnoTablaGenetica que se usan para buscar la guía
        Dictionary<int, Domain.Entities.ProduccionAvicolaRaw> guiasRaw = new();
        Dictionary<int, GuiaGeneticaDto> guiasGenetica = new();
        
        if (!string.IsNullOrWhiteSpace(lote.Raza) && lote.AnoTablaGenetica.HasValue)
        {
            try
            {
                var razaNorm = lote.Raza.Trim().ToLower();
                var ano = lote.AnoTablaGenetica.Value.ToString();
                
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
                    lote.Raza, 
                    lote.AnoTablaGenetica.Value, 
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

        // Obtener traslados del lote para verificar reducciones
        var traslados = await _ctx.Set<Domain.Entities.MovimientoAves>()
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteId && 
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
        var hembraIni = lote.HembrasL ?? 0;
        var machoIni = lote.MachosL ?? 0;

        // Variables acumuladas
        int acMortH = 0, acSelH = 0, acErrH = 0;
        int acMortM = 0, acSelM = 0, acErrM = 0;
        double acConsH = 0, acConsM = 0;
        double acKcalSemH = 0, acKcalSemM = 0;
        double acProtSemH = 0, acProtSemM = 0;
        double? consAcGrHAnterior = null;
        double? consAcGrMAnterior = null;

        for (int semana = 1; semana <= 25; semana++)
        {
            // Calcular rango de fechas para la semana
            var fechaInicioSemana = lote.FechaEncaset.Value.AddDays((semana - 1) * 7);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            // Obtener registros de esta semana
            var registrosSemana = seguimientos.Where(s =>
            {
                var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, s.FechaRegistro);
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
                // Identificación
                CodGuia = lote.CodigoGuiaGenetica, // Código de guía genética del lote
                IdLoteRAP = null, // Se puede agregar si existe en el lote
                Regional = lote.Regional,
                Granja = lote.Farm?.Name,
                Lote = lote.LoteNombre,
                Raza = lote.Raza,
                AnoG = lote.AnoTablaGenetica,
                HembraIni = hembraIni,
                MachoIni = machoIni,
                Traslado = null, // Se puede calcular desde traslados si es necesario
                NucleoL = lote.Nucleo?.NucleoNombre,
                Anon = null, // Se puede agregar si existe en el lote
                Edad = CalcularEdadDias(lote.FechaEncaset.Value, fechaInicioSemana),
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
                IncrConsHGUIA = null, // Se puede calcular si hay guía anterior
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
                IncrConsMGUIA = null, // Se puede calcular si hay guía anterior
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

            datosSemanales.Add(dto);
        }

        return new ReporteTecnicoLevanteCompletoDto
        {
            InformacionLote = infoLote,
            DatosSemanales = datosSemanales,
            EsConsolidado = consolidarSublotes,
            SublotesIncluidos = consolidarSublotes ? new List<string> { sublote ?? "Sin sublote" } : new List<string>()
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
    /// Genera reporte diario específico de MACHOS desde el seguimiento diario de levante
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioMachosDto>> GenerarReporteDiarioMachosAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            // Obtener lote para información inicial
            var lote = await _ctx.Lotes
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lote == null)
                throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");
            
            if (!lote.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote {loteId} no tiene fecha de encaset");
            
            var machosIniciales = lote.MachosL ?? 0;
            var granjaId = lote.GranjaId;
        
        // Obtener todos los registros de seguimiento (para cálculos acumulados correctos)
        var queryTodos = _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .OrderBy(s => s.FechaRegistro);
        
        var todosSeguimientos = await queryTodos.ToListAsync(ct);
        
        // Filtrar solo semanas de levante (1-25)
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
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
        var datosMachos = new List<ReporteTecnicoDiarioMachosDto>();
        decimal? pesoAnterior = null;
        
        // Variables para acumular porcentajes (según fórmulas Excel)
        decimal porcMortalidadAcumuladaAnterior = 0;
        decimal porcSeleccionAcumuladaAnterior = 0;
        decimal porcDescarteAcumuladaAnterior = 0;
        decimal porcErrorSexajeAcumuladaAnterior = 0;
        decimal consumoAcumuladoAnterior = 0;
        
        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            // Calcular acumulados hasta esta fecha (incluyendo todos los registros anteriores)
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            
            // Calcular saldo actual de machos (aves_vivas)
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
            throw new InvalidOperationException($"Error al generar reporte diario de machos para lote {loteId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Genera reporte diario específico de HEMBRAS desde el seguimiento diario de levante
    /// </summary>
    public async Task<List<ReporteTecnicoDiarioHembrasDto>> GenerarReporteDiarioHembrasAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        CancellationToken ct = default)
    {
        try
        {
            // Obtener lote para información inicial
            var lote = await _ctx.Lotes
                .AsNoTracking()
                .Include(l => l.Farm)
                .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);
            
            if (lote == null)
                throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");
            
            if (!lote.FechaEncaset.HasValue)
                throw new InvalidOperationException($"El lote {loteId} no tiene fecha de encaset");
            
            var hembrasIniciales = lote.HembrasL ?? 0;
            var granjaId = lote.GranjaId;
        
        // Obtener todos los registros de seguimiento (para cálculos acumulados correctos)
        var queryTodos = _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId)
            .OrderBy(s => s.FechaRegistro);
        
        var todosSeguimientos = await queryTodos.ToListAsync(ct);
        
        // Filtrar solo semanas de levante (1-25)
        todosSeguimientos = todosSeguimientos.Where(seg =>
        {
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
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
            var edadDias = CalcularEdadDias(lote.FechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);
            
            // Calcular acumulados hasta esta fecha (incluyendo todos los registros anteriores)
            var registrosHastaFecha = todosSeguimientos
                .Where(s => s.FechaRegistro <= seg.FechaRegistro)
                .ToList();
            
            // Calcular saldo actual de hembras (aves_vivas)
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
            throw new InvalidOperationException($"Error al generar reporte diario de hembras para lote {loteId}: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Genera reporte técnico de Levante con estructura de tabs
    /// Incluye datos diarios separados (machos y hembras) y datos semanales completos
    /// </summary>
    public async Task<ReporteTecnicoLevanteConTabsDto> GenerarReporteLevanteConTabsAsync(
        int loteId,
        DateTime? fechaInicio = null,
        DateTime? fechaFin = null,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        try
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
            infoLote.Etapa = "LEVANTE";
            
            // Generar datos para cada tab
            var datosDiariosMachos = await GenerarReporteDiarioMachosAsync(loteId, fechaInicio, fechaFin, ct);
            var datosDiariosHembras = await GenerarReporteDiarioHembrasAsync(loteId, fechaInicio, fechaFin, ct);
            
            // Reutilizar método existente para datos semanales
            var reporteCompleto = await GenerarReporteLevanteCompletoAsync(loteId, consolidarSublotes, ct);
            
            return new ReporteTecnicoLevanteConTabsDto
            {
                InformacionLote = infoLote,
                DatosDiariosMachos = datosDiariosMachos,
                DatosDiariosHembras = datosDiariosHembras,
                DatosSemanales = reporteCompleto.DatosSemanales,
                EsConsolidado = consolidarSublotes,
                SublotesIncluidos = consolidarSublotes 
                    ? reporteCompleto.SublotesIncluidos 
                    : new List<string> { sublote ?? "Sin sublote" }
            };
        }
        catch (InvalidOperationException)
        {
            throw; // Re-lanzar excepciones de operación inválida
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error al generar reporte con tabs para lote {loteId}: {ex.Message}", ex);
        }
    }

    #endregion
}

