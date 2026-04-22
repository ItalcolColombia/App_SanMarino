// src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ReporteContableService : IReporteContableService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IMovimientoAvesService _movimientoAvesService;
    private readonly IFarmInventoryMovementService _inventoryMovementService;
    private readonly ITrasladoHuevosService _trasladoHuevosService;
    
    // Factor de conversión: 1 bulto = 40 kg (configurable)
    private const decimal FACTOR_CONVERSION_BULTO_KG = 40m;

    public ReporteContableService(
        ZooSanMarinoContext ctx, 
        ICurrentUser currentUser,
        IMovimientoAvesService movimientoAvesService,
        IFarmInventoryMovementService inventoryMovementService,
        ITrasladoHuevosService trasladoHuevosService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _movimientoAvesService = movimientoAvesService;
        _inventoryMovementService = inventoryMovementService;
        _trasladoHuevosService = trasladoHuevosService;
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

        // Obtener datos diarios completos (aplicar filtro de fecha si existe)
        var datosDiarios = await ObtenerDatosDiariosCompletosAsync(
            todosLotes,
            entradasIniciales,
            lotePadre.LoteId ?? 0,
            lotePadre.LoteNombre ?? string.Empty,
            request.FechaInicio,
            request.FechaFin,
            request.FaseLote,
            ct);

        // Calcular saldos acumulativos
        var datosConSaldos = CalcularSaldosAcumulativos(datosDiarios, entradasIniciales, semanasContables, lotePadre.GranjaId, ct);

        // Agrupar por semana contable y consolidar
        // Validar que haya semanas para procesar
        if (!semanasAFiltrar.Any())
        {
            throw new InvalidOperationException("No se encontraron semanas contables para el período especificado");
        }

        var reportesSemanales = semanasAFiltrar.Select(semana => 
        {
            var datosSemana = datosConSaldos
                .Where(d => d.Fecha >= semana.FechaInicio && d.Fecha <= semana.FechaFin)
                .ToList();

            // Obtener saldo anterior (de la semana anterior)
            var saldoAnterior = ObtenerSaldoAnteriorSemana(semana.Semana, semanasContables, datosConSaldos, entradasIniciales);

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
            ReportesSemanales = reportesSemanales
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

    #region Métodos Privados

    /// <summary>
    /// Calcula las semanas contables desde la fecha de primera llegada hasta la fecha especificada
    /// La semana contable inicia cuando llega el primer lote y dura 7 días calendario
    /// </summary>
    private List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> CalcularSemanasContables(
        DateTime fechaPrimeraLlegada, 
        DateTime fechaHasta)
    {
        if (fechaPrimeraLlegada > fechaHasta)
        {
            throw new ArgumentException("La fecha de primera llegada no puede ser posterior a la fecha hasta");
        }

        var semanas = new List<(int Semana, DateTime FechaInicio, DateTime FechaFin)>();
        var fechaInicio = fechaPrimeraLlegada.Date;
        var fechaHastaDate = fechaHasta.Date;
        var semana = 1;
        const int maxSemanas = 200; // Límite de seguridad para evitar loops infinitos

        while (fechaInicio <= fechaHastaDate && semana <= maxSemanas)
        {
            var fechaFin = fechaInicio.AddDays(6); // 7 días calendario (incluyendo el día inicial)
            semanas.Add((semana, fechaInicio, fechaFin));
            fechaInicio = fechaFin.AddDays(1);
            semana++;
        }

        return semanas;
    }

    /// <summary>
    /// Obtiene los consumos diarios de todos los lotes (levante y producción)
    /// </summary>
    private async Task<List<ConsumoDiarioContableDto>> ObtenerConsumosDiariosAsync(
        List<Lote> lotes,
        CancellationToken ct)
    {
        var consumos = new List<ConsumoDiarioContableDto>();
        var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();

        // Calcular fecha mínima de encaset para filtrar consumos
        var fechasEncaset = lotes
            .Where(l => l.FechaEncaset.HasValue)
            .Select(l => l.FechaEncaset!.Value)
            .ToList();
        
        var fechaMinima = fechasEncaset.Any() 
            ? fechasEncaset.Min().Date 
            : DateTime.Today.Date;

        var loteIdsString = loteIds.Select(id => id.ToString()).ToList();
        var lotesDict = lotes.ToDictionary(l => l.LoteId!.Value, l => l.LoteNombre ?? string.Empty);

        // Obtener consumos desde tabla unificada seguimiento_diario (levante y producción)
        var consumosUnificado = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => (s.TipoSeguimiento == "levante" || s.TipoSeguimiento == "produccion") &&
                        loteIdsString.Contains(s.LoteId) &&
                        s.Fecha.Date >= fechaMinima)
            .Select(s => new
            {
                s.Fecha,
                s.LoteId,
                ConsumoKg = (decimal)((s.ConsumoKgHembras ?? 0) + (s.ConsumoKgMachos ?? 0))
            })
            .ToListAsync(ct);

        foreach (var c in consumosUnificado)
        {
            var loteIdInt = int.TryParse(c.LoteId, out var id) ? id : 0;
            if (loteIdInt == 0) continue;
            consumos.Add(new ConsumoDiarioContableDto
            {
                Fecha = c.Fecha.Date,
                LoteId = loteIdInt,
                LoteNombre = lotesDict.TryGetValue(loteIdInt, out var nombre) ? nombre : string.Empty,
                ConsumoAlimento = c.ConsumoKg,
                ConsumoAgua = 0,
                ConsumoMedicamento = 0,
                ConsumoVacuna = 0,
                OtrosConsumos = 0,
                TotalConsumo = c.ConsumoKg
            });
        }

        // Agrupar por fecha y lote, sumando consumos
        return consumos
            .GroupBy(c => new { c.Fecha, c.LoteId })
            .Select(g => new ConsumoDiarioContableDto
            {
                Fecha = g.Key.Fecha,
                LoteId = g.Key.LoteId,
                LoteNombre = g.First().LoteNombre,
                ConsumoAlimento = g.Sum(c => c.ConsumoAlimento),
                ConsumoAgua = g.Sum(c => c.ConsumoAgua),
                ConsumoMedicamento = g.Sum(c => c.ConsumoMedicamento),
                ConsumoVacuna = g.Sum(c => c.ConsumoVacuna),
                OtrosConsumos = g.Sum(c => c.OtrosConsumos),
                TotalConsumo = g.Sum(c => c.TotalConsumo)
            })
            .OrderBy(c => c.Fecha)
            .ToList();
    }

    /// <summary>
    /// Obtiene las entradas iniciales de aves por lote
    /// </summary>
    private async Task<Dictionary<int, (int hembras, int machos)>> ObtenerEntradasInicialesAsync(
        List<Lote> lotes,
        CancellationToken ct)
    {
        var entradas = new Dictionary<int, (int, int)>();
        var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();

        // Opción B: lotes en producción = Lote con Fase "Produccion" (propio o hijo)
        var lotesProduccion = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.Fase == "Produccion" && l.DeletedAt == null &&
                (loteIds.Contains(l.LoteId ?? 0) || (l.LotePadreId.HasValue && loteIds.Contains(l.LotePadreId.Value))))
            .ToListAsync(ct);

        foreach (var lp in lotesProduccion)
        {
            var reportKey = lp.LotePadreId ?? lp.LoteId;
            if (reportKey.HasValue)
                entradas[reportKey.Value] = (lp.HembrasInicialesProd ?? 0, lp.MachosInicialesProd ?? 0);
        }

        // Lotes en levante (sin registro de producción)
        foreach (var lote in lotes)
        {
            if (lote.LoteId.HasValue && !entradas.ContainsKey(lote.LoteId.Value))
                entradas[lote.LoteId.Value] = (lote.HembrasL ?? 0, lote.MachosL ?? 0);
        }

        return entradas;
    }

    /// <summary>
    /// Obtiene datos diarios completos (aves, mortalidad, selección, ventas, traslados, consumo, bultos)
    /// </summary>
    private async Task<List<DatoDiarioContableDto>> ObtenerDatosDiariosCompletosAsync(
        List<Lote> lotes,
        Dictionary<int, (int hembras, int machos)> entradasIniciales,
        int lotePadreId,
        string lotePadreNombre,
        DateTime? fechaInicioFiltro,
        DateTime? fechaFinFiltro,
        string faseLote,
        CancellationToken ct)
    {
        var datosDiarios = new List<DatoDiarioContableDto>();
        var loteIds = lotes.Where(l => l.LoteId.HasValue).Select(l => l.LoteId!.Value).ToList();
        var loteIdsString = loteIds.Select(id => id.ToString()).ToList();

        // Plantillas para crear listas vacías con el tipo correcto (patrón C# para anonymous types)
        var _levanteTemplate = new { LoteId = 0, Fecha = DateTime.MinValue, MortalidadHembras = (int?)null, MortalidadMachos = (int?)null, SelH = (int?)null, SelM = (int?)null, ConsumoKgHembras = (decimal?)null, ConsumoKgMachos = (decimal?)null };
        var _prodTemplate    = new { LoteId = 0, Fecha = DateTime.MinValue, MortalidadH = 0, MortalidadM = 0, SelH = 0, ConsKgH = 0m, ConsKgM = 0m };

        // ── LEVANTE: datos desde seguimiento_diario (tipo = "levante") ──────────
        var datosLevante = new[] { _levanteTemplate }.Take(0).ToList();
        if (faseLote != "Produccion")
        {
            var queryLevante = _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "levante" && loteIdsString.Contains(s.LoteId));

            if (fechaInicioFiltro.HasValue)
                queryLevante = queryLevante.Where(s => s.Fecha.Date >= fechaInicioFiltro.Value.Date);
            if (fechaFinFiltro.HasValue)
                queryLevante = queryLevante.Where(s => s.Fecha.Date <= fechaFinFiltro.Value.Date);

            var datosLevanteRaw = await queryLevante
                .Select(s => new { s.LoteId, s.Fecha, s.MortalidadHembras, s.MortalidadMachos, s.SelH, s.SelM, s.ConsumoKgHembras, s.ConsumoKgMachos })
                .ToListAsync(ct);

            datosLevante = datosLevanteRaw
                .Select(s => new { LoteId = int.TryParse(s.LoteId, out var id) ? id : 0, s.Fecha, s.MortalidadHembras, s.MortalidadMachos, s.SelH, s.SelM, s.ConsumoKgHembras, s.ConsumoKgMachos })
                .Where(x => x.LoteId > 0)
                .ToList();
        }

        // ── PRODUCCIÓN: desde SeguimientoProduccion (produccion_diaria) o seguimiento_diario fallback ──
        var datosProduccion = new[] { _prodTemplate }.Take(0).ToList();
        if (faseLote != "Levante")
        {
            // Primero intentar desde SeguimientoProduccion (tabla produccion_diaria)
            var queryProd = _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => loteIds.Contains(s.LoteId));

            if (fechaInicioFiltro.HasValue)
                queryProd = queryProd.Where(s => s.Fecha.Date >= fechaInicioFiltro.Value.Date);
            if (fechaFinFiltro.HasValue)
                queryProd = queryProd.Where(s => s.Fecha.Date <= fechaFinFiltro.Value.Date);

            var produccionDiariaRaw = await queryProd
                .Select(s => new { LoteId = s.LoteId, s.Fecha, MortalidadH = s.MortalidadH, MortalidadM = s.MortalidadM, SelH = s.SelH, ConsKgH = s.ConsKgH, ConsKgM = s.ConsKgM })
                .ToListAsync(ct);

            if (produccionDiariaRaw.Any())
            {
                datosProduccion = produccionDiariaRaw.ToList();
            }
            else
            {
                // Fallback: seguimiento_diario tipo=produccion
                var queryProdFallback = _ctx.SeguimientoDiario
                    .AsNoTracking()
                    .Where(s => s.TipoSeguimiento == "produccion" && loteIdsString.Contains(s.LoteId));

                if (fechaInicioFiltro.HasValue)
                    queryProdFallback = queryProdFallback.Where(s => s.Fecha.Date >= fechaInicioFiltro.Value.Date);
                if (fechaFinFiltro.HasValue)
                    queryProdFallback = queryProdFallback.Where(s => s.Fecha.Date <= fechaFinFiltro.Value.Date);

                var datosProduccionRaw = await queryProdFallback
                    .Select(s => new
                    {
                        LoteId = 0, // placeholder; parsed below
                        s.Fecha,
                        MortalidadH = s.MortalidadHembras ?? 0,
                        MortalidadM = s.MortalidadMachos ?? 0,
                        SelH       = s.SelH ?? 0,
                        ConsKgH    = s.ConsumoKgHembras ?? 0m,
                        ConsKgM    = s.ConsumoKgMachos ?? 0m,
                        LoteIdStr  = s.LoteId
                    })
                    .ToListAsync(ct);

                datosProduccion = datosProduccionRaw
                    .Select(s => new { LoteId = int.TryParse(s.LoteIdStr, out var id) ? id : 0, s.Fecha, s.MortalidadH, s.MortalidadM, s.SelH, s.ConsKgH, s.ConsKgM })
                    .Where(x => x.LoteId > 0)
                    .ToList();
            }
        }

        // Obtener ventas y traslados (solo si hay lotes)
        var ventasTraslados = loteIds.Any() 
            ? await ObtenerVentasYTrasladosAsync(loteIds, ct)
            : new Dictionary<(int loteId, DateTime fecha), (int ventasH, int ventasM, int trasladosH, int trasladosM)>();

        // Obtener datos de bultos (si hay granja y lotes)
        var granjaId = lotes.FirstOrDefault()?.GranjaId ?? 0;
        var datosBultos = (loteIds.Any() && granjaId > 0)
            ? await ObtenerDatosBultosAsync(loteIds, granjaId, ct)
            : new List<(DateTime Fecha, decimal SaldoAnterior, decimal Traslados, decimal Entradas, decimal Retiros, decimal ConsumoHembras, decimal ConsumoMachos)>();

        // Consolidar todas las fechas (usar HashSet para mejor rendimiento con muchos datos)
        var todasLasFechasSet = new HashSet<DateTime>();
        foreach (var d in datosLevante)
            todasLasFechasSet.Add(d.Fecha.Date);
        foreach (var d in datosProduccion)
            todasLasFechasSet.Add(d.Fecha.Date);
        
        foreach (var v in ventasTraslados)
        {
            todasLasFechasSet.Add(v.Key.fecha);
        }
        
        foreach (var b in datosBultos)
        {
            todasLasFechasSet.Add(b.Fecha);
        }
        
        var todasLasFechas = todasLasFechasSet.OrderBy(f => f).ToList();

        // Generar un registro por lote por fecha (sin consolidar)
        foreach (var fecha in todasLasFechas)
        {
            var bultos = datosBultos.FirstOrDefault(d => d.Fecha == fecha);
            var tieneBultos = bultos.Fecha != default(DateTime);

            foreach (var lote in lotes)
            {
                if (!lote.LoteId.HasValue) continue;

                var loteId = lote.LoteId.Value;

                var levante = datosLevante.FirstOrDefault(d => d.LoteId == loteId && d.Fecha.Date == fecha);
                var produccion = datosProduccion.FirstOrDefault(d => d.LoteId == loteId && d.Fecha.Date == fecha);
                var fechaEncasetLote = lote.FechaEncaset?.Date ?? DateTime.MinValue;
                var tieneEntradas = fecha.Date == fechaEncasetLote && entradasIniciales.ContainsKey(loteId);
                var (ventasH, ventasM, trasladosH, trasladosM) = ventasTraslados
                    .TryGetValue((loteId, fecha), out var vt) ? vt : (0, 0, 0, 0);

                // Omitir si no hay ningún dato para este lote en esta fecha
                if (levante == null && produccion == null && !tieneEntradas &&
                    ventasH == 0 && ventasM == 0 && trasladosH == 0 && trasladosM == 0)
                    continue;

                var entradasH = tieneEntradas ? entradasIniciales[loteId].hembras : 0;
                var entradasM = tieneEntradas ? entradasIniciales[loteId].machos : 0;
                var consumoKgH = (decimal)(levante?.ConsumoKgHembras ?? 0) + (produccion?.ConsKgH ?? 0);
                var consumoKgM = (decimal)(levante?.ConsumoKgMachos ?? 0) + (produccion?.ConsKgM ?? 0);
                var consumoBultosH = consumoKgH / FACTOR_CONVERSION_BULTO_KG;
                var consumoBultosM = consumoKgM / FACTOR_CONVERSION_BULTO_KG;
                // Los bultos (entradas/traslados/retiros) son a nivel de granja; solo se asignan al lote padre
                var esPadre = loteId == lotePadreId;

                var dato = new DatoDiarioContableDto
                {
                    Fecha = fecha,
                    LoteId = loteId,
                    LoteNombre = lote.LoteNombre ?? string.Empty,

                    EntradasHembras = entradasH,
                    EntradasMachos = entradasM,
                    MortalidadHembras = levante?.MortalidadHembras ?? produccion?.MortalidadH ?? 0,
                    MortalidadMachos = levante?.MortalidadMachos ?? produccion?.MortalidadM ?? 0,
                    SeleccionHembras = levante?.SelH ?? produccion?.SelH ?? 0,
                    SeleccionMachos = levante?.SelM ?? 0,
                    VentasHembras = ventasH,
                    VentasMachos = ventasM,
                    TrasladosHembras = trasladosH,
                    TrasladosMachos = trasladosM,

                    ConsumoAlimentoHembras = consumoKgH,
                    ConsumoAlimentoMachos = consumoKgM,

                    SaldoBultosAnterior = esPadre && tieneBultos ? bultos.SaldoAnterior : 0,
                    TrasladosBultos = esPadre && tieneBultos ? bultos.Traslados : 0,
                    EntradasBultos = esPadre && tieneBultos ? bultos.Entradas : 0,
                    RetirosBultos = esPadre && tieneBultos ? bultos.Retiros : 0,
                    ConsumoBultosHembras = consumoBultosH,
                    ConsumoBultosMachos = consumoBultosM,
                };

                datosDiarios.Add(dato);
            }
        }

        return datosDiarios.OrderBy(d => d.Fecha).ThenBy(d => d.LoteId).ToList();
    }

    /// <summary>
    /// Obtiene ventas y traslados de aves por lote y fecha
    /// </summary>
    private async Task<Dictionary<(int loteId, DateTime fecha), (int ventasH, int ventasM, int trasladosH, int trasladosM)>> 
        ObtenerVentasYTrasladosAsync(
        List<int> loteIds,
        CancellationToken ct)
    {
        var resultado = new Dictionary<(int, DateTime), (int, int, int, int)>();

        if (!loteIds.Any()) return resultado;

        // Obtener movimientos completados para cada lote
        foreach (var loteId in loteIds)
        {
            try
            {
                var movimientos = await _movimientoAvesService.GetMovimientosByLoteAsync(loteId);

                foreach (var mov in movimientos)
                {
                    // Solo considerar movimientos completados
                    if (mov.Estado != "Completado") continue;
                    
                    // Solo considerar movimientos de salida (origen)
                    if (mov.Origen?.LoteId != loteId) continue;

                    var fecha = mov.FechaMovimiento.Date;
                    var key = (loteId, fecha);

                    if (!resultado.ContainsKey(key))
                    {
                        resultado[key] = (0, 0, 0, 0);
                    }

                    var (vH, vM, tH, tM) = resultado[key];

                    if (mov.TipoMovimiento == "Venta")
                    {
                        vH += mov.CantidadHembras;
                        vM += mov.CantidadMachos;
                    }
                    else if (mov.TipoMovimiento == "Traslado")
                    {
                        tH += mov.CantidadHembras;
                        tM += mov.CantidadMachos;
                    }

                    resultado[key] = (vH, vM, tH, tM);
                }
            }
            catch
            {
                // Si hay error al obtener movimientos de un lote, continuar con los demás
                continue;
            }
        }

        return resultado;
    }

    /// <summary>
    /// Obtiene datos de bultos (entradas, traslados, consumo)
    /// Solo considera productos con type_item = 'alimento' en su metadata
    /// </summary>
    private async Task<List<(DateTime Fecha, decimal SaldoAnterior, decimal Traslados, decimal Entradas, decimal Retiros, decimal ConsumoHembras, decimal ConsumoMachos)>> 
        ObtenerDatosBultosAsync(
        List<int> loteIds,
        int granjaId,
        CancellationToken ct)
    {
        var datos = new List<(DateTime, decimal, decimal, decimal, decimal, decimal, decimal)>();

        if (granjaId == 0) return datos;

        // Filtrar por compañía del usuario para evitar leer filas con company_id NULL (evita error "Column 'company_id' is null")
        var companyId = _currentUser?.CompanyId ?? 0;
        if (companyId <= 0) return datos;

        // Obtener IDs de productos que tienen type_item = 'alimento' en su metadata
        // Nota: EF Core no puede traducir TryGetProperty a SQL, por lo que obtenemos todos los activos y filtramos en memoria
        var todosProductos = await _ctx.CatalogItems
            .AsNoTracking()
            .Where(c => c.Activo && c.CompanyId == companyId)
            .ToListAsync(ct);

        var productosAlimento = todosProductos
            .Where(c => c.Metadata.RootElement.TryGetProperty("type_item", out var typeItem) &&
                       typeItem.GetString() == "alimento")
            .Select(c => c.Id)
            .ToList();

        if (!productosAlimento.Any()) return datos;

        // Obtener movimientos de inventario solo para productos de tipo 'alimento'
        // Nota: Los movimientos de inventario están a nivel de granja, no de lote
        var query = new MovementQuery
        {
            Type = null, // Obtener todos los tipos
            Page = 1,
            PageSize = 10000
        };

        var movimientos = await _inventoryMovementService.GetPagedAsync(granjaId, query, ct);

        // Filtrar solo movimientos de productos con type_item = 'alimento'
        var movimientosAlimento = movimientos.Items
            .Where(m => productosAlimento.Contains(m.CatalogItemId))
            .ToList();

        // Agrupar por fecha
        var movimientosPorFecha = movimientosAlimento
            .GroupBy(m => m.CreatedAt.Date)
            .ToList();

        foreach (var grupo in movimientosPorFecha)
        {
            var fecha = grupo.Key;
            
            // Entradas de bultos (MovementType = "Entry" o "TransferIn")
            // Nota: Si Unit = "bultos", usar Quantity directamente
            // Si Unit = "kg", convertir a bultos usando FACTOR_CONVERSION_BULTO_KG
            var entradas = grupo
                .Where(m => m.MovementType == "Entry" || m.MovementType == "TransferIn")
                .Sum(m => m.Unit.ToLower() == "bultos" || m.Unit.ToLower() == "bulto" 
                    ? m.Quantity 
                    : m.Quantity / FACTOR_CONVERSION_BULTO_KG);
            
            // Traslados de bultos (MovementType = "TransferOut")
            var traslados = grupo
                .Where(m => m.MovementType == "TransferOut")
                .Sum(m => m.Unit.ToLower() == "bultos" || m.Unit.ToLower() == "bulto" 
                    ? m.Quantity 
                    : m.Quantity / FACTOR_CONVERSION_BULTO_KG);
            
            // Retiros de bultos (MovementType = "Exit")
            var retiros = grupo
                .Where(m => m.MovementType == "Exit")
                .Sum(m => m.Unit.ToLower() == "bultos" || m.Unit.ToLower() == "bulto" 
                    ? m.Quantity 
                    : m.Quantity / FACTOR_CONVERSION_BULTO_KG);

            datos.Add((fecha, 0, traslados, entradas, retiros, 0, 0));
        }

        return datos;
    }

    /// <summary>
    /// Calcula saldos acumulativos de aves y bultos
    /// </summary>
    private List<DatoDiarioContableDto> CalcularSaldosAcumulativos(
        List<DatoDiarioContableDto> datosDiarios,
        Dictionary<int, (int hembras, int machos)> entradasIniciales,
        List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasContables,
        int granjaId,
        CancellationToken ct)
    {
        var datosConSaldos = new List<DatoDiarioContableDto>();
        var saldosPorLote = new Dictionary<int, (int hembras, int machos)>();
        var saldoBultosPorFecha = new Dictionary<DateTime, decimal>();

        // Inicializar saldos con entradas iniciales
        foreach (var (loteId, (hembras, machos)) in entradasIniciales)
        {
            saldosPorLote[loteId] = (hembras, machos);
        }

        // Agrupar datos por fecha para calcular saldo de bultos correctamente
        var datosPorFecha = datosDiarios
            .GroupBy(d => d.Fecha)
            .OrderBy(g => g.Key)
            .ToList();

        decimal saldoBultosAcumulado = 0;

        foreach (var grupoFecha in datosPorFecha)
        {
            var fecha = grupoFecha.Key;
            var datosFecha = grupoFecha.ToList();

            // Calcular saldo anterior de bultos para esta fecha
            var fechaAnterior = fecha.AddDays(-1);
            if (saldoBultosPorFecha.ContainsKey(fechaAnterior))
            {
                saldoBultosAcumulado = saldoBultosPorFecha[fechaAnterior];
            }

            // Procesar cada registro en esta fecha (ya consolidado por fecha)
            foreach (var dato in datosFecha)
            {
                var loteId = dato.LoteId;
                
                // Obtener saldo anterior de aves
                var (saldoHAnterior, saldoMAnterior) = saldosPorLote.GetValueOrDefault(loteId, (0, 0));

                // Calcular saldo actual de aves
                var saldoHActual = saldoHAnterior 
                    + dato.EntradasHembras
                    - dato.MortalidadHembras
                    - dato.SeleccionHembras
                    - dato.VentasHembras
                    - dato.TrasladosHembras;

                var saldoMActual = saldoMAnterior
                    + dato.EntradasMachos
                    - dato.MortalidadMachos
                    - dato.SeleccionMachos
                    - dato.VentasMachos
                    - dato.TrasladosMachos;

                // Actualizar saldos de aves
                saldosPorLote[loteId] = (Math.Max(0, saldoHActual), Math.Max(0, saldoMActual));

                // Calcular saldo de bultos (consolidado para todos los lotes en la misma fecha)
                // Los bultos se calculan una vez por fecha, no por lote
                saldoBultosAcumulado = saldoBultosAcumulado
                    + dato.EntradasBultos
                    - dato.TrasladosBultos
                    - dato.RetirosBultos
                    - dato.ConsumoBultosHembras
                    - dato.ConsumoBultosMachos;

                var datoConSaldo = dato with
                {
                    SaldoHembras = Math.Max(0, saldoHActual),
                    SaldoMachos = Math.Max(0, saldoMActual),
                    SaldoBultosAnterior = saldoBultosPorFecha.GetValueOrDefault(fechaAnterior, 0),
                    SaldoBultos = Math.Max(0, saldoBultosAcumulado)
                };

                datosConSaldos.Add(datoConSaldo);
            }

            // Guardar saldo de bultos para esta fecha (solo una vez por fecha)
            if (datosFecha.Any())
            {
                saldoBultosPorFecha[fecha] = Math.Max(0, saldoBultosAcumulado);
            }
        }

        return datosConSaldos;
    }

    /// <summary>
    /// Obtiene el saldo anterior de una semana (saldo final de la semana anterior)
    /// </summary>
    private (int hembras, int machos, decimal bultos) ObtenerSaldoAnteriorSemana(
        int semanaActual,
        List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasContables,
        List<DatoDiarioContableDto> datosConSaldos,
        Dictionary<int, (int hembras, int machos)> entradasIniciales)
    {
        // Si es la primera semana, usar entradas iniciales
        if (semanaActual == 1)
        {
            var totalHembras = entradasIniciales.Values.Sum(e => e.hembras);
            var totalMachos = entradasIniciales.Values.Sum(e => e.machos);
            // Para bultos, el saldo inicial es 0 (se calcula desde las entradas)
            return (totalHembras, totalMachos, 0);
        }

        // Obtener semana anterior
        var semanaAnterior = semanasContables
            .FirstOrDefault(s => s.Semana == semanaActual - 1);

        if (semanaAnterior.Semana == 0)
        {
            return (0, 0, 0);
        }

        // Obtener última fecha de la semana anterior y sumar saldos de todos los lotes
        var ultimaFechaSemanaAnterior = datosConSaldos
            .Where(d => d.Fecha >= semanaAnterior.FechaInicio && d.Fecha <= semanaAnterior.FechaFin)
            .Select(d => d.Fecha)
            .DefaultIfEmpty(default)
            .Max();

        if (ultimaFechaSemanaAnterior == default(DateTime))
            return (0, 0, 0);

        var ultimosDatos = datosConSaldos
            .Where(d => d.Fecha == ultimaFechaSemanaAnterior)
            .ToList();

        var totalH = ultimosDatos.Sum(d => d.SaldoHembras);
        var totalM = ultimosDatos.Sum(d => d.SaldoMachos);
        var saldoBultos = ultimosDatos.Max(d => (decimal?)d.SaldoBultos) ?? 0;

        return (totalH, totalM, saldoBultos);
    }

    /// <summary>
    /// Consolida los datos de una semana contable
    /// </summary>
    private ReporteContableSemanalDto ConsolidarSemanaContable(
        int semanaContable,
        DateTime fechaInicio,
        DateTime fechaFin,
        int lotePadreId,
        string lotePadreNombre,
        List<string> sublotes,
        List<DatoDiarioContableDto> datosDiarios,
        (int hembras, int machos, decimal bultos) saldoAnterior,
        List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasContables,
        DateTime fechaInicioRegistro,
        DateTime fechaPrimeraLlegada)
    {
        // Calcular totales semanales
        var mortalidadH = datosDiarios.Sum(d => d.MortalidadHembras);
        var mortalidadM = datosDiarios.Sum(d => d.MortalidadMachos);
        var seleccionH = datosDiarios.Sum(d => d.SeleccionHembras);
        var seleccionM = datosDiarios.Sum(d => d.SeleccionMachos);
        var ventasH = datosDiarios.Sum(d => d.VentasHembras);
        var ventasM = datosDiarios.Sum(d => d.VentasMachos);
        var trasladosH = datosDiarios.Sum(d => d.TrasladosHembras);
        var trasladosM = datosDiarios.Sum(d => d.TrasladosMachos);
        var entradasH = datosDiarios.Sum(d => d.EntradasHembras);
        var entradasM = datosDiarios.Sum(d => d.EntradasMachos);

        // Calcular saldo final
        var saldoFinH = saldoAnterior.hembras + entradasH - mortalidadH - seleccionH - ventasH - trasladosH;
        var saldoFinM = saldoAnterior.machos + entradasM - mortalidadM - seleccionM - ventasM - trasladosM;

        // Bultos
        var trasladosBultos = datosDiarios.Sum(d => d.TrasladosBultos);
        var entradasBultos = datosDiarios.Sum(d => d.EntradasBultos);
        var retirosBultos = datosDiarios.Sum(d => d.RetirosBultos);
        var consumoBultosH = datosDiarios.Sum(d => d.ConsumoBultosHembras);
        var consumoBultosM = datosDiarios.Sum(d => d.ConsumoBultosMachos);
        var saldoBultosFinal = saldoAnterior.bultos + entradasBultos - trasladosBultos - retirosBultos - consumoBultosH - consumoBultosM;

        // Consumo (Kg)
        var consumoAlimento = datosDiarios.Sum(d => d.ConsumoAlimentoHembras + d.ConsumoAlimentoMachos);

        // Crear ConsumosDiarios para compatibilidad
        var consumosDiarios = datosDiarios.Select(d => new ConsumoDiarioContableDto
        {
            Fecha = d.Fecha,
            LoteId = d.LoteId,
            LoteNombre = d.LoteNombre,
            ConsumoAlimento = d.ConsumoAlimentoHembras + d.ConsumoAlimentoMachos,
            ConsumoAgua = d.ConsumoAgua,
            ConsumoMedicamento = d.ConsumoMedicamento,
            ConsumoVacuna = d.ConsumoVacuna,
            OtrosConsumos = 0,
            TotalConsumo = d.ConsumoAlimentoHembras + d.ConsumoAlimentoMachos
        }).ToList();

        // Calcular secciones INICIO (primeros 7 días) y LEVANTE (después de 7 días)
        // Validar que fechaInicioRegistro sea válida
        if (fechaInicioRegistro == default(DateTime))
        {
            fechaInicioRegistro = fechaPrimeraLlegada;
        }
        
        var fechaFinInicio = fechaInicioRegistro.AddDays(6); // Primeros 7 días (día 0 al día 6)
        
        // Datos de INICIO (primeros 7 días desde fechaInicioRegistro)
        var datosInicio = datosDiarios
            .Where(d => d.Fecha.Date >= fechaInicioRegistro.Date && d.Fecha.Date <= fechaFinInicio.Date)
            .ToList();
        
        // Datos de LEVANTE (después de los primeros 7 días)
        var datosLevante = datosDiarios
            .Where(d => d.Fecha.Date > fechaFinInicio.Date)
            .ToList();

        // Calcular sección INICIO
        SeccionReporteContableDto? seccionInicio = null;
        if (datosInicio.Any())
        {
            // Obtener saldo anterior del primer día de INICIO
            var primerDiaInicio = datosInicio.OrderBy(d => d.Fecha).First();
            var saldoBultosAnteriorInicio = primerDiaInicio.SaldoBultosAnterior;
            
            var trasladosBultosInicio = datosInicio.Sum(d => d.TrasladosBultos);
            var entradasBultosInicio = datosInicio.Sum(d => d.EntradasBultos);
            var consumoBultosHInicio = datosInicio.Sum(d => d.ConsumoBultosHembras);
            var consumoBultosMInicio = datosInicio.Sum(d => d.ConsumoBultosMachos);
            
            // Obtener saldo final del último día de INICIO
            var ultimoDiaInicio = datosInicio.OrderByDescending(d => d.Fecha).First();
            var saldoBultosFinalInicio = ultimoDiaInicio.SaldoBultos;

            seccionInicio = new SeccionReporteContableDto
            {
                TipoSeccion = "INICIO",
                FechaInicio = fechaInicioRegistro,
                FechaFin = fechaFinInicio,
                SaldoBultosAnterior = saldoBultosAnteriorInicio,
                TrasladosBultos = trasladosBultosInicio,
                EntradasBultos = entradasBultosInicio,
                ConsumoBultosHembras = consumoBultosHInicio,
                ConsumoBultosMachos = consumoBultosMInicio,
                SaldoBultosFinal = Math.Max(0, saldoBultosFinalInicio),
                DatosDiarios = datosInicio.OrderBy(d => d.Fecha).ToList()
            };
        }

        // Calcular sección LEVANTE
        SeccionReporteContableDto? seccionLevante = null;
        if (datosLevante.Any())
        {
            // Obtener saldo anterior del primer día de LEVANTE
            var primerDiaLevante = datosLevante.OrderBy(d => d.Fecha).First();
            var saldoBultosAnteriorLevante = primerDiaLevante.SaldoBultosAnterior;
            
            var trasladosBultosLevante = datosLevante.Sum(d => d.TrasladosBultos);
            var entradasBultosLevante = datosLevante.Sum(d => d.EntradasBultos);
            var consumoBultosHLevante = datosLevante.Sum(d => d.ConsumoBultosHembras);
            var consumoBultosMLevante = datosLevante.Sum(d => d.ConsumoBultosMachos);
            
            // Obtener saldo final del último día de LEVANTE
            var ultimoDiaLevante = datosLevante.OrderByDescending(d => d.Fecha).First();
            var saldoBultosFinalLevante = ultimoDiaLevante.SaldoBultos;
            
            var fechaInicioLevante = fechaFinInicio.AddDays(1);
            var fechaFinLevante = datosLevante.Max(d => d.Fecha);

            seccionLevante = new SeccionReporteContableDto
            {
                TipoSeccion = "LEVANTE",
                FechaInicio = fechaInicioLevante,
                FechaFin = fechaFinLevante,
                SaldoBultosAnterior = saldoBultosAnteriorLevante,
                TrasladosBultos = trasladosBultosLevante,
                EntradasBultos = entradasBultosLevante,
                ConsumoBultosHembras = consumoBultosHLevante,
                ConsumoBultosMachos = consumoBultosMLevante,
                SaldoBultosFinal = Math.Max(0, saldoBultosFinalLevante),
                DatosDiarios = datosLevante.OrderBy(d => d.Fecha).ToList()
            };
        }

        return new ReporteContableSemanalDto
        {
            SemanaContable = semanaContable,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            LotePadreId = lotePadreId,
            LotePadreNombre = lotePadreNombre,
            Sublotes = sublotes,
            
            // AVES - Saldo Anterior
            SaldoAnteriorHembras = saldoAnterior.hembras,
            SaldoAnteriorMachos = saldoAnterior.machos,
            
            // AVES - Entradas
            EntradasHembras = entradasH,
            EntradasMachos = entradasM,
            TotalEntradas = entradasH + entradasM,
            
            // AVES - Mortalidad
            MortalidadHembrasSemanal = mortalidadH,
            MortalidadMachosSemanal = mortalidadM,
            MortalidadTotalSemanal = mortalidadH + mortalidadM,
            
            // AVES - Selección
            SeleccionHembrasSemanal = seleccionH,
            SeleccionMachosSemanal = seleccionM,
            TotalSeleccionSemanal = seleccionH + seleccionM,
            
            // AVES - Ventas y Traslados
            VentasHembrasSemanal = ventasH,
            VentasMachosSemanal = ventasM,
            TrasladosHembrasSemanal = trasladosH,
            TrasladosMachosSemanal = trasladosM,
            TotalVentasSemanal = ventasH + ventasM,
            TotalTrasladosSemanal = trasladosH + trasladosM,
            
            // AVES - Saldo Final
            SaldoFinHembras = Math.Max(0, saldoFinH),
            SaldoFinMachos = Math.Max(0, saldoFinM),
            TotalAvesVivas = Math.Max(0, saldoFinH) + Math.Max(0, saldoFinM),
            
            // BULTO
            SaldoBultosAnterior = saldoAnterior.bultos,
            TrasladosBultosSemanal = trasladosBultos,
            EntradasBultosSemanal = entradasBultos,
            RetirosBultosSemanal = retirosBultos,
            ConsumoBultosHembrasSemanal = consumoBultosH,
            ConsumoBultosMachosSemanal = consumoBultosM,
            SaldoBultosFinal = Math.Max(0, saldoBultosFinal),
            
            // CONSUMO (Kg)
            ConsumoTotalAlimento = consumoAlimento,
            ConsumoTotalAgua = datosDiarios.Sum(d => d.ConsumoAgua),
            ConsumoTotalMedicamento = datosDiarios.Sum(d => d.ConsumoMedicamento),
            ConsumoTotalVacuna = datosDiarios.Sum(d => d.ConsumoVacuna),
            OtrosConsumos = 0,
            TotalGeneral = consumoAlimento,
            
            // Secciones INICIO y LEVANTE
            SeccionInicio = seccionInicio,
            SeccionLevante = seccionLevante,
            
            // Detalle diario
            DatosDiarios = datosDiarios.OrderBy(d => d.Fecha).ToList(),
            ConsumosDiarios = consumosDiarios.OrderBy(c => c.Fecha).ToList()
        };
    }

    #endregion

    #region Reporte Movimientos de Huevos

    public async Task<ReporteMovimientosHuevosDto> ObtenerReporteMovimientosHuevosAsync(
        ObtenerReporteMovimientosHuevosRequestDto request,
        CancellationToken ct = default)
    {
        // Validar que el lote es un lote padre
        var lotePadre = await _ctx.Lotes
            .AsNoTracking()
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
            .Select(l => new { l.LoteId, l.LoteNombre })
            .ToListAsync(ct);

        var loteIds = sublotes.Select(s => s.LoteId?.ToString() ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var loteIdsInt = sublotes.Where(s => s.LoteId.HasValue).Select(s => s.LoteId!.Value).ToList();

        if (!loteIds.Any())
            throw new InvalidOperationException($"No se encontraron sublotes para el lote padre {request.LotePadreId}");

        // Determinar rango de fechas
        DateTime fechaInicio, fechaFin;
        if (request.FechaInicio.HasValue && request.FechaFin.HasValue)
        {
            fechaInicio = request.FechaInicio.Value.Date;
            fechaFin = request.FechaFin.Value.Date;
        }
        else if (request.SemanaContable.HasValue)
        {
            // Obtener semanas contables para calcular fechas
            var semanas = await ObtenerSemanasContablesAsync(request.LotePadreId, ct);
            var semana = semanas.FirstOrDefault(s => s == request.SemanaContable.Value);
            if (semana == 0)
                throw new InvalidOperationException($"Semana contable {request.SemanaContable.Value} no encontrada");

            // Calcular fechas de la semana (simplificado - debería usar la lógica de semanas contables)
            var lotesIds = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LotePadreId == request.LotePadreId || l.LoteId == request.LotePadreId)
                .Select(l => l.LoteId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToListAsync(ct);

            var lotesIdsStr = lotesIds.Select(id => id.ToString()).ToList();
            var primeraFecha = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && lotesIdsStr.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            if (primeraFecha == default)
                throw new InvalidOperationException("No se encontraron registros de producción para calcular fechas");

            fechaInicio = primeraFecha.Date.AddDays((semana - 1) * 7);
            fechaFin = fechaInicio.AddDays(6);
        }
        else
        {
            // Usar todas las fechas disponibles desde tabla unificada seguimiento_diario (producción)
            var loteIdsStrProd = loteIdsInt.Select(id => id.ToString()).ToList();
            var primeraFecha = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && loteIdsStrProd.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            var ultimaFecha = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && loteIdsStrProd.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderByDescending(f => f)
                .FirstOrDefaultAsync(ct);

            if (primeraFecha == default)
                throw new InvalidOperationException("No se encontraron registros de producción");

            fechaInicio = primeraFecha.Date;
            fechaFin = ultimaFecha.Date;
        }

        // Obtener seguimientos diarios de producción desde tabla unificada seguimiento_diario
        var loteIdsStrSeguimientos = loteIdsInt.Select(id => id.ToString()).ToList();
        var seguimientosRaw = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "produccion" &&
                        loteIdsStrSeguimientos.Contains(s.LoteId) &&
                        s.Fecha >= fechaInicio &&
                        s.Fecha <= fechaFin)
            .OrderBy(s => s.Fecha)
            .ThenBy(s => s.LoteId)
            .Select(s => new
            {
                s.LoteId,
                s.Fecha,
                HuevoTot = s.HuevoTot ?? 0,
                HuevoInc = s.HuevoInc ?? 0,
                HuevoLimpio = s.HuevoLimpio ?? 0,
                HuevoTratado = s.HuevoTratado ?? 0,
                HuevoSucio = s.HuevoSucio ?? 0,
                HuevoDeforme = s.HuevoDeforme ?? 0,
                HuevoBlanco = s.HuevoBlanco ?? 0,
                HuevoDobleYema = s.HuevoDobleYema ?? 0,
                HuevoPiso = s.HuevoPiso ?? 0,
                HuevoPequeno = s.HuevoPequeno ?? 0,
                HuevoRoto = s.HuevoRoto ?? 0,
                HuevoDesecho = s.HuevoDesecho ?? 0,
                HuevoOtro = s.HuevoOtro ?? 0
            })
            .ToListAsync(ct);

        var seguimientos = seguimientosRaw
            .Where(s => int.TryParse(s.LoteId, out var lid) && lid > 0)
            .Select(s => new { LoteId = int.Parse(s.LoteId), s.Fecha, s.HuevoTot, s.HuevoInc, s.HuevoLimpio, s.HuevoTratado, s.HuevoSucio, s.HuevoDeforme, s.HuevoBlanco, s.HuevoDobleYema, s.HuevoPiso, s.HuevoPequeno, s.HuevoRoto, s.HuevoDesecho, s.HuevoOtro })
            .ToList();

        // Obtener traslados de huevos (API espera string)
        var traslados = new List<TrasladoHuevosDto>();
        foreach (var loteIdStr in loteIds)
        {
            var trasladosLote = await _trasladoHuevosService.ObtenerTrasladosPorLoteAsync(loteIdStr);
            traslados.AddRange(trasladosLote.Where(t => 
                t.FechaTraslado.Date >= fechaInicio && 
                t.FechaTraslado.Date <= fechaFin &&
                t.Estado == "Completado"));
        }

        // Crear diccionario de lotes para nombres
        var lotesDict = sublotes.ToDictionary(
            s => s.LoteId?.ToString() ?? string.Empty,
            s => s.LoteNombre ?? string.Empty);

        // Agrupar por fecha y consolidar
        var movimientosPorFecha = seguimientos
            .GroupBy(sp => sp.Fecha.Date)
            .Select(g =>
            {
                var fecha = g.Key;
                var seguimientosFecha = g.ToList();
                
                // Consolidar producción diaria
                var postura = seguimientosFecha.Sum(s => s.HuevoTot);
                var hvtoFertil = seguimientosFecha.Sum(s => s.HuevoInc);
                var limpio = seguimientosFecha.Sum(s => s.HuevoLimpio);
                var tratado = seguimientosFecha.Sum(s => s.HuevoTratado);
                var hvoComercial = limpio + tratado;
                var huevoDesecho = seguimientosFecha.Sum(s => s.HuevoDesecho);
                
                // Obtener traslados de esta fecha
                var trasladosFecha = traslados
                    .Where(t => t.FechaTraslado.Date == fecha)
                    .ToList();

                // Calcular movimientos
                var entrada = trasladosFecha
                    .Where(t => t.TipoOperacion == "Traslado" && t.GranjaDestinoId.HasValue)
                    .Sum(t => t.TotalHuevos);
                
                var venta = trasladosFecha
                    .Where(t => t.TipoOperacion == "Venta")
                    .Sum(t => t.TotalHuevos);
                
                var salida = trasladosFecha
                    .Where(t => t.TipoOperacion == "Traslado" && !t.GranjaDestinoId.HasValue)
                    .Sum(t => t.TotalHuevos);
                
                var trasladoAPlanta = trasladosFecha
                    .Where(t => t.TipoOperacion == "Traslado" && t.TipoDestino == "Planta")
                    .Sum(t => t.TotalHuevos);
                
                var descarte = trasladosFecha
                    .Sum(t => t.CantidadDesecho);

                // Obtener lote principal (usar el primero si hay múltiples)
                var loteId = seguimientosFecha.First().LoteId;
                var loteIdStr = loteId.ToString();
                var loteNombre = lotesDict.GetValueOrDefault(loteIdStr, loteIdStr);

                return new MovimientoHuevoDiarioDto
                {
                    Fecha = fecha,
                    LoteId = loteIdStr,
                    LoteNombre = loteNombre,
                    Postura = postura,
                    HvtoFertil = hvtoFertil,
                    HvoComercial = hvoComercial,
                    HuevoDesecho = huevoDesecho,
                    Limpio = limpio,
                    Tratado = tratado,
                    Sucio = seguimientosFecha.Sum(s => s.HuevoSucio),
                    Deforme = seguimientosFecha.Sum(s => s.HuevoDeforme),
                    Blanco = seguimientosFecha.Sum(s => s.HuevoBlanco),
                    DobleYema = seguimientosFecha.Sum(s => s.HuevoDobleYema),
                    Piso = seguimientosFecha.Sum(s => s.HuevoPiso),
                    Pequeno = seguimientosFecha.Sum(s => s.HuevoPequeno),
                    Roto = seguimientosFecha.Sum(s => s.HuevoRoto),
                    Otro = seguimientosFecha.Sum(s => s.HuevoOtro),
                    Entrada = entrada,
                    CapturaInfo = postura, // La producción diaria es la captura de información
                    Venta = venta,
                    Salida = salida,
                    TrasladoAPlanta = trasladoAPlanta,
                    Descarte = descarte
                };
            })
            .OrderBy(m => m.Fecha)
            .ToList();

        // Calcular totales
        var totales = new
        {
            Postura = movimientosPorFecha.Sum(m => m.Postura),
            HvtoFertil = movimientosPorFecha.Sum(m => m.HvtoFertil),
            HvoComercial = movimientosPorFecha.Sum(m => m.HvoComercial),
            HuevoDesecho = movimientosPorFecha.Sum(m => m.HuevoDesecho),
            Entrada = movimientosPorFecha.Sum(m => m.Entrada),
            Venta = movimientosPorFecha.Sum(m => m.Venta),
            Salida = movimientosPorFecha.Sum(m => m.Salida),
            TrasladoAPlanta = movimientosPorFecha.Sum(m => m.TrasladoAPlanta),
            Descarte = movimientosPorFecha.Sum(m => m.Descarte)
        };

        return new ReporteMovimientosHuevosDto
        {
            LotePadreId = request.LotePadreId,
            LotePadreNombre = lotePadre.LoteNombre ?? string.Empty,
            SemanaContable = request.SemanaContable,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            MovimientosDiarios = movimientosPorFecha,
            TotalPostura = totales.Postura,
            TotalHvtoFertil = totales.HvtoFertil,
            TotalHvoComercial = totales.HvoComercial,
            TotalHuevoDesecho = totales.HuevoDesecho,
            TotalEntrada = totales.Entrada,
            TotalVenta = totales.Venta,
            TotalSalida = totales.Salida,
            TotalTrasladoAPlanta = totales.TrasladoAPlanta,
            TotalDescarte = totales.Descarte
        };
    }

    #endregion

    #region Filtros disponibles

    public async Task<FiltrosContablesDto> GetFiltrosDisponiblesAsync(CancellationToken ct = default)
    {
        var lotes = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Include(l => l.LotePosturaBase)
            .Where(l => l.LotePadreId == null &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null)
            .OrderBy(l => l.Farm.Name)
            .ThenBy(l => l.NucleoId)
            .ThenBy(l => l.GalponId)
            .ThenBy(l => l.LoteNombre)
            .ToListAsync(ct);

        var granjas = lotes
            .GroupBy(l => l.GranjaId)
            .Select(gGranja => new GranjaFiltroContableDto
            {
                GranjaId = gGranja.Key,
                GranjaNombre = gGranja.First().Farm?.Name ?? gGranja.Key.ToString(),
                Nucleos = gGranja
                    .GroupBy(l => l.NucleoId)
                    .Select(gNucleo => new NucleoFiltroContableDto
                    {
                        NucleoId = gNucleo.Key,
                        NucleoNombre = gNucleo.First().Nucleo?.NucleoNombre ?? gNucleo.Key ?? "(Sin núcleo)",
                        Galpones = gNucleo
                            .GroupBy(l => l.GalponId)
                            .Select(gGalpon => new GalponFiltroContableDto
                            {
                                GalponId = gGalpon.Key,
                                GalponNombre = gGalpon.First().Galpon?.GalponNombre ?? gGalpon.Key ?? "(Sin galpón)",
                                LotesBase = gGalpon
                                    .Select(l => new LoteBaseFiltroContableDto
                                    {
                                        LoteId = l.LoteId!.Value,
                                        LoteNombre = l.LotePosturaBase?.LoteNombre ?? l.LoteNombre,
                                        LotePosturaBaseId = l.LotePosturaBaseId,
                                        CodigoErp = l.LotePosturaBase?.CodigoErp ?? l.LoteErp
                                    })
                                    .ToList()
                            })
                            .ToList()
                    })
                    .ToList()
            })
            .ToList();

        return new FiltrosContablesDto { Granjas = granjas };
    }

    #endregion
}

