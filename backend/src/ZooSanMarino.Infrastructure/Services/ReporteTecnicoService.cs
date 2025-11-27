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

    public ReporteTecnicoService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
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

        // Determinar si es levante o producción basado en la edad
        var esLevante = await EsLoteEnLevanteAsync(loteId, ct);
        
        var datosDiarios = esLevante
            ? await ObtenerDatosDiariosLevanteAsync(loteId, lote.FechaEncaset, fechaInicio, fechaFin, ct)
            : await ObtenerDatosDiariosProduccionAsync(loteId.ToString(), lote.FechaEncaset, fechaInicio, fechaFin, ct);

        var datosSemanales = ConsolidarSemanales(datosDiarios, lote.FechaEncaset);

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

            var esLevante = await EsLoteEnLevanteAsync(sublote.LoteId ?? 0, ct);
            var datosSublote = esLevante
                ? await ObtenerDatosDiariosLevanteAsync(sublote.LoteId ?? 0, sublote.FechaEncaset, fechaInicio, fechaFin, ct)
                : await ObtenerDatosDiariosProduccionAsync(sublote.LoteId?.ToString() ?? "", sublote.FechaEncaset, fechaInicio, fechaFin, ct);

            todosDatosDiarios.AddRange(datosSublote);
        }

        // Consolidar por fecha (sumar datos de todos los sublotes para la misma fecha)
        var datosConsolidados = ConsolidarDatosDiarios(todosDatosDiarios);

        // Usar información del primer sublote como base
        var loteBase = sublotes.First();
        var infoLote = MapearInformacionLote(loteBase);
        infoLote.Sublote = null; // Consolidado no tiene sublote específico

        var datosSemanales = ConsolidarSemanales(datosConsolidados, loteBase.FechaEncaset);

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
            ? reporteDiario.DatosSemanales.Where(s => s.Semana == semana.Value).ToList()
            : reporteDiario.DatosSemanales;

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
            var esLevante = await EsLoteEnLevanteAsync(sublote.LoteId ?? 0, ct);
            
            var datosDiarios = esLevante
                ? await ObtenerDatosDiariosLevanteAsync(sublote.LoteId ?? 0, sublote.FechaEncaset, null, null, ct)
                : await ObtenerDatosDiariosProduccionAsync(sublote.LoteId?.ToString() ?? "", sublote.FechaEncaset, null, null, ct);

            var datosSemanales = ConsolidarSemanales(datosDiarios, sublote.FechaEncaset);
            datosSemanalesPorSublote[subloteNombre] = datosSemanales;
        }

        // Consolidar semanas completas (solo si todos los sublotes tienen la semana completa)
        var semanasConsolidadas = ConsolidarSemanasCompletas(datosSemanalesPorSublote, semana);

        var loteBase = sublotes.First();
        var infoLote = MapearInformacionLote(loteBase);
        infoLote.Sublote = null;

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
        // Verificar si tiene registros en levante
        var tieneLevante = await _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .AnyAsync(s => s.LoteId == loteId, ct);

        // Si tiene registros en levante, asumimos que está en levante
        // Si no tiene registros en levante pero tiene en producción, está en producción
        return tieneLevante;
    }

    private async Task<List<ReporteTecnicoDiarioDto>> ObtenerDatosDiariosLevanteAsync(
        int loteId,
        DateTime? fechaEncaset,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var query = _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId);

        if (fechaInicio.HasValue)
            query = query.Where(s => s.FechaRegistro >= fechaInicio.Value);

        if (fechaFin.HasValue)
            query = query.Where(s => s.FechaRegistro <= fechaFin.Value);

        var seguimientos = await query
            .OrderBy(s => s.FechaRegistro)
            .ToListAsync(ct);

        if (!fechaEncaset.HasValue)
            return new List<ReporteTecnicoDiarioDto>();

        var lote = await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId, ct);

        if (lote == null)
            return new List<ReporteTecnicoDiarioDto>();

        var datosDiarios = new List<ReporteTecnicoDiarioDto>();
        var avesIniciales = (lote.HembrasL ?? 0) + (lote.MachosL ?? 0);
        var avesActuales = avesIniciales;
        var mortalidadAcumulada = 0;
        var consumoAcumulado = 0m;
        var errorSexajeAcumulado = 0;
        var descarteAcumulado = 0;
        decimal? pesoAnterior = null;

        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaEncaset.Value, seg.FechaRegistro);
            var edadSemanas = CalcularEdadSemanas(edadDias);

            var mortalidadTotal = seg.MortalidadHembras + seg.MortalidadMachos;
            mortalidadAcumulada += mortalidadTotal;
            avesActuales -= mortalidadTotal;

            var errorSexaje = seg.ErrorSexajeHembras + seg.ErrorSexajeMachos;
            errorSexajeAcumulado += errorSexaje;

            var descarte = seg.SelH + seg.SelM;
            descarteAcumulado += descarte;
            avesActuales -= descarte;

            var consumoKilos = (decimal)seg.ConsumoKgHembras + (decimal)(seg.ConsumoKgMachos ?? 0);
            consumoAcumulado += consumoKilos;

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
                MortalidadPorcentajeDiario = avesActuales > 0 ? (decimal)mortalidadTotal / avesActuales * 100 : 0,
                MortalidadPorcentajeAcumulado = avesIniciales > 0 ? (decimal)mortalidadAcumulada / avesIniciales * 100 : 0,
                ErrorSexajeNumero = errorSexaje,
                ErrorSexajePorcentaje = avesActuales > 0 ? (decimal)errorSexaje / avesActuales * 100 : 0,
                ErrorSexajePorcentajeAcumulado = avesIniciales > 0 ? (decimal)errorSexajeAcumulado / avesIniciales * 100 : 0,
                DescarteNumero = descarte,
                DescartePorcentajeDiario = avesActuales > 0 ? (decimal)descarte / avesActuales * 100 : 0,
                DescartePorcentajeAcumulado = avesIniciales > 0 ? (decimal)descarteAcumulado / avesIniciales * 100 : 0,
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
        DateTime? fechaEncaset)
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

                return new ReporteTecnicoSemanalDto
                {
                    Semana = g.Key,
                    FechaInicio = primeraFecha,
                    FechaFin = ultimaFecha,
                    EdadInicioSemanas = g.Key,
                    EdadFinSemanas = g.Key,
                    AvesInicioSemana = datosSemana.First().NumeroAves,
                    AvesFinSemana = datosSemana.Last().NumeroAves,
                    MortalidadTotalSemana = datosSemana.Sum(d => d.MortalidadTotal),
                    MortalidadPorcentajeSemana = datosSemana.Average(d => d.MortalidadPorcentajeDiario),
                    ConsumoKilosSemana = datosSemana.Sum(d => d.ConsumoKilos),
                    ConsumoGramosPorAveSemana = datosSemana.Average(d => d.ConsumoGramosPorAve),
                    PesoPromedioSemana = datosSemana.Where(d => d.PesoActual.HasValue).Select(d => d.PesoActual!.Value).DefaultIfEmpty(0).Average(),
                    UniformidadPromedioSemana = datosSemana.Where(d => d.Uniformidad.HasValue).Select(d => d.Uniformidad!.Value).DefaultIfEmpty(0).Average(),
                    SeleccionVentasSemana = datosSemana.Sum(d => d.SeleccionVentasNumero),
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

            var consolidado = new ReporteTecnicoSemanalDto
            {
                Semana = semana,
                FechaInicio = primeraFecha,
                FechaFin = ultimaFecha,
                EdadInicioSemanas = semana,
                EdadFinSemanas = semana,
                AvesInicioSemana = datosSemanaPorSublote.Sum(s => s.AvesInicioSemana),
                AvesFinSemana = datosSemanaPorSublote.Sum(s => s.AvesFinSemana),
                MortalidadTotalSemana = datosSemanaPorSublote.Sum(s => s.MortalidadTotalSemana),
                MortalidadPorcentajeSemana = datosSemanaPorSublote.Average(s => s.MortalidadPorcentajeSemana),
                ConsumoKilosSemana = datosSemanaPorSublote.Sum(s => s.ConsumoKilosSemana),
                ConsumoGramosPorAveSemana = datosSemanaPorSublote.Average(s => s.ConsumoGramosPorAveSemana),
                PesoPromedioSemana = datosSemanaPorSublote.Where(s => s.PesoPromedioSemana.HasValue).Select(s => s.PesoPromedioSemana!.Value).DefaultIfEmpty(0).Average(),
                UniformidadPromedioSemana = datosSemanaPorSublote.Where(s => s.UniformidadPromedioSemana.HasValue).Select(s => s.UniformidadPromedioSemana!.Value).DefaultIfEmpty(0).Average(),
                SeleccionVentasSemana = datosSemanaPorSublote.Sum(s => s.SeleccionVentasSemana),
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
        var diff = fechaRegistro.Date - fechaEncaset.Date;
        return Math.Max(1, diff.Days + 1);
    }

    private int CalcularEdadSemanas(int edadDias)
    {
        // 7 días = 1 semana
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
        var ingresos = await _ctx.FarmInventoryMovements
            .AsNoTracking()
            .Include(m => m.CatalogItem)
            .Where(m => m.FarmId == granjaId &&
                       m.CreatedAt.Date == fecha.Date &&
                       (m.MovementType == Domain.Enums.InventoryMovementType.Entry ||
                        m.MovementType == Domain.Enums.InventoryMovementType.TransferIn) &&
                       (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                        m.CatalogItem.Nombre.ToLower().Contains("food") ||
                        m.CatalogItem.Codigo.ToLower().StartsWith("al")))
            .SumAsync(m => m.Quantity, ct);

        return ingresos;
    }

    private async Task<decimal> ObtenerTrasladosAlimentoAsync(int granjaId, DateTime fecha, CancellationToken ct)
    {
        // Obtener traslados de alimentos (TransferOut) del día
        var traslados = await _ctx.FarmInventoryMovements
            .AsNoTracking()
            .Include(m => m.CatalogItem)
            .Where(m => m.FarmId == granjaId &&
                       m.CreatedAt.Date == fecha.Date &&
                       m.MovementType == Domain.Enums.InventoryMovementType.TransferOut &&
                       (m.CatalogItem.Nombre.ToLower().Contains("alimento") ||
                        m.CatalogItem.Nombre.ToLower().Contains("food") ||
                        m.CatalogItem.Codigo.ToLower().StartsWith("al")))
            .SumAsync(m => m.Quantity, ct);

        return traslados;
    }

    #endregion
}

