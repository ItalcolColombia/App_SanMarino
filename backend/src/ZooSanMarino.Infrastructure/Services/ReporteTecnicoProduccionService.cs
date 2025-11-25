// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ReporteTecnicoProduccionService : IReporteTecnicoProduccionService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;

    public ReporteTecnicoProduccionService(ZooSanMarinoContext ctx, ICurrentUser currentUser)
    {
        _ctx = ctx;
        _currentUser = currentUser;
    }

    public async Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteAsync(
        GenerarReporteTecnicoProduccionRequestDto request,
        CancellationToken ct = default)
    {
        if (request.TipoConsolidacion == "consolidado")
        {
            return await GenerarReporteConsolidadoAsync(request, ct);
        }
        else
        {
            if (!request.LoteId.HasValue)
                throw new ArgumentException("LoteId es requerido para reporte por sublote");

            return await GenerarReporteSubloteAsync(request.LoteId.Value, request.FechaInicio, request.FechaFin, ct);
        }
    }

    private async Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteSubloteAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var lote = await _ctx.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId, ct);

        if (lote == null)
            throw new InvalidOperationException($"Lote con ID {loteId} no encontrado");

        // Obtener ProduccionLote para datos iniciales
        var produccionLote = await _ctx.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId == loteId.ToString(), ct);

        var loteInfo = MapearInformacionLote(lote, produccionLote);
        var datosDiarios = await ObtenerDatosDiariosAsync(
            loteId.ToString(),
            produccionLote?.FechaInicio ?? lote.FechaEncaset ?? DateTime.Today,
            fechaInicio,
            fechaFin,
            produccionLote?.AvesInicialesH ?? lote.HembrasL ?? 0,
            produccionLote?.AvesInicialesM ?? lote.MachosL ?? 0,
            ct);

        var datosSemanales = ConsolidarSemanales(datosDiarios, produccionLote?.FechaInicio ?? lote.FechaEncaset ?? DateTime.Today);

        return new ReporteTecnicoProduccionCompletoDto(
            loteInfo,
            datosDiarios,
            datosSemanales
        );
    }

    private async Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteConsolidadoAsync(
        GenerarReporteTecnicoProduccionRequestDto request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.LoteNombreBase))
            throw new ArgumentException("LoteNombreBase es requerido para reporte consolidado");

        // Buscar todos los sublotes del lote base
        var sublotes = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LoteNombre.StartsWith(request.LoteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .OrderBy(l => l.LoteNombre)
            .ToListAsync(ct);

        if (!sublotes.Any())
            throw new InvalidOperationException($"No se encontraron sublotes para el lote {request.LoteNombreBase}");

        var todosDatosDiarios = new List<ReporteTecnicoProduccionDiarioDto>();
        var fechaInicioProduccion = sublotes
            .Select(s => s.FechaEncaset ?? DateTime.Today)
            .Min();

        foreach (var sublote in sublotes)
        {
            var produccionLote = await _ctx.ProduccionLotes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.LoteId == sublote.LoteId.ToString(), ct);

            var datosSublote = await ObtenerDatosDiariosAsync(
                sublote.LoteId.ToString(),
                produccionLote?.FechaInicio ?? sublote.FechaEncaset ?? DateTime.Today,
                request.FechaInicio,
                request.FechaFin,
                produccionLote?.AvesInicialesH ?? sublote.HembrasL ?? 0,
                produccionLote?.AvesInicialesM ?? sublote.MachosL ?? 0,
                ct);

            todosDatosDiarios.AddRange(datosSublote);
        }

        // Consolidar por fecha (sumar datos de todos los sublotes para la misma fecha)
        var datosConsolidados = ConsolidarDatosDiarios(todosDatosDiarios);
        var datosSemanales = await ConsolidarSemanalesConsolidadoAsync(datosConsolidados, fechaInicioProduccion, sublotes, ct);

        // Usar información del primer sublote como base
        var loteBase = sublotes.First();
        var produccionLoteBase = await _ctx.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId == loteBase.LoteId.ToString(), ct);

        var loteInfo = MapearInformacionLote(loteBase, produccionLoteBase);

        return new ReporteTecnicoProduccionCompletoDto(
            loteInfo,
            datosConsolidados,
            datosSemanales
        );
    }

    private async Task<List<ReporteTecnicoProduccionDiarioDto>> ObtenerDatosDiariosAsync(
        string loteId,
        DateTime fechaInicioProduccion,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        int avesInicialesH,
        int avesInicialesM,
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

        var datosDiarios = new List<ReporteTecnicoProduccionDiarioDto>();
        var saldoHembras = avesInicialesH;
        var saldoMachos = avesInicialesM;

        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaInicioProduccion, seg.Fecha);
            var semana = CalcularSemana(edadDias);

            // Obtener ventas y traslados del día
            var (ventasH, ventasM, trasladosH, trasladosM) = await ObtenerVentasYTrasladosAsync(
                int.Parse(loteId), seg.Fecha, ct);

            // Obtener huevos enviados a planta
            var huevosEnviadosPlanta = await ObtenerHuevosEnviadosPlantaAsync(loteId, seg.Fecha, ct);

            // Actualizar saldos
            saldoHembras = saldoHembras - seg.MortalidadH - seg.SelH - ventasH - trasladosH;
            saldoMachos = saldoMachos - seg.MortalidadM - ventasM - trasladosM;

            // Calcular porcentajes
            var totalAves = saldoHembras + saldoMachos;
            var porcentajePostura = totalAves > 0 ? (decimal)seg.HuevoTot / totalAves * 100 : 0;
            var porcentajeIncubable = seg.HuevoTot > 0 ? (decimal)seg.HuevoInc / seg.HuevoTot * 100 : 0;

            var dto = new ReporteTecnicoProduccionDiarioDto(
                Dia: edadDias,
                Semana: semana,
                Fecha: seg.Fecha,
                MortalidadHembras: seg.MortalidadH,
                MortalidadMachos: seg.MortalidadM,
                SeleccionHembras: seg.SelH,
                SeleccionMachos: 0, // No hay selección de machos en producción típicamente
                VentasHembras: ventasH,
                VentasMachos: ventasM,
                TrasladosHembras: trasladosH,
                TrasladosMachos: trasladosM,
                SaldoHembras: saldoHembras,
                SaldoMachos: saldoMachos,
                HuevosTotales: seg.HuevoTot,
                PorcentajePostura: porcentajePostura,
                KilosAlimentoHembras: seg.ConsKgH,
                KilosAlimentoMachos: seg.ConsKgM,
                HuevosEnviadosPlanta: huevosEnviadosPlanta,
                PorcentajeIncubable: porcentajeIncubable,
                PesoHembra: seg.PesoH,
                PesoMachos: seg.PesoM,
                PesoHuevo: seg.PesoHuevo
            );

            datosDiarios.Add(dto);
        }

        return datosDiarios;
    }

    private async Task<(int ventasH, int ventasM, int trasladosH, int trasladosM)> ObtenerVentasYTrasladosAsync(
        int loteId,
        DateTime fecha,
        CancellationToken ct)
    {
        var movimientos = await _ctx.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteId &&
                       m.FechaMovimiento.Date == fecha.Date &&
                       m.Estado == "Completado")
            .ToListAsync(ct);

        var ventasH = movimientos
            .Where(m => m.TipoMovimiento == "Venta")
            .Sum(m => m.CantidadHembras);
        var ventasM = movimientos
            .Where(m => m.TipoMovimiento == "Venta")
            .Sum(m => m.CantidadMachos);

        var trasladosH = movimientos
            .Where(m => m.TipoMovimiento == "Traslado")
            .Sum(m => m.CantidadHembras);
        var trasladosM = movimientos
            .Where(m => m.TipoMovimiento == "Traslado")
            .Sum(m => m.CantidadMachos);

        return (ventasH, ventasM, trasladosH, trasladosM);
    }

    private async Task<int> ObtenerHuevosEnviadosPlantaAsync(string loteId, DateTime fecha, CancellationToken ct)
    {
        var traslados = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LoteId == loteId &&
                       t.FechaTraslado.Date == fecha.Date &&
                       t.TipoDestino == "Planta" &&
                       t.Estado == "Completado")
            .ToListAsync(ct);

        return traslados.Sum(t => t.CantidadLimpio + t.CantidadTratado);
    }

    private List<ReporteTecnicoProduccionDiarioDto> ConsolidarDatosDiarios(
        List<ReporteTecnicoProduccionDiarioDto> todosDatos)
    {
        return todosDatos
            .GroupBy(d => d.Fecha.Date)
            .Select(g => new ReporteTecnicoProduccionDiarioDto(
                Dia: g.First().Dia,
                Semana: g.First().Semana,
                Fecha: g.Key,
                MortalidadHembras: g.Sum(d => d.MortalidadHembras),
                MortalidadMachos: g.Sum(d => d.MortalidadMachos),
                SeleccionHembras: g.Sum(d => d.SeleccionHembras),
                SeleccionMachos: g.Sum(d => d.SeleccionMachos),
                VentasHembras: g.Sum(d => d.VentasHembras),
                VentasMachos: g.Sum(d => d.VentasMachos),
                TrasladosHembras: g.Sum(d => d.TrasladosHembras),
                TrasladosMachos: g.Sum(d => d.TrasladosMachos),
                SaldoHembras: g.Max(d => d.SaldoHembras), // Tomar el último saldo del día
                SaldoMachos: g.Max(d => d.SaldoMachos),
                HuevosTotales: g.Sum(d => d.HuevosTotales),
                PorcentajePostura: g.Average(d => d.PorcentajePostura),
                KilosAlimentoHembras: g.Sum(d => d.KilosAlimentoHembras),
                KilosAlimentoMachos: g.Sum(d => d.KilosAlimentoMachos),
                HuevosEnviadosPlanta: g.Sum(d => d.HuevosEnviadosPlanta),
                PorcentajeIncubable: g.Average(d => d.PorcentajeIncubable),
                PesoHembra: g.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).Average()
                    : null,
                PesoMachos: g.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).Average()
                    : null,
                PesoHuevo: g.Average(d => d.PesoHuevo)
            ))
            .OrderBy(d => d.Fecha)
            .ToList();
    }

    private List<ReporteTecnicoProduccionSemanalDto> ConsolidarSemanales(
        List<ReporteTecnicoProduccionDiarioDto> datosDiarios,
        DateTime? fechaInicioProduccion)
    {
        if (!fechaInicioProduccion.HasValue || !datosDiarios.Any())
            return new List<ReporteTecnicoProduccionSemanalDto>();

        var semanas = datosDiarios
            .GroupBy(d => d.Semana)
            .Where(g => g.Count() >= 7) // Solo semanas completas (7 días)
            .Select(g => new
            {
                Semana = g.Key,
                Datos = g.OrderBy(d => d.Fecha).ToList()
            })
            .OrderBy(s => s.Semana)
            .ToList();

        var datosSemanales = new List<ReporteTecnicoProduccionSemanalDto>();

        foreach (var semana in semanas)
        {
            var datosSemana = semana.Datos;
            var fechaInicio = datosSemana.First().Fecha;
            var fechaFin = datosSemana.Last().Fecha;
            var edadInicio = datosSemana.First().Dia;
            var edadFin = datosSemana.Last().Dia;

            var dto = new ReporteTecnicoProduccionSemanalDto(
                Semana: semana.Semana,
                FechaInicioSemana: fechaInicio,
                FechaFinSemana: fechaFin,
                EdadInicioSemanas: CalcularSemana(edadInicio),
                EdadFinSemanas: CalcularSemana(edadFin),
                MortalidadHembrasSemanal: datosSemana.Sum(d => d.MortalidadHembras),
                MortalidadMachosSemanal: datosSemana.Sum(d => d.MortalidadMachos),
                SeleccionHembrasSemanal: datosSemana.Sum(d => d.SeleccionHembras),
                SeleccionMachosSemanal: datosSemana.Sum(d => d.SeleccionMachos),
                VentasHembrasSemanal: datosSemana.Sum(d => d.VentasHembras),
                VentasMachosSemanal: datosSemana.Sum(d => d.VentasMachos),
                TrasladosHembrasSemanal: datosSemana.Sum(d => d.TrasladosHembras),
                TrasladosMachosSemanal: datosSemana.Sum(d => d.TrasladosMachos),
                SaldoInicioHembras: datosSemana.First().SaldoHembras,
                SaldoFinHembras: datosSemana.Last().SaldoHembras,
                SaldoInicioMachos: datosSemana.First().SaldoMachos,
                SaldoFinMachos: datosSemana.Last().SaldoMachos,
                HuevosTotalesSemanal: datosSemana.Sum(d => d.HuevosTotales),
                PorcentajePosturaPromedio: datosSemana.Average(d => d.PorcentajePostura),
                KilosAlimentoHembrasSemanal: datosSemana.Sum(d => d.KilosAlimentoHembras),
                KilosAlimentoMachosSemanal: datosSemana.Sum(d => d.KilosAlimentoMachos),
                HuevosEnviadosPlantaSemanal: datosSemana.Sum(d => d.HuevosEnviadosPlanta),
                PorcentajeIncubablePromedio: datosSemana.Average(d => d.PorcentajeIncubable),
                PesoHembraPromedio: datosSemana.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).Average()
                    : null,
                PesoMachosPromedio: datosSemana.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).Average()
                    : null,
                PesoHuevoPromedio: datosSemana.Average(d => d.PesoHuevo),
                DetalleDiario: datosSemana
            );

            datosSemanales.Add(dto);
        }

        return datosSemanales;
    }

    private async Task<List<ReporteTecnicoProduccionSemanalDto>> ConsolidarSemanalesConsolidadoAsync(
        List<ReporteTecnicoProduccionDiarioDto> datosConsolidados,
        DateTime fechaInicioProduccion,
        List<Lote> sublotes,
        CancellationToken ct)
    {
        // Para consolidación, solo consolidar semanas completas donde TODOS los sublotes tengan 7 días
        var semanasCompletas = new List<int>();
        
        var semanasUnicas = datosConsolidados.Select(d => d.Semana).Distinct().OrderBy(s => s).ToList();
        
        foreach (var semana in semanasUnicas)
        {
            var esCompleta = await EsSemanaCompletaConsolidadaAsync(semana, sublotes, fechaInicioProduccion, ct);
            if (esCompleta)
                semanasCompletas.Add(semana);
        }

        // Filtrar datos solo para semanas completas
        var datosFiltrados = datosConsolidados
            .Where(d => semanasCompletas.Contains(d.Semana))
            .ToList();

        return ConsolidarSemanales(datosFiltrados, fechaInicioProduccion);
    }

    private async Task<bool> EsSemanaCompletaConsolidadaAsync(
        int semana,
        List<Lote> sublotes,
        DateTime fechaInicioProduccion,
        CancellationToken ct)
    {
        // Verificar que cada sublote tenga al menos 7 días de datos en esa semana
        foreach (var sublote in sublotes)
        {
            var produccionLote = await _ctx.ProduccionLotes
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.LoteId == sublote.LoteId.ToString(), ct);

            var fechaInicioSublote = produccionLote?.FechaInicio ?? sublote.FechaEncaset ?? DateTime.Today;
            
            // Calcular la fecha de inicio de la semana para este sublote
            // La semana se calcula desde la fecha de inicio del sublote
            var edadInicioSemana = (semana - 1) * 7;
            var fechaInicioSemana = fechaInicioSublote.AddDays(edadInicioSemana);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            // Verificar que el sublote tenga al menos 7 días de edad en esa semana
            var edadFinSemana = CalcularEdadDias(fechaInicioSublote, fechaFinSemana);
            if (edadFinSemana < 6) // Menos de 7 días (0-6)
                return false;

            // Verificar que hay datos para toda la semana (7 días)
            var diasConDatos = await _ctx.SeguimientoProduccion
                .AsNoTracking()
                .Where(s => s.LoteId == sublote.LoteId.ToString() &&
                           s.Fecha >= fechaInicioSemana &&
                           s.Fecha <= fechaFinSemana)
                .CountAsync(ct);

            if (diasConDatos < 7)
                return false;
        }

        return true;
    }

    private ReporteTecnicoProduccionLoteInfoDto MapearInformacionLote(Lote lote, ProduccionLote? produccionLote)
    {
        return new ReporteTecnicoProduccionLoteInfoDto(
            LoteId: lote.LoteId ?? 0,
            LoteNombre: lote.LoteNombre,
            Raza: lote.Raza,
            Linea: lote.Linea,
            FechaInicioProduccion: produccionLote?.FechaInicio ?? lote.FechaEncaset,
            NumeroHembrasIniciales: produccionLote?.AvesInicialesH ?? lote.HembrasL,
            NumeroMachosIniciales: produccionLote?.AvesInicialesM ?? lote.MachosL,
            Galpon: lote.GalponId != null ? int.TryParse(lote.GalponId, out var g) ? g : null : null,
            Tecnico: lote.Tecnico,
            GranjaNombre: lote.Farm?.Name,
            NucleoNombre: lote.Nucleo?.NucleoNombre
        );
    }

    public async Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, CancellationToken ct = default)
    {
        var sublotes = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LoteNombre.StartsWith(loteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteNombre)
            .OrderBy(n => n)
            .ToListAsync(ct);

        return sublotes
            .Select(n => ExtraerSublote(n) ?? n)
            .Distinct()
            .ToList();
    }

    private string? ExtraerSublote(string loteNombre)
    {
        var partes = loteNombre.Trim().Split(' ');
        if (partes.Length > 1 && partes[^1].Length == 1)
            return partes[^1];
        return null;
    }

    private int CalcularEdadDias(DateTime fechaInicio, DateTime fecha)
    {
        return (fecha.Date - fechaInicio.Date).Days;
    }

    private int CalcularSemana(int edadDias)
    {
        return (edadDias / 7) + 1;
    }
}

