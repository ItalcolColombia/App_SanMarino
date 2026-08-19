// src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
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
    /// Obtiene datos diarios completos (aves, mortalidad, selección, ventas, traslados, consumo, bultos).
    /// Devuelve además las fechas cuya fila nació SOLO por movimientos de bultos (sin dato del lote),
    /// que la semana 1 necesita para absorber el alimento recibido antes del encasetamiento.
    /// </summary>
    private async Task<(List<DatoDiarioContableDto> Datos, HashSet<DateTime> FechasSoloBultos)>
        ObtenerDatosDiariosCompletosAsync(
        List<Lote> lotes,
        Dictionary<int, (int hembras, int machos)> entradasIniciales,
        int lotePadreId,
        string lotePadreNombre,
        DateTime? fechaInicioFiltro,
        DateTime? fechaFinFiltro,
        string faseLote,
        (DateTime Desde, DateTime Hasta) ventanaBultos,
        CancellationToken ct)
    {
        var datosDiarios = new List<DatoDiarioContableDto>();
        var fechasSoloBultos = new HashSet<DateTime>();
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
            ? await ObtenerDatosBultosAsync(granjaId, ventanaBultos, ct)
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
            var padreGeneroFila = false;

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
                if (esPadre) padreGeneroFila = true;
            }

            // C1 — una fecha con movimientos de bultos genera fila del lote padre aunque ningún lote
            // tenga dato propio ese día. Antes se descartaba con el `continue` de arriba y el alimento
            // desaparecía del reporte y del saldo (caso típico: llega días antes del encasetamiento).
            if (tieneBultos && lotePadreId > 0)
            {
                var movimiento = new ReporteContableBultosCalculos.MovimientoBultosDia(
                    fecha, bultos.Traslados, bultos.Entradas, bultos.Retiros);

                if (ReporteContableBultosCalculos.GeneraFilaSoloBultos(
                        movimiento, padreGeneroFila, ventanaBultos.Desde, ventanaBultos.Hasta))
                {
                    // Las fechas del kardex de bultos nacen de un DateTimeOffset (Kind Unspecified) y
                    // las del lote son locales; sin igualar el Kind esta fila sería la única del JSON
                    // sin offset. El valor de la fecha no cambia, solo su serialización.
                    var fechaFila = DateTime.SpecifyKind(fecha, DateTimeKind.Local);

                    datosDiarios.Add(new DatoDiarioContableDto
                    {
                        Fecha = fechaFila,
                        LoteId = lotePadreId,
                        LoteNombre = lotePadreNombre,

                        SaldoBultosAnterior = bultos.SaldoAnterior,
                        TrasladosBultos = bultos.Traslados,
                        EntradasBultos = bultos.Entradas,
                        RetirosBultos = bultos.Retiros,
                    });

                    fechasSoloBultos.Add(fechaFila);
                }
            }
        }

        return (datosDiarios.OrderBy(d => d.Fecha).ThenBy(d => d.LoteId).ToList(), fechasSoloBultos);
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
    /// Obtiene el kardex de bultos de la granja (entradas, traslados, retiros) dentro de la ventana
    /// del reporte. Solo considera los movimientos cuyo tipo efectivo es alimento
    /// (<see cref="ItemInventarioTipoCalculos"/>).
    /// <para>
    /// Este método arrastró DOS defectos que se tapaban entre sí, ambos corregidos en ago-2026:
    /// </para>
    /// <list type="number">
    /// <item><b>El tope de página (commit <c>92cd918</c>).</b> Pasaba por
    /// <c>IFarmInventoryMovementService.GetPagedAsync</c> con <c>PageSize = 10000</c>, y ese método
    /// clampaba a 20 cualquier pedido mayor a 200; encima filtraba por ítem DESPUÉS de paginar, así
    /// que un movimiento de vacunas consumía cupo del kardex de alimento. El reporte veía los 20
    /// movimientos más recientes de la granja y todo lo histórico desaparecía.</item>
    /// <item><b>El criterio de "esto es alimento".</b> Miraba
    /// <c>catalogo_items.metadata-&gt;&gt;'type_item'</c>, el modelo VIEJO que ya nadie llena (NULL en
    /// el 80 % del catálogo), en vez de la columna <c>item_type</c> que lo reemplazó; y comparaba
    /// distinguiendo mayúsculas, perdiendo las filas cargadas como <c>"Alimento"</c>. Costaba 257
    /// movimientos: la granja 20 entera (236) más 19 de la granja 5. Ver
    /// <see cref="ItemInventarioTipoCalculos"/>.</item>
    /// </list>
    /// <para>
    /// Hoy los filtros (granja, empresa, país, tipo de ítem y ventana) se resuelven en UNA consulta
    /// traducida a SQL, sin tope y sin traer el catálogo a memoria.
    /// </para>
    /// </summary>
    private async Task<List<(DateTime Fecha, decimal SaldoAnterior, decimal Traslados, decimal Entradas, decimal Retiros, decimal ConsumoHembras, decimal ConsumoMachos)>>
        ObtenerDatosBultosAsync(
        int granjaId,
        (DateTime Desde, DateTime Hasta) ventanaBultos,
        CancellationToken ct)
    {
        var datos = new List<(DateTime, decimal, decimal, decimal, decimal, decimal, decimal)>();

        if (granjaId == 0) return datos;

        // Filtrar por compañía del usuario para evitar leer filas con company_id NULL (evita error "Column 'company_id' is null")
        var companyId = _currentUser?.CompanyId ?? 0;
        if (companyId <= 0) return datos;

        // Los movimientos de inventario están a nivel de GRANJA, no de lote: el reporte los imputa al
        // lote padre. La ventana acota la consulta al rango que el reporte puede imputar; el corte
        // superior es exclusivo al día siguiente (created_at es timestamptz y no está anclado a
        // medianoche) y no se usa .Date sobre la columna para no depender de la zona de la sesión.
        var (desde, hastaExclusivo) = ReporteContableBultosCalculos.RangoConsulta(ventanaBultos);
        var desdeUtc = new DateTimeOffset(DateTime.SpecifyKind(desde, DateTimeKind.Utc));
        var hastaUtc = new DateTimeOffset(DateTime.SpecifyKind(hastaExclusivo, DateTimeKind.Utc));

        // El tipo efectivo es el del movimiento y, si no lo trae, el del catálogo — el mismo criterio
        // que usa el módulo de inventario. Se compara en minúsculas porque el catálogo tiene filas
        // cargadas como "Alimento"; EF traduce ToLower() a SQL, así que el filtro entero viaja a la BD
        // y ya no hace falta traer el catálogo de la empresa a memoria.
        var tipoAlimento = ItemInventarioTipoCalculos.TipoAlimento;

        // Las empresas que ya operan sobre el módulo unificado no tienen NI UNA fila en la tabla
        // vieja: sin este desvío, sus columnas de bultos salen en cero sin ningún error a la vista.
        if (await LeeInventarioUnificadoAsync(companyId, ct))
            return await ObtenerDatosBultosUnificadoAsync(granjaId, companyId, desdeUtc, hastaUtc, tipoAlimento, ct);

        // Un Exit sellado con ESTOS DOS campos es el espejo de un consumo que el reporte ya publica en
        // sus columnas de consumo: contarlo también como retiro resta los mismos kilos dos veces. Se
        // descarta acá para que ni siquiera viaje (la BD filtra, el backend orquesta); el criterio es
        // el mismo que EsConsumoYaContabilizadoPorSeguimiento y se compara igual (Trim + minúsculas),
        // porque EF traduce Trim()/ToLower() a SQL pero no una llamada a ese método.
        var reasonConsumo = ReporteContableBultosCalculos.ReasonConsumoDiario.ToLower();
        var destinoConsumo = ReporteContableBultosCalculos.DestinoConsumo.ToLower();

        var queryMovimientos = _ctx.FarmInventoryMovements
            .AsNoTracking()
            .Where(m => m.FarmId == granjaId &&
                        m.CompanyId == companyId &&
                        !(m.Reason != null && m.Destination != null &&
                          m.Reason.Trim().ToLower() == reasonConsumo &&
                          m.Destination.Trim().ToLower() == destinoConsumo) &&
                        (m.ItemType != null && m.ItemType != ""
                            ? m.ItemType.Trim().ToLower()
                            : m.CatalogItem.ItemType.Trim().ToLower()) == tipoAlimento &&
                        m.CatalogItem.Activo &&
                        m.CatalogItem.CompanyId == companyId &&
                        m.CreatedAt >= desdeUtc &&
                        m.CreatedAt < hastaUtc);

        // Mismo filtro por país que aplicaba GetPagedAsync (condicional: solo si la sesión lo trae)
        var paisId = _currentUser?.PaisId ?? 0;
        if (paisId > 0)
            queryMovimientos = queryMovimientos.Where(m => m.PaisId == paisId);

        var movimientosAlimento = await queryMovimientos
            .Select(m => new
            {
                m.CreatedAt,
                m.Quantity,
                m.Unit,
                MovementType = m.MovementType.ToString()
            })
            .ToListAsync(ct);

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
    /// ¿Esta empresa declaró que sus reportes leen el alimento del inventario unificado? La decisión
    /// es de <see cref="ReporteAlimentoInventarioCalculos"/>; acá solo se resuelve el dato.
    /// Fail-closed: si la empresa no existe, se lee la tabla de siempre.
    /// </summary>
    private async Task<bool> LeeInventarioUnificadoAsync(int companyId, CancellationToken ct)
    {
        var flag = await _ctx.Companies.AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => c.ReportesAlimentoDesdeInventarioUnificado)
            .FirstOrDefaultAsync(ct);

        return ReporteAlimentoInventarioCalculos.LeeInventarioUnificado(flag);
    }

    /// <summary>
    /// Mismo resultado que la consulta histórica —fecha, entradas, traslados y retiros en BULTOS—
    /// pero leyendo <c>inventario_gestion_movimiento</c>.
    ///
    /// <para>
    /// Dos diferencias que NO son opcionales: el tipo de ítem se resuelve contra
    /// <c>item_inventario_ecuador</c> (el catálogo del módulo nuevo, no <c>catalogo_items</c>), y el
    /// <c>movement_type</c> es un texto, no el enum viejo, así que la traducción a las tres
    /// categorías vive en la clase de cálculo con sus tests.
    /// </para>
    ///
    /// <para>
    /// El silo NO participa: el grano del reporte es (granja, fecha) y el movimiento trae su cantidad
    /// completa, así que llevar el saldo por silo no multiplica ni una fila.
    /// </para>
    /// </summary>
    private async Task<List<(DateTime Fecha, decimal SaldoAnterior, decimal Traslados, decimal Entradas, decimal Retiros, decimal ConsumoHembras, decimal ConsumoMachos)>>
        ObtenerDatosBultosUnificadoAsync(
        int granjaId,
        int companyId,
        DateTimeOffset desdeUtc,
        DateTimeOffset hastaUtc,
        string tipoAlimento,
        CancellationToken ct)
    {
        var datos = new List<(DateTime, decimal, decimal, decimal, decimal, decimal, decimal)>();

        var query = _ctx.InventarioGestionMovimientos
            .AsNoTracking()
            .Where(m => m.FarmId == granjaId &&
                        m.CompanyId == companyId &&
                        m.CreatedAt >= desdeUtc &&
                        m.CreatedAt < hastaUtc)
            .Join(_ctx.ItemInventario.AsNoTracking(),
                m => m.ItemInventarioEcuadorId,
                i => i.Id,
                (m, i) => new { m, i })
            .Where(x => x.i.TipoItem.Trim().ToLower() == tipoAlimento && x.i.Activo);

        // Mismo filtro por país condicional que la rama vieja.
        var paisId = _currentUser?.PaisId ?? 0;
        if (paisId > 0) query = query.Where(x => x.m.PaisId == paisId);

        var movimientos = await query
            .Select(x => new { x.m.CreatedAt, x.m.Quantity, x.m.Unit, x.m.MovementType })
            .ToListAsync(ct);

        foreach (var grupo in movimientos.GroupBy(m => m.CreatedAt.Date))
        {
            decimal Bultos(CategoriaMovimientoAlimento categoria) => grupo
                .Where(m => ReporteAlimentoInventarioCalculos.Categoria(m.MovementType) == categoria)
                .Sum(m => ReporteAlimentoInventarioCalculos.ABultos(m.Quantity, m.Unit, FACTOR_CONVERSION_BULTO_KG));

            datos.Add((grupo.Key, 0,
                Bultos(CategoriaMovimientoAlimento.Traslado),
                Bultos(CategoriaMovimientoAlimento.Entrada),
                Bultos(CategoriaMovimientoAlimento.Retiro),
                0, 0));
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
        bool retirosYaTraenElConsumo,
        CancellationToken ct)
    {
        var datosConSaldos = new List<DatoDiarioContableDto>();
        var saldosPorLote = new Dictionary<int, (int hembras, int machos)>();

        // Inicializar saldos con entradas iniciales
        foreach (var (loteId, (hembras, machos)) in entradasIniciales)
        {
            saldosPorLote[loteId] = (hembras, machos);
        }

        // Agrupar datos por fecha: el saldo de bultos se acumula en orden cronológico, no por lote
        var datosPorFecha = datosDiarios
            .GroupBy(d => d.Fecha)
            .OrderBy(g => g.Key)
            .SelectMany(g => g)
            .ToList();

        // Saldo de bultos: cálculo puro (mismo algoritmo que vivía acá, ahora testeable)
        // `DeltaDelSaldo` decide qué términos entran según de qué módulo venga el kardex: en el
        // unificado los `retiros` YA son el consumo diario de la granja, así que restar además el
        // consumo del seguimiento descontaría el de este padre dos veces. Ver su doc.
        var saldosBultos = ReporteContableBultosCalculos.AcumularSaldos(
            datosPorFecha.Select(d => (
                d.Fecha,
                ReporteContableBultosCalculos.DeltaDelSaldo(
                    new ReporteContableBultosCalculos.DeltaBultosFila(
                        d.EntradasBultos,
                        d.TrasladosBultos,
                        d.RetirosBultos,
                        d.ConsumoBultosHembras,
                        d.ConsumoBultosMachos),
                    retirosYaTraenElConsumo))));

        for (var i = 0; i < datosPorFecha.Count; i++)
        {
            var dato = datosPorFecha[i];
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

            var datoConSaldo = dato with
            {
                SaldoHembras = Math.Max(0, saldoHActual),
                SaldoMachos = Math.Max(0, saldoMActual),
                SaldoBultosAnterior = saldosBultos[i].SaldoAnterior,
                SaldoBultos = saldosBultos[i].Saldo
            };

            datosConSaldos.Add(datoConSaldo);
        }

        return datosConSaldos;
    }

    /// <summary>
    /// Obtiene el saldo anterior de una semana (saldo final de la semana anterior).
    /// <para>
    /// Las aves se leen del último día de la semana anterior CON dato del lote: una fila solo-bultos
    /// (kardex de alimento sin registro del lote) no describe el inventario de aves de la familia de
    /// lotes, así que no puede definir ese día. Los bultos, en cambio, sí se leen del último día con
    /// cualquier movimiento — es su kardex. Sin filas solo-bultos ambas fechas coinciden y el
    /// resultado es idéntico al histórico.
    /// </para>
    /// </summary>
    private (int hembras, int machos, decimal bultos) ObtenerSaldoAnteriorSemana(
        int semanaActual,
        List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> semanasContables,
        List<DatoDiarioContableDto> datosConSaldos,
        Dictionary<int, (int hembras, int machos)> entradasIniciales,
        Func<DatoDiarioContableDto, bool> esFilaSoloBultos)
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

        var datosSemanaAnterior = datosConSaldos
            .Where(d => d.Fecha >= semanaAnterior.FechaInicio && d.Fecha <= semanaAnterior.FechaFin)
            .ToList();

        // Bultos: último día con movimiento (incluye las filas solo-bultos, son su kardex)
        var ultimaFechaBultos = datosSemanaAnterior
            .Select(d => d.Fecha)
            .DefaultIfEmpty(default)
            .Max();

        var saldoBultos = ultimaFechaBultos == default(DateTime)
            ? 0
            : datosConSaldos
                .Where(d => d.Fecha == ultimaFechaBultos)
                .Max(d => (decimal?)d.SaldoBultos) ?? 0;

        // Aves: último día con dato del lote y suma de los saldos de esa fecha
        var ultimaFechaSemanaAnterior = datosSemanaAnterior
            .Where(d => !esFilaSoloBultos(d))
            .Select(d => d.Fecha)
            .DefaultIfEmpty(default)
            .Max();

        if (ultimaFechaSemanaAnterior == default(DateTime))
            return (0, 0, saldoBultos);

        var ultimosDatos = datosConSaldos
            .Where(d => d.Fecha == ultimaFechaSemanaAnterior && !esFilaSoloBultos(d))
            .ToList();

        var totalH = ultimosDatos.Sum(d => d.SaldoHembras);
        var totalM = ultimosDatos.Sum(d => d.SaldoMachos);

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

        // Alcance = lote padre + sublotes. La topología nueva (cierre de levante → LPP) no crea
        // lotes hijos: el padre es el lote operativo y registra su propia producción; y un padre
        // con hijos también puede tener seguimiento y traslados propios (mismo criterio
        // padre+hijos que ya usa el flujo por semana contable de más abajo).
        var lotesReporte = new[] { new { lotePadre.LoteId, lotePadre.LoteNombre } }
            .Concat(sublotes)
            .ToList();

        var loteIds = lotesReporte.Select(s => s.LoteId?.ToString() ?? string.Empty)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var loteIdsInt = lotesReporte.Where(s => s.LoteId.HasValue).Select(s => s.LoteId!.Value).ToList();

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
            var primeraFechaLegacy = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && lotesIdsStr.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            var primeraFechaNueva = await _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => lotesIds.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            var primeraFecha = ReporteContableHuevosCalculos.MenorFechaNoDefault(
                primeraFechaLegacy, primeraFechaNueva);

            if (primeraFecha == default)
                throw new InvalidOperationException("No se encontraron registros de producción para calcular fechas");

            fechaInicio = primeraFecha.Date.AddDays((semana - 1) * 7);
            fechaFin = fechaInicio.AddDays(6);
        }
        else
        {
            // Usar todas las fechas disponibles combinando la tabla legacy seguimiento_diario
            // (tipo produccion) con la canónica seguimiento_diario_produccion.
            var loteIdsStrProd = loteIdsInt.Select(id => id.ToString()).ToList();
            var primeraFechaLegacy = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && loteIdsStrProd.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            var ultimaFechaLegacy = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == "produccion" && loteIdsStrProd.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderByDescending(f => f)
                .FirstOrDefaultAsync(ct);

            var primeraFechaNueva = await _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => loteIdsInt.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderBy(f => f)
                .FirstOrDefaultAsync(ct);

            var ultimaFechaNueva = await _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => loteIdsInt.Contains(s.LoteId))
                .Select(s => s.Fecha)
                .OrderByDescending(f => f)
                .FirstOrDefaultAsync(ct);

            var primeraFecha = ReporteContableHuevosCalculos.MenorFechaNoDefault(
                primeraFechaLegacy, primeraFechaNueva);
            var ultimaFecha = ReporteContableHuevosCalculos.MayorFechaNoDefault(
                ultimaFechaLegacy, ultimaFechaNueva);

            if (primeraFecha == default)
                throw new InvalidOperationException("No se encontraron registros de producción");

            fechaInicio = primeraFecha.Date;
            fechaFin = ultimaFecha.Date;
        }

        // Seguimientos diarios de producción: fuente legacy (seguimiento_diario, tipo produccion)
        // UNION seguimiento_diario_produccion, deduplicadas por (lote, día calendario) con el
        // criterio canónico de las fns de producción: gana el registro de timestamp más temprano.
        // Rango superior EXCLUSIVO al día siguiente y sin `.Date` en el predicado (EF lo traduce
        // a date_trunc dependiente de la TZ de la sesión — gotcha FechasPuras): las filas
        // canónicas van ancladas a MEDIODÍA y `<= fechaFin` (medianoche) cortaba el último día.
        var finExclusivo = fechaFin.Date.AddDays(1);
        var loteIdsStrSeguimientos = loteIdsInt.Select(id => id.ToString()).ToList();
        var seguimientosLegacyRaw = await _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == "produccion" &&
                        loteIdsStrSeguimientos.Contains(s.LoteId) &&
                        s.Fecha >= fechaInicio &&
                        s.Fecha < finExclusivo)
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

        var seguimientosNuevosRaw = await _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => loteIdsInt.Contains(s.LoteId) &&
                        s.Fecha >= fechaInicio &&
                        s.Fecha < finExclusivo)
            .Select(s => new
            {
                s.LoteId,
                s.Fecha,
                s.HuevoTot,
                s.HuevoInc,
                s.HuevoLimpio,
                s.HuevoTratado,
                s.HuevoSucio,
                s.HuevoDeforme,
                s.HuevoBlanco,
                s.HuevoDobleYema,
                s.HuevoPiso,
                s.HuevoPequeno,
                s.HuevoRoto,
                s.HuevoDesecho,
                s.HuevoOtro
            })
            .ToListAsync(ct);

        var seguimientos = ReporteContableHuevosCalculos.MergeDualFuentePorDia(
            seguimientosLegacyRaw
                .Where(s => int.TryParse(s.LoteId, out var lid) && lid > 0)
                .Select(s => new ReporteContableHuevosCalculos.FilaHuevosDia(
                    int.Parse(s.LoteId), s.Fecha, EsLegacy: true,
                    s.HuevoTot, s.HuevoInc, s.HuevoLimpio, s.HuevoTratado, s.HuevoSucio,
                    s.HuevoDeforme, s.HuevoBlanco, s.HuevoDobleYema, s.HuevoPiso,
                    s.HuevoPequeno, s.HuevoRoto, s.HuevoDesecho, s.HuevoOtro))
                .Concat(seguimientosNuevosRaw
                    .Select(s => new ReporteContableHuevosCalculos.FilaHuevosDia(
                        s.LoteId, s.Fecha, EsLegacy: false,
                        s.HuevoTot, s.HuevoInc, s.HuevoLimpio, s.HuevoTratado, s.HuevoSucio,
                        s.HuevoDeforme, s.HuevoBlanco, s.HuevoDobleYema, s.HuevoPiso,
                        s.HuevoPequeno, s.HuevoRoto, s.HuevoDesecho, s.HuevoOtro))));

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

        // Crear diccionario de lotes para nombres (padre + sublotes)
        var lotesDict = lotesReporte.ToDictionary(
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
        IQueryable<Lote> q = _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Include(l => l.LotePosturaBase)
            .Where(l => l.LotePadreId == null &&
                        l.CompanyId == _currentUser.CompanyId &&
                        l.DeletedAt == null);

        // Alcance granular: el árbol granja→núcleo→galpón→lote base se construye a partir de los
        // lotes, así que basta podar los lotes NO permitidos de las granjas restringidas: los
        // núcleos/galpones sin lotes visibles (y las granjas con scope vacío) desaparecen solos.
        // lote_id es PK global ⇒ la unión entre granjas es exacta. Sin restricciones no filtra nada.
        var restringidos = await _scopeResolver.GetAllRestrictedScopesAsync();
        if (restringidos.Count > 0)
        {
            var granjasRestringidas = restringidos.Keys.ToList();
            var lotesPermitidos = restringidos.SelectMany(kv => kv.Value.LotesPermitidos).ToList();
            q = q.Where(l => !granjasRestringidas.Contains(l.GranjaId) ||
                             (l.LoteId != null && lotesPermitidos.Contains(l.LoteId.Value)));
        }

        var lotes = await q
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

