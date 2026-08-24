// src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteContableService : IReporteContableService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly ITrasladoHuevosService _trasladoHuevosService;
    private readonly ILocationScopeResolver _scopeResolver;

    // Factor de conversión: 1 bulto = 40 kg (configurable)
    private const decimal FACTOR_CONVERSION_BULTO_KG = 40m;

    public ReporteContableService(
        ZooSanMarinoContext ctx,
        ICurrentUser currentUser,
        IMovimientoAvesService movimientoAvesService,
        ITrasladoHuevosService trasladoHuevosService,
        ILocationScopeResolver scopeResolver)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _movimientoAvesService = movimientoAvesService;
        _trasladoHuevosService = trasladoHuevosService;
        _scopeResolver = scopeResolver;
    }

    public async Task<ReporteContableCompletoDto> GenerarReporteAsync(
        GenerarReporteContableRequestDto request,
        CancellationToken ct = default)
    {
        // Validar parámetros de entrada
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        
        if (request.LotePadreId <= 0)
            throw new ArgumentException("El ID del lote padre debe ser mayor que cero", nameof(request));
        
        if (_currentUser == null)
            throw new InvalidOperationException("No se pudo obtener la información del usuario actual");
        
        if (_currentUser.CompanyId <= 0)
            throw new InvalidOperationException($"CompanyId inválido: {_currentUser.CompanyId}");

        // Validar que el lote es un lote padre
        var lotePadre = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LoteId == request.LotePadreId && 
                                     l.CompanyId == _currentUser.CompanyId &&
                                     l.DeletedAt == null &&
                                     l.LotePadreId == null, // Debe ser lote padre
                                     ct);

        if (lotePadre == null)
            throw new InvalidOperationException($"Lote padre con ID {request.LotePadreId} no encontrado o no es un lote padre");

        // Obtener todos los sublotes (hijos) del lote padre
        var sublotes = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId == request.LotePadreId &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .ToListAsync(ct);

        // Incluir también el lote padre en la lista para consolidación
        var todosLotes = new List<Lote> { lotePadre };
        todosLotes.AddRange(sublotes);

        if (!todosLotes.Any())
            throw new InvalidOperationException($"No se encontraron lotes para el lote padre {request.LotePadreId}");

        // Calcular fecha de primera llegada (mínima fecha de encaset)
        var fechasEncaset = todosLotes
            .Where(l => l.FechaEncaset.HasValue)
            .Select(l => l.FechaEncaset!.Value)
            .ToList();
        
        var fechaPrimeraLlegada = fechasEncaset.Any() 
            ? fechasEncaset.Min() 
            : DateTime.Today;

        // Obtener fecha del primer registro desde tabla unificada seguimiento_diario (levante o producción)
        var loteIds = todosLotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();
        var loteIdsString = loteIds.Select(id => id.ToString()).ToList();
        
        var tiposSeguimientoPrimera = request.FaseLote == "Produccion"
            ? new[] { "produccion" }
            : new[] { "levante" };

        var primeraFechaRegistro = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => tiposSeguimientoPrimera.Contains(s.TipoSeguimiento) &&
                        loteIdsString.Contains(s.LoteId))
            .OrderBy(s => s.Fecha)
            .Select(s => s.Fecha.Date)
            .FirstOrDefaultAsync(ct);

        // Para Produccion, también intentar desde SeguimientoProduccion si seguimiento_diario no tiene datos
        if (primeraFechaRegistro == default(DateTime) && request.FaseLote == "Produccion")
        {
            var primeraFechaProduccion = await _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => loteIds.Contains(s.LoteId))
                .OrderBy(s => s.Fecha)
                .Select(s => s.Fecha.Date)
                .FirstOrDefaultAsync(ct);
            if (primeraFechaProduccion != default(DateTime))
                primeraFechaRegistro = primeraFechaProduccion;
        }

        var fechaInicioRegistro = primeraFechaRegistro != default(DateTime) ? primeraFechaRegistro : fechaPrimeraLlegada;

        // Calcular fecha fin para filtro (usar fecha fin del request o hoy)
        // Si se especifica rango de fechas, usar la fecha fin del rango; si no, usar hoy
        var fechaFinFiltro = request.FechaFin?.Date ?? DateTime.Today;
        
        // Si se especifica fecha inicio pero no fecha fin, usar fecha inicio + 90 días como límite razonable
        if (request.FechaInicio.HasValue && !request.FechaFin.HasValue)
        {
            fechaFinFiltro = request.FechaInicio.Value.Date.AddDays(90);
        }

        // Calcular semanas contables desde fecha primera llegada hasta fecha fin filtro
        var semanasContables = CalcularSemanasContables(fechaPrimeraLlegada, fechaFinFiltro);

        // Si se especifica semana contable, usar solo esa semana
        // Si se especifica rango de fechas, filtrar semanas que intersectan con el rango
        // Si no se especifica nada, usar todas las semanas
        List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasAFiltrar;
        
        if (request.SemanaContable.HasValue)
        {
            // Prioridad: Si se especifica semana contable, usar solo esa semana
            semanasAFiltrar = semanasContables
                .Where(s => s.Semana == request.SemanaContable.Value)
                .ToList();
        }
        else if (request.FechaInicio.HasValue || request.FechaFin.HasValue)
        {
            // Si se especifica rango de fechas, filtrar semanas que intersectan con el rango
            var fechaInicioFiltro = request.FechaInicio?.Date ?? fechaPrimeraLlegada;
            // Usar la variable fechaFinFiltro ya declarada arriba, o recalcular si es necesario
            if (request.FechaFin.HasValue)
            {
                fechaFinFiltro = request.FechaFin.Value.Date;
            }
            
            // Validar que fecha inicio no sea mayor que fecha fin
            if (fechaInicioFiltro > fechaFinFiltro)
            {
                throw new ArgumentException("La fecha de inicio no puede ser posterior a la fecha de fin");
            }
            
            // Incluir todas las semanas que tengan al menos un día dentro del rango
            semanasAFiltrar = semanasContables
                .Where(s => s.FechaInicio <= fechaFinFiltro && s.FechaFin >= fechaInicioFiltro)
                .ToList();
        }
        else
        {
            // Si no se especifica nada, usar todas las semanas disponibles
            semanasAFiltrar = semanasContables;
        }

        // Obtener entradas iniciales
        var entradasIniciales = await ObtenerEntradasInicialesAsync(todosLotes, ct);

        // Ventana en la que un movimiento de bultos (de granja) puede generar fila propia del lote
        // padre: desde N días antes del encaset —la ventana de alimento previo de la empresa, la misma
        // que usa engorde— hasta el fin del reporte. Sin ella el alimento que llega antes del
        // encasetamiento no tendría dónde caer; con ella no se cuelan movimientos de otros ciclos.
        var diasAlimentoPrevio = await _ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == _currentUser.CompanyId)
            .Select(c => (int?)c.DiasAlimentoPrevioEncaset)
            .FirstOrDefaultAsync(ct);

        var ventanaBultos = ReporteContableBultosCalculos.Ventana(
            fechaPrimeraLlegada, diasAlimentoPrevio, request.FechaInicio, fechaFinFiltro);

        // Obtener datos diarios completos (aplicar filtro de fecha si existe)
        var (datosDiarios, fechasSoloBultos) = await ObtenerDatosDiariosCompletosAsync(
            todosLotes,
            entradasIniciales,
            lotePadre.LoteId ?? 0,
            lotePadre.LoteNombre ?? string.Empty,
            request.FechaInicio,
            request.FechaFin,
            request.FaseLote,
            ventanaBultos,
            ct);

        // Calcular saldos acumulativos
        // ¿El kardex de bultos viene del módulo unificado? Ahí los `retiros` son los movimientos
        // `Consumo`, que escribe el propio seguimiento diario de TODOS los lotes de la granja ⇒ el
        // consumo de este padre no se puede restar otra vez. Se resuelve una sola vez por reporte.
        var retirosYaTraenElConsumo = await LeeInventarioUnificadoAsync(_currentUser.CompanyId, ct);

        var datosConSaldos = CalcularSaldosAcumulativos(datosDiarios, entradasIniciales, semanasContables, lotePadre.GranjaId, retirosYaTraenElConsumo, ct);

        // Agrupar por semana contable y consolidar
        // Validar que haya semanas para procesar
        if (!semanasAFiltrar.Any())
        {
            throw new InvalidOperationException("No se encontraron semanas contables para el período especificado");
        }

        var lotePadreIdReporte = lotePadre.LoteId ?? 0;

        // Una fila "solo bultos" es la del lote padre en una fecha que no tenía dato del lote: lleva
        // el kardex de alimento, pero NO representa el estado de aves de esa fecha para toda la
        // familia de lotes (los sublotes no tienen fila ahí).
        bool EsFilaSoloBultos(DatoDiarioContableDto d) =>
            d.LoteId == lotePadreIdReporte && fechasSoloBultos.Contains(d.Fecha);

        var reportesSemanales = semanasAFiltrar.Select(semana =>
        {
            // La primera semana absorbe además las filas solo-bultos anteriores a su inicio (alimento
            // recibido antes del encasetamiento): de otro modo no caerían en ninguna semana.
            var datosSemana = datosConSaldos
                .Where(d => ReporteContableBultosCalculos.PerteneceASemana(
                    d.Fecha,
                    semana.FechaInicio,
                    semana.FechaFin,
                    absorbeAnteriores: semana.Semana == 1 && EsFilaSoloBultos(d)))
                .ToList();

            // Obtener saldo anterior (de la semana anterior)
            var saldoAnterior = ObtenerSaldoAnteriorSemana(semana.Semana, semanasContables, datosConSaldos, entradasIniciales, EsFilaSoloBultos);

            return ConsolidarSemanaContable(
                semana.Semana,
                semana.FechaInicio,
                semana.FechaFin,
                request.LotePadreId,
                lotePadre.LoteNombre ?? string.Empty,
                sublotes.Select(s => s.LoteNombre ?? string.Empty).ToList(),
                datosSemana,
                saldoAnterior,
                semanasContables,
                fechaInicioRegistro,
                fechaPrimeraLlegada
            );
        }).ToList();

        // Obtener semana contable actual
        var semanaActual = semanasContables
            .Where(s => s.FechaInicio <= DateTime.Today && s.FechaFin >= DateTime.Today)
            .FirstOrDefault();

        // Si no hay semana actual, usar la última semana disponible o la primera
        var semanaActualFinal = semanaActual.Semana == 0 
            ? (semanasContables.Any() ? semanasContables.LastOrDefault() : default((int Semana, DateTime FechaInicio, DateTime FechaFin)))
            : semanaActual;
        
        // Si aún no hay semana, usar valores por defecto
        if (semanaActualFinal.Semana == 0)
        {
            semanaActualFinal = (1, fechaPrimeraLlegada, fechaPrimeraLlegada.AddDays(6));
        }

        // Alcance del kardex de bultos: los movimientos de alimento se traen por GRANJA (no hay dato
        // de lote con que filtrarlos), asi que cuando la granja tiene mas de un lote padre los
        // reportes de todos muestran los mismos kilos. Se cuenta para poder decirlo — no se cambia
        // ningun numero. Medido en ago-2026: 10 de los 11 padres de Sanmarino estan en ese caso.
        var lotesPadreEnGranja = await _ctx.Lotes.AsNoTracking()
            .CountAsync(l => l.CompanyId == _currentUser.CompanyId
                          && l.GranjaId == lotePadre.GranjaId
                          && l.DeletedAt == null
                          && l.LotePadreId == null, ct);

        return new ReporteContableCompletoDto
        {
            LotePadreId = lotePadre.LoteId ?? 0,
            LotePadreNombre = lotePadre.LoteNombre ?? string.Empty,
            GranjaId = lotePadre.GranjaId,
            GranjaNombre = lotePadre.Farm?.Name ?? string.Empty,
            NucleoId = lotePadre.NucleoId,
            NucleoNombre = lotePadre.Nucleo?.NucleoNombre,
            FechaPrimeraLlegada = fechaPrimeraLlegada,
            SemanaContableActual = semanaActualFinal.Semana,
            FechaInicioSemanaActual = semanaActualFinal.FechaInicio,
            FechaFinSemanaActual = semanaActualFinal.FechaFin,
            ReportesSemanales = reportesSemanales,
            LotesPadreEnGranja = lotesPadreEnGranja,
            AdvertenciaBultos = ReporteContableBultosCalculos.AdvertenciaAlcance(
                lotesPadreEnGranja, lotePadre.Farm?.Name)
        };
    }

    public async Task<List<int>> ObtenerSemanasContablesAsync(
        int lotePadreId,
        CancellationToken ct = default)
    {
        // Validar que el lote es un lote padre
        var lotePadre = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == lotePadreId && 
                                     l.CompanyId == _currentUser.CompanyId &&
                                     l.DeletedAt == null &&
                                     l.LotePadreId == null, ct);

        if (lotePadre == null)
            return new List<int>();

        // Obtener todos los sublotes
        var sublotes = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LotePadreId == lotePadreId &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .ToListAsync(ct);

        var todosLotes = new List<Lote> { lotePadre };
        todosLotes.AddRange(sublotes);

        // Calcular fecha de primera llegada
        var fechasEncaset = todosLotes
            .Where(l => l.FechaEncaset.HasValue)
            .Select(l => l.FechaEncaset!.Value)
            .ToList();
        
        var fechaPrimeraLlegada = fechasEncaset.Any() 
            ? fechasEncaset.Min() 
            : DateTime.Today;

        // Calcular semanas contables
        var semanasContables = CalcularSemanasContables(fechaPrimeraLlegada, DateTime.Today);

        return semanasContables.Select(s => s.Semana).ToList();
    }
}

