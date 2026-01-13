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
        List<Lote> sublotes;
        
        // Si se proporciona loteId, usar lógica de lote padre
        if (request.LoteId.HasValue)
        {
            var loteSeleccionado = await _ctx.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == request.LoteId.Value && 
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (loteSeleccionado == null)
                throw new InvalidOperationException($"Lote con ID {request.LoteId.Value} no encontrado");
            
            // Si el lote seleccionado es un lote padre, traer todos sus hijos
            if (loteSeleccionado.LotePadreId == null)
            {
                // Es un lote padre, traer todos los lotes que tienen este como padre
                sublotes = await _ctx.Lotes
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == request.LoteId.Value &&
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
        else if (!string.IsNullOrWhiteSpace(request.LoteNombreBase))
        {
            // Lógica antigua: buscar por nombre base (compatibilidad hacia atrás)
            sublotes = await _ctx.Lotes
                .AsNoTracking()
                .Where(l => l.LoteNombre.StartsWith(request.LoteNombreBase) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }
        else
        {
            throw new ArgumentException("LoteId o LoteNombreBase es requerido para reporte consolidado");
        }

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
                sublote.LoteId?.ToString() ?? string.Empty,
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

        // CORRECCIÓN: Si la fechaInicioProduccion es posterior a las fechas de los registros,
        // usar la fecha del primer registro como referencia para calcular la edad correctamente
        var fechaReferencia = fechaInicioProduccion;
        if (seguimientos.Any())
        {
            var primeraFecha = seguimientos.Min(s => s.Fecha);
            // Si la fecha de inicio es posterior a la primera fecha de registro, usar la primera fecha
            // Esto corrige el problema cuando la fecha de inicio está mal configurada
            if (fechaInicioProduccion.Date > primeraFecha.Date)
            {
                fechaReferencia = primeraFecha.Date;
            }
        }

        var datosDiarios = new List<ReporteTecnicoProduccionDiarioDto>();
        var saldoHembras = avesInicialesH;
        var saldoMachos = avesInicialesM;

        foreach (var seg in seguimientos)
        {
            var edadDias = CalcularEdadDias(fechaReferencia, seg.Fecha);
            var semana = CalcularSemana(edadDias);

            // Obtener ventas y traslados del día
            var (ventasH, ventasM, trasladosH, trasladosM) = await ObtenerVentasYTrasladosAsync(
                int.Parse(loteId), seg.Fecha, ct);

            // Obtener huevos enviados a planta
            var huevosEnviadosPlanta = await ObtenerHuevosEnviadosPlantaAsync(loteId, seg.Fecha, ct);

            // Obtener transferencias de huevos del día
            var (huevosTrasladadosTotal, huevosTrasladadosLimpio, huevosTrasladadosTratado, 
                 huevosTrasladadosSucio, huevosTrasladadosDeforme, huevosTrasladadosBlanco,
                 huevosTrasladadosDobleYema, huevosTrasladadosPiso, huevosTrasladadosPequeno,
                 huevosTrasladadosRoto, huevosTrasladadosDesecho, huevosTrasladadosOtro) = 
                await ObtenerTransferenciasHuevosAsync(loteId, seg.Fecha, ct);

            // Actualizar saldos
            saldoHembras = saldoHembras - seg.MortalidadH - seg.SelH - ventasH - trasladosH;
            saldoMachos = saldoMachos - seg.MortalidadM - ventasM - trasladosM;

            // Calcular porcentajes
            var totalAves = saldoHembras + saldoMachos;
            var porcentajePostura = totalAves > 0 ? (decimal)seg.HuevoTot / totalAves * 100 : 0;
            var porcentajeIncubable = seg.HuevoTot > 0 ? (decimal)seg.HuevoInc / seg.HuevoTot * 100 : 0;

            // Calcular porcentajes de tipos de huevos
            var porcentajeLimpio = seg.HuevoTot > 0 ? (decimal?)seg.HuevoLimpio / seg.HuevoTot * 100 : null;
            var porcentajeTratado = seg.HuevoTot > 0 ? (decimal?)seg.HuevoTratado / seg.HuevoTot * 100 : null;
            var porcentajeSucio = seg.HuevoTot > 0 ? (decimal?)seg.HuevoSucio / seg.HuevoTot * 100 : null;
            var porcentajeDeforme = seg.HuevoTot > 0 ? (decimal?)seg.HuevoDeforme / seg.HuevoTot * 100 : null;
            var porcentajeBlanco = seg.HuevoTot > 0 ? (decimal?)seg.HuevoBlanco / seg.HuevoTot * 100 : null;
            var porcentajeDobleYema = seg.HuevoTot > 0 ? (decimal?)seg.HuevoDobleYema / seg.HuevoTot * 100 : null;
            var porcentajePiso = seg.HuevoTot > 0 ? (decimal?)seg.HuevoPiso / seg.HuevoTot * 100 : null;
            var porcentajePequeno = seg.HuevoTot > 0 ? (decimal?)seg.HuevoPequeno / seg.HuevoTot * 100 : null;
            var porcentajeRoto = seg.HuevoTot > 0 ? (decimal?)seg.HuevoRoto / seg.HuevoTot * 100 : null;
            var porcentajeDesecho = seg.HuevoTot > 0 ? (decimal?)seg.HuevoDesecho / seg.HuevoTot * 100 : null;
            var porcentajeOtro = seg.HuevoTot > 0 ? (decimal?)seg.HuevoOtro / seg.HuevoTot * 100 : null;

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
                PesoHuevo: seg.PesoHuevo,
                // Desglose de tipos de huevos
                HuevoLimpio: seg.HuevoLimpio,
                HuevoTratado: seg.HuevoTratado,
                HuevoSucio: seg.HuevoSucio,
                HuevoDeforme: seg.HuevoDeforme,
                HuevoBlanco: seg.HuevoBlanco,
                HuevoDobleYema: seg.HuevoDobleYema,
                HuevoPiso: seg.HuevoPiso,
                HuevoPequeno: seg.HuevoPequeno,
                HuevoRoto: seg.HuevoRoto,
                HuevoDesecho: seg.HuevoDesecho,
                HuevoOtro: seg.HuevoOtro,
                // Porcentajes de tipos de huevos
                PorcentajeLimpio: porcentajeLimpio,
                PorcentajeTratado: porcentajeTratado,
                PorcentajeSucio: porcentajeSucio,
                PorcentajeDeforme: porcentajeDeforme,
                PorcentajeBlanco: porcentajeBlanco,
                PorcentajeDobleYema: porcentajeDobleYema,
                PorcentajePiso: porcentajePiso,
                PorcentajePequeno: porcentajePequeno,
                PorcentajeRoto: porcentajeRoto,
                PorcentajeDesecho: porcentajeDesecho,
                PorcentajeOtro: porcentajeOtro,
                // Transferencias de huevos
                HuevosTrasladadosTotal: huevosTrasladadosTotal,
                HuevosTrasladadosLimpio: huevosTrasladadosLimpio,
                HuevosTrasladadosTratado: huevosTrasladadosTratado,
                HuevosTrasladadosSucio: huevosTrasladadosSucio,
                HuevosTrasladadosDeforme: huevosTrasladadosDeforme,
                HuevosTrasladadosBlanco: huevosTrasladadosBlanco,
                HuevosTrasladadosDobleYema: huevosTrasladadosDobleYema,
                HuevosTrasladadosPiso: huevosTrasladadosPiso,
                HuevosTrasladadosPequeno: huevosTrasladadosPequeno,
                HuevosTrasladadosRoto: huevosTrasladadosRoto,
                HuevosTrasladadosDesecho: huevosTrasladadosDesecho,
                HuevosTrasladadosOtro: huevosTrasladadosOtro
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

    private async Task<(int total, int limpio, int tratado, int sucio, int deforme, int blanco, 
                        int dobleYema, int piso, int pequeno, int roto, int desecho, int otro)> 
        ObtenerTransferenciasHuevosAsync(string loteId, DateTime fecha, CancellationToken ct)
    {
        var traslados = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LoteId == loteId &&
                       t.FechaTraslado.Date == fecha.Date &&
                       t.Estado == "Completado")
            .ToListAsync(ct);

        var total = traslados.Sum(t => t.TotalHuevos);
        var limpio = traslados.Sum(t => t.CantidadLimpio);
        var tratado = traslados.Sum(t => t.CantidadTratado);
        var sucio = traslados.Sum(t => t.CantidadSucio);
        var deforme = traslados.Sum(t => t.CantidadDeforme);
        var blanco = traslados.Sum(t => t.CantidadBlanco);
        var dobleYema = traslados.Sum(t => t.CantidadDobleYema);
        var piso = traslados.Sum(t => t.CantidadPiso);
        var pequeno = traslados.Sum(t => t.CantidadPequeno);
        var roto = traslados.Sum(t => t.CantidadRoto);
        var desecho = traslados.Sum(t => t.CantidadDesecho);
        var otro = traslados.Sum(t => t.CantidadOtro);

        return (total, limpio, tratado, sucio, deforme, blanco, dobleYema, piso, pequeno, roto, desecho, otro);
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
                PesoHuevo: g.Average(d => d.PesoHuevo),
                // Desglose de tipos de huevos
                HuevoLimpio: g.Sum(d => d.HuevoLimpio),
                HuevoTratado: g.Sum(d => d.HuevoTratado),
                HuevoSucio: g.Sum(d => d.HuevoSucio),
                HuevoDeforme: g.Sum(d => d.HuevoDeforme),
                HuevoBlanco: g.Sum(d => d.HuevoBlanco),
                HuevoDobleYema: g.Sum(d => d.HuevoDobleYema),
                HuevoPiso: g.Sum(d => d.HuevoPiso),
                HuevoPequeno: g.Sum(d => d.HuevoPequeno),
                HuevoRoto: g.Sum(d => d.HuevoRoto),
                HuevoDesecho: g.Sum(d => d.HuevoDesecho),
                HuevoOtro: g.Sum(d => d.HuevoOtro),
                // Porcentajes promedio de tipos de huevos
                PorcentajeLimpio: g.Average(d => d.PorcentajeLimpio),
                PorcentajeTratado: g.Average(d => d.PorcentajeTratado),
                PorcentajeSucio: g.Average(d => d.PorcentajeSucio),
                PorcentajeDeforme: g.Average(d => d.PorcentajeDeforme),
                PorcentajeBlanco: g.Average(d => d.PorcentajeBlanco),
                PorcentajeDobleYema: g.Average(d => d.PorcentajeDobleYema),
                PorcentajePiso: g.Average(d => d.PorcentajePiso),
                PorcentajePequeno: g.Average(d => d.PorcentajePequeno),
                PorcentajeRoto: g.Average(d => d.PorcentajeRoto),
                PorcentajeDesecho: g.Average(d => d.PorcentajeDesecho),
                PorcentajeOtro: g.Average(d => d.PorcentajeOtro),
                // Transferencias de huevos
                HuevosTrasladadosTotal: g.Sum(d => d.HuevosTrasladadosTotal),
                HuevosTrasladadosLimpio: g.Sum(d => d.HuevosTrasladadosLimpio),
                HuevosTrasladadosTratado: g.Sum(d => d.HuevosTrasladadosTratado),
                HuevosTrasladadosSucio: g.Sum(d => d.HuevosTrasladadosSucio),
                HuevosTrasladadosDeforme: g.Sum(d => d.HuevosTrasladadosDeforme),
                HuevosTrasladadosBlanco: g.Sum(d => d.HuevosTrasladadosBlanco),
                HuevosTrasladadosDobleYema: g.Sum(d => d.HuevosTrasladadosDobleYema),
                HuevosTrasladadosPiso: g.Sum(d => d.HuevosTrasladadosPiso),
                HuevosTrasladadosPequeno: g.Sum(d => d.HuevosTrasladadosPequeno),
                HuevosTrasladadosRoto: g.Sum(d => d.HuevosTrasladadosRoto),
                HuevosTrasladadosDesecho: g.Sum(d => d.HuevosTrasladadosDesecho),
                HuevosTrasladadosOtro: g.Sum(d => d.HuevosTrasladadosOtro)
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

        // CORRECCIÓN: Filtrar semanas negativas y asegurar que sean positivas
        var semanas = datosDiarios
            .Where(d => d.Semana > 0 && d.Dia > 0) // Solo días y semanas positivas
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
                // Desglose de tipos de huevos semanal
                HuevoLimpioSemanal: datosSemana.Sum(d => d.HuevoLimpio),
                HuevoTratadoSemanal: datosSemana.Sum(d => d.HuevoTratado),
                HuevoSucioSemanal: datosSemana.Sum(d => d.HuevoSucio),
                HuevoDeformeSemanal: datosSemana.Sum(d => d.HuevoDeforme),
                HuevoBlancoSemanal: datosSemana.Sum(d => d.HuevoBlanco),
                HuevoDobleYemaSemanal: datosSemana.Sum(d => d.HuevoDobleYema),
                HuevoPisoSemanal: datosSemana.Sum(d => d.HuevoPiso),
                HuevoPequenoSemanal: datosSemana.Sum(d => d.HuevoPequeno),
                HuevoRotoSemanal: datosSemana.Sum(d => d.HuevoRoto),
                HuevoDesechoSemanal: datosSemana.Sum(d => d.HuevoDesecho),
                HuevoOtroSemanal: datosSemana.Sum(d => d.HuevoOtro),
                // Porcentajes promedio de tipos de huevos
                PorcentajeLimpioPromedio: datosSemana.Average(d => d.PorcentajeLimpio),
                PorcentajeTratadoPromedio: datosSemana.Average(d => d.PorcentajeTratado),
                PorcentajeSucioPromedio: datosSemana.Average(d => d.PorcentajeSucio),
                PorcentajeDeformePromedio: datosSemana.Average(d => d.PorcentajeDeforme),
                PorcentajeBlancoPromedio: datosSemana.Average(d => d.PorcentajeBlanco),
                PorcentajeDobleYemaPromedio: datosSemana.Average(d => d.PorcentajeDobleYema),
                PorcentajePisoPromedio: datosSemana.Average(d => d.PorcentajePiso),
                PorcentajePequenoPromedio: datosSemana.Average(d => d.PorcentajePequeno),
                PorcentajeRotoPromedio: datosSemana.Average(d => d.PorcentajeRoto),
                PorcentajeDesechoPromedio: datosSemana.Average(d => d.PorcentajeDesecho),
                PorcentajeOtroPromedio: datosSemana.Average(d => d.PorcentajeOtro),
                // Transferencias de huevos semanal
                HuevosTrasladadosTotalSemanal: datosSemana.Sum(d => d.HuevosTrasladadosTotal),
                HuevosTrasladadosLimpioSemanal: datosSemana.Sum(d => d.HuevosTrasladadosLimpio),
                HuevosTrasladadosTratadoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosTratado),
                HuevosTrasladadosSucioSemanal: datosSemana.Sum(d => d.HuevosTrasladadosSucio),
                HuevosTrasladadosDeformeSemanal: datosSemana.Sum(d => d.HuevosTrasladadosDeforme),
                HuevosTrasladadosBlancoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosBlanco),
                HuevosTrasladadosDobleYemaSemanal: datosSemana.Sum(d => d.HuevosTrasladadosDobleYema),
                HuevosTrasladadosPisoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosPiso),
                HuevosTrasladadosPequenoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosPequeno),
                HuevosTrasladadosRotoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosRoto),
                HuevosTrasladadosDesechoSemanal: datosSemana.Sum(d => d.HuevosTrasladadosDesecho),
                HuevosTrasladadosOtroSemanal: datosSemana.Sum(d => d.HuevosTrasladadosOtro),
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
        // Calcular diferencia en días
        var diff = fecha.Date - fechaInicio.Date;
        var diasDiferencia = diff.Days;
        
        // CORRECCIÓN: Si la fecha es anterior a la fecha de inicio, usar valor absoluto
        // Esto puede ocurrir si la fecha de inicio está mal configurada
        // En avicultura: día 1 = día del inicio de producción
        // Si el registro es el mismo día del inicio = día 1
        // Si el registro es 1 día después = día 2
        // Por lo tanto: edad = diferencia + 1
        // Si la diferencia es negativa, usar valor absoluto y sumar 1
        if (diasDiferencia < 0)
        {
            // Si la fecha de inicio es posterior, usar la fecha del registro como día 1
            return 1;
        }
        
        // En avicultura: día 1 = día del inicio
        // Ejemplo: 
        // - Inicio: 02 nov, Registro: 02 nov → diferencia = 0 → edad = 1 día
        // - Inicio: 02 nov, Registro: 03 nov → diferencia = 1 → edad = 2 días
        return Math.Max(1, diasDiferencia + 1);
    }

    private int CalcularSemana(int edadDias)
    {
        // PRODUCCIÓN: Comienza desde la semana 26 (después de las 25 semanas de levante)
        // 7 días = 1 semana
        // Semana 26 = días 1-7 de producción
        // Semana 27 = días 8-14 de producción
        // etc.
        // Asegurar que siempre sea positivo
        if (edadDias < 1)
            return 26; // Mínimo semana 26 para producción
            
        // Calcular semana de producción: 25 semanas de levante + semanas de producción
        var semanasProduccion = (int)Math.Ceiling(edadDias / 7.0);
        return 25 + semanasProduccion;
    }
}

