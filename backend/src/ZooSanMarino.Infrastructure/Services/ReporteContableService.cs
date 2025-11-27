// src/ZooSanMarino.Infrastructure/Services/ReporteContableService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ReporteContableService : IReporteContableService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public ReporteContableService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<ReporteContableCompletoDto> GenerarReporteAsync(
        GenerarReporteContableRequestDto request,
        CancellationToken ct = default)
    {
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

        // Calcular semanas contables
        var semanasContables = CalcularSemanasContables(fechaPrimeraLlegada, DateTime.Today);

        // Filtrar por semana contable si se especifica
        var semanasAFiltrar = request.SemanaContable.HasValue
            ? semanasContables.Where(s => s.Semana == request.SemanaContable.Value).ToList()
            : semanasContables;

        // Obtener consumos diarios de todos los lotes
        var consumosDiarios = await ObtenerConsumosDiariosAsync(todosLotes, ct);

        // Agrupar por semana contable y consolidar
        var reportesSemanales = semanasAFiltrar.Select(semana => 
        {
            var consumosSemana = consumosDiarios
                .Where(c => c.Fecha >= semana.FechaInicio && c.Fecha <= semana.FechaFin)
                .ToList();

            return ConsolidarSemanaContable(
                semana.Semana,
                semana.FechaInicio,
                semana.FechaFin,
                request.LotePadreId,
                lotePadre.LoteNombre ?? string.Empty,
                sublotes.Select(s => s.LoteNombre ?? string.Empty).ToList(),
                consumosSemana
            );
        }).ToList();

        // Obtener semana contable actual
        var semanaActual = semanasContables
            .Where(s => s.FechaInicio <= DateTime.Today && s.FechaFin >= DateTime.Today)
            .FirstOrDefault();

        // Si no hay semana actual, usar la primera semana
        var semanaActualFinal = semanaActual.Semana == 0 
            ? semanasContables.FirstOrDefault() 
            : semanaActual;

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
    /// Calcula las semanas contables desde la fecha de primera llegada hasta hoy
    /// La semana contable inicia cuando llega el primer lote y dura 7 días calendario
    /// </summary>
    private List<(int Semana, DateTime FechaInicio, DateTime FechaFin)> CalcularSemanasContables(
        DateTime fechaPrimeraLlegada, 
        DateTime fechaHasta)
    {
        var semanas = new List<(int Semana, DateTime FechaInicio, DateTime FechaFin)>();
        var fechaInicio = fechaPrimeraLlegada.Date;
        var semana = 1;

        while (fechaInicio <= fechaHasta)
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

        // Obtener consumos de levante
        var consumosLevante = await _ctx.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => loteIds.Contains(s.LoteId) &&
                       s.FechaRegistro.Date >= fechaMinima)
            .Select(s => new ConsumoDiarioContableDto
            {
                Fecha = s.FechaRegistro.Date,
                LoteId = s.LoteId,
                LoteNombre = s.Lote.LoteNombre ?? string.Empty,
                ConsumoAlimento = (decimal)(s.ConsumoKgHembras + (s.ConsumoKgMachos ?? 0)),
                ConsumoAgua = 0, // TODO: Obtener de donde se almacene el consumo de agua
                ConsumoMedicamento = 0, // TODO: Obtener de donde se almacene el consumo de medicamento
                ConsumoVacuna = 0, // TODO: Obtener de donde se almacene el consumo de vacuna
                OtrosConsumos = 0, // TODO: Obtener otros consumos
                TotalConsumo = (decimal)(s.ConsumoKgHembras + (s.ConsumoKgMachos ?? 0))
            })
            .ToListAsync(ct);

        consumos.AddRange(consumosLevante);

        // Obtener consumos de producción
        var loteIdsString = loteIds.Select(id => id.ToString()).ToList();
        var consumosProduccionRaw = await _ctx.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => loteIdsString.Contains(s.LoteId) &&
                       s.Fecha.Date >= fechaMinima)
            .ToListAsync(ct);

        // Obtener nombres de lotes para producción
        // ProduccionLote usa LoteId como string, necesitamos obtener el nombre desde la tabla lotes
        var lotesProduccionDict = new Dictionary<string, string>();
        foreach (var loteIdStr in loteIdsString)
        {
            if (int.TryParse(loteIdStr, out var loteIdInt))
            {
                var lote = await _ctx.Lotes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LoteId == loteIdInt, ct);
                if (lote != null)
                {
                    lotesProduccionDict[loteIdStr] = lote.LoteNombre ?? string.Empty;
                }
            }
        }

        var consumosProduccion = consumosProduccionRaw.Select(s => new ConsumoDiarioContableDto
        {
            Fecha = s.Fecha.Date,
            LoteId = int.TryParse(s.LoteId, out var id) ? id : 0,
            LoteNombre = lotesProduccionDict.TryGetValue(s.LoteId, out var nombre) ? nombre : string.Empty,
            ConsumoAlimento = s.ConsKgH + s.ConsKgM,
            ConsumoAgua = 0, // TODO: Obtener de donde se almacene el consumo de agua
            ConsumoMedicamento = 0, // TODO: Obtener de donde se almacene el consumo de medicamento
            ConsumoVacuna = 0, // TODO: Obtener de donde se almacene el consumo de vacuna
            OtrosConsumos = 0, // TODO: Obtener otros consumos
            TotalConsumo = s.ConsKgH + s.ConsKgM
        }).ToList();

        consumos.AddRange(consumosProduccion);

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
    /// Consolida los consumos de una semana contable
    /// </summary>
    private ReporteContableSemanalDto ConsolidarSemanaContable(
        int semanaContable,
        DateTime fechaInicio,
        DateTime fechaFin,
        int lotePadreId,
        string lotePadreNombre,
        List<string> sublotes,
        List<ConsumoDiarioContableDto> consumosDiarios)
    {
        return new ReporteContableSemanalDto
        {
            SemanaContable = semanaContable,
            FechaInicio = fechaInicio,
            FechaFin = fechaFin,
            LotePadreId = lotePadreId,
            LotePadreNombre = lotePadreNombre,
            Sublotes = sublotes,
            ConsumoTotalAlimento = consumosDiarios.Sum(c => c.ConsumoAlimento),
            ConsumoTotalAgua = consumosDiarios.Sum(c => c.ConsumoAgua),
            ConsumoTotalMedicamento = consumosDiarios.Sum(c => c.ConsumoMedicamento),
            ConsumoTotalVacuna = consumosDiarios.Sum(c => c.ConsumoVacuna),
            OtrosConsumos = consumosDiarios.Sum(c => c.OtrosConsumos),
            TotalGeneral = consumosDiarios.Sum(c => c.TotalConsumo),
            ConsumosDiarios = consumosDiarios.OrderBy(c => c.Fecha).ToList()
        };
    }

    #endregion
}

