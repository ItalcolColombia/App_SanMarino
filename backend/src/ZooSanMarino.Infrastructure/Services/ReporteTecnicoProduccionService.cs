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
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public ReporteTecnicoProduccionService(
        ZooSanMarinoContext ctx, 
        ICurrentUser currentUser,
        IGuiaGeneticaService guiaGeneticaService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _guiaGeneticaService = guiaGeneticaService;
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
        int lotePosturaProduccionId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        // lotePosturaProduccionId = lote_postura_produccion.lote_postura_produccion_id
        var lpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == lotePosturaProduccionId &&
                                     l.CompanyId == _currentUser.CompanyId &&
                                     l.DeletedAt == null, ct);

        if (lpp == null)
            throw new InvalidOperationException($"Lote producción con ID {lotePosturaProduccionId} no encontrado");

        var fechaInicioProd = lpp.FechaInicioProduccion ?? lpp.FechaEncaset ?? DateTime.Today;
        var hembras = lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? lpp.HembrasL ?? 0;
        var machos = lpp.AvesMInicial ?? lpp.MachosInicialesProd ?? lpp.MachosL ?? 0;

        var loteInfo = MapearInformacionLoteFromLPP(lpp);
        var datosDiarios = await ObtenerDatosDiariosPorLPPAsync(
            lotePosturaProduccionId,
            lpp.LoteId,
            fechaInicioProd,
            fechaInicio,
            fechaFin,
            hembras,
            machos,
            ct);

        var datosSemanales = ConsolidarSemanales(datosDiarios, fechaInicioProd);

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
        List<LotePosturaProduccion> sublotesLpp;
        
        if (request.LoteId.HasValue)
        {
            // loteId = LotePosturaProduccionId
            var lppSeleccionado = await _ctx.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == request.LoteId.Value &&
                                         l.CompanyId == _currentUser.CompanyId &&
                                         l.DeletedAt == null, ct);
            
            if (lppSeleccionado == null)
                throw new InvalidOperationException($"Lote producción con ID {request.LoteId.Value} no encontrado");
            
            if (lppSeleccionado.LotePadreId == null)
            {
                // Es lote padre, traer hijos LPP
                sublotesLpp = await _ctx.LotePosturaProduccion
                    .AsNoTracking()
                    .Where(l => l.LotePadreId == request.LoteId.Value &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
                sublotesLpp.Insert(0, lppSeleccionado);
            }
            else
            {
                var padreId = lppSeleccionado.LotePadreId.Value;
                sublotesLpp = await _ctx.LotePosturaProduccion
                    .AsNoTracking()
                    .Where(l => (l.LotePadreId == padreId || l.LotePosturaProduccionId == padreId) &&
                               l.CompanyId == _currentUser.CompanyId &&
                               l.DeletedAt == null)
                    .OrderBy(l => l.LoteNombre)
                    .ToListAsync(ct);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.LoteNombreBase))
        {
            sublotesLpp = await _ctx.LotePosturaProduccion
                .AsNoTracking()
                .Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(request.LoteNombreBase) &&
                           l.CompanyId == _currentUser.CompanyId &&
                           l.DeletedAt == null)
                .OrderBy(l => l.LoteNombre)
                .ToListAsync(ct);
        }
        else
        {
            throw new ArgumentException("LoteId o LoteNombreBase es requerido para reporte consolidado");
        }

        if (!sublotesLpp.Any())
            throw new InvalidOperationException($"No se encontraron sublotes para el lote seleccionado");

        var todosDatosDiarios = new List<ReporteTecnicoProduccionDiarioDto>();
        var fechaInicioProduccion = sublotesLpp
            .Select(s => s.FechaInicioProduccion ?? s.FechaEncaset ?? DateTime.Today)
            .Min();

        foreach (var sublote in sublotesLpp)
        {
            var lppId = sublote.LotePosturaProduccionId ?? 0;
            var fechaInicioSublote = sublote.FechaInicioProduccion ?? sublote.FechaEncaset ?? DateTime.Today;
            var hembras = sublote.AvesHInicial ?? sublote.HembrasInicialesProd ?? sublote.HembrasL ?? 0;
            var machos = sublote.AvesMInicial ?? sublote.MachosInicialesProd ?? sublote.MachosL ?? 0;

            var datosSublote = await ObtenerDatosDiariosPorLPPAsync(
                lppId,
                sublote.LoteId,
                fechaInicioSublote,
                request.FechaInicio,
                request.FechaFin,
                hembras,
                machos,
                ct);

            todosDatosDiarios.AddRange(datosSublote);
        }

        var datosConsolidados = ConsolidarDatosDiarios(todosDatosDiarios);
        var datosSemanales = await ConsolidarSemanalesConsolidadoLPPAsync(datosConsolidados, fechaInicioProduccion, sublotesLpp, ct);

        var loteInfo = MapearInformacionLoteFromLPP(sublotesLpp.First());

        return new ReporteTecnicoProduccionCompletoDto(
            loteInfo,
            datosConsolidados,
            datosSemanales
        );
    }

    /// <summary>Registro de seguimiento producción leído desde tabla unificada seguimiento_diario (TipoSeguimiento = produccion).</summary>
    private sealed class SegProduccionParaReporte
    {
        public DateTime Fecha { get; set; }
        public int MortalidadH { get; set; }
        public int MortalidadM { get; set; }
        public int SelH { get; set; }
        public int SelM { get; set; }
        public decimal ConsKgH { get; set; }
        public decimal ConsKgM { get; set; }
        public int HuevoTot { get; set; }
        public int HuevoInc { get; set; }
        public int HuevoLimpio { get; set; }
        public int HuevoTratado { get; set; }
        public int HuevoSucio { get; set; }
        public int HuevoDeforme { get; set; }
        public int HuevoBlanco { get; set; }
        public int HuevoDobleYema { get; set; }
        public int HuevoPiso { get; set; }
        public int HuevoPequeno { get; set; }
        public int HuevoRoto { get; set; }
        public int HuevoDesecho { get; set; }
        public int HuevoOtro { get; set; }
        public decimal? PesoH { get; set; }
        public decimal? PesoM { get; set; }
        public decimal PesoHuevo { get; set; }
    }

    private const string TipoProduccion = "produccion";

    /// <summary>Obtiene seguimientos de producción por lote_postura_produccion_id (seguimiento_diario).</summary>
    private async Task<List<SegProduccionParaReporte>> ObtenerSeguimientosProduccionPorLPPAsync(
        int lotePosturaProduccionId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var query = _ctx.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoProduccion && s.LotePosturaProduccionId == lotePosturaProduccionId);

        if (fechaInicio.HasValue)
            query = query.Where(s => s.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)
            query = query.Where(s => s.Fecha <= fechaFin.Value);

        return await query
            .OrderBy(s => s.Fecha)
            .Select(s => new SegProduccionParaReporte
            {
                Fecha = s.Fecha,
                MortalidadH = s.MortalidadHembras ?? 0,
                MortalidadM = s.MortalidadMachos ?? 0,
                SelH = s.SelH ?? 0,
                SelM = s.SelM ?? 0,
                ConsKgH = s.ConsumoKgHembras ?? 0,
                ConsKgM = s.ConsumoKgMachos ?? 0,
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
                HuevoOtro = s.HuevoOtro ?? 0,
                PesoH = s.PesoH,
                PesoM = s.PesoM,
                PesoHuevo = (decimal)(s.PesoHuevo ?? 0)
            })
            .ToListAsync(ct);
    }

    private async Task<List<ReporteTecnicoProduccionDiarioDto>> ObtenerDatosDiariosPorLPPAsync(
        int lotePosturaProduccionId,
        int? loteOrigenId,
        DateTime fechaInicioProduccion,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        int avesInicialesH,
        int avesInicialesM,
        CancellationToken ct)
    {
        var seguimientos = await ObtenerSeguimientosProduccionPorLPPAsync(lotePosturaProduccionId, fechaInicio, fechaFin, ct);

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

            // Obtener ventas y traslados del día (MovimientoAves usa LoteOrigenId = lotes.LoteId)
            (int ventasH, int ventasM, int trasladosH, int trasladosM) = await ObtenerVentasYTrasladosAsync(
                loteOrigenId, seg.Fecha, ct);

            // Obtener huevos enviados a planta (TrasladoHuevos usa LotePosturaProduccionId)
            var huevosEnviadosPlanta = await ObtenerHuevosEnviadosPlantaPorLPPAsync(lotePosturaProduccionId, seg.Fecha, ct);

            // Obtener transferencias de huevos del día
            (int huevosTrasladadosTotal, int huevosTrasladadosLimpio, int huevosTrasladadosTratado,
             int huevosTrasladadosSucio, int huevosTrasladadosDeforme, int huevosTrasladadosBlanco,
             int huevosTrasladadosDobleYema, int huevosTrasladadosPiso, int huevosTrasladadosPequeno,
             int huevosTrasladadosRoto, int huevosTrasladadosDesecho, int huevosTrasladadosOtro) =
                await ObtenerTransferenciasHuevosPorLPPAsync(lotePosturaProduccionId, seg.Fecha, ct);

            // Actualizar saldos
            saldoHembras = saldoHembras - seg.MortalidadH - seg.SelH - ventasH - trasladosH;
            saldoMachos = saldoMachos - seg.MortalidadM - seg.SelM - ventasM - trasladosM;

            // Obtener venta de huevos del día
            var ventaHuevo = await ObtenerVentaHuevosPorLPPAsync(lotePosturaProduccionId, seg.Fecha, ct);
            
            // Obtener huevos cargados (por ahora igual a incubables, puede venir de otra fuente)
            var huevosCargados = seg.HuevoInc; // TODO: Si hay tabla de incubación, obtener de ahí
            
            // Calcular porcentajes
            // Porcentaje de postura se calcula sobre hembras (solo hembras ponen huevos)
            var porcentajePostura = saldoHembras > 0 ? (decimal)seg.HuevoTot / saldoHembras * 100 : 0;
            var porcentajeEnviadoPlanta = seg.HuevoTot > 0 ? (decimal)huevosEnviadosPlanta / seg.HuevoTot * 100 : 0;
            
            // Porcentaje de nacimientos (por ahora null, requiere tabla de nacimientos)
            decimal? porcentajeNacimientos = null; // TODO: Calcular si hay datos de nacimientos
            
            // Pollitos vendidos (por ahora null, requiere tabla de ventas de pollitos)
            int? pollitosVendidos = null; // TODO: Obtener de tabla de ventas de pollitos
            
            // Porcentaje de grasa corporal (por ahora null, requiere datos de pesaje)
            decimal? porcentajeGrasaCorporal = null; // TODO: Calcular si hay datos de grasa corporal

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
                SeleccionMachos: seg.SelM,
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
                PorcentajeEnviadoPlanta: porcentajeEnviadoPlanta,
                HuevosIncubables: seg.HuevoInc,
                HuevosCargados: huevosCargados,
                PorcentajeNacimientos: porcentajeNacimientos,
                VentaHuevo: ventaHuevo,
                PollitosVendidos: pollitosVendidos,
                PesoHembra: seg.PesoH,
                PesoMachos: seg.PesoM,
                PesoHuevo: seg.PesoHuevo,
                PorcentajeGrasaCorporal: porcentajeGrasaCorporal,
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
        int? loteOrigenId,
        DateTime fecha,
        CancellationToken ct)
    {
        if (!loteOrigenId.HasValue)
            return (0, 0, 0, 0);

        var movimientos = await _ctx.MovimientoAves
            .AsNoTracking()
            .Where(m => m.LoteOrigenId == loteOrigenId.Value &&
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

    private async Task<int> ObtenerHuevosEnviadosPlantaPorLPPAsync(int lotePosturaProduccionId, DateTime fecha, CancellationToken ct)
    {
        var traslados = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LotePosturaProduccionId == lotePosturaProduccionId &&
                       t.FechaTraslado.Date == fecha.Date &&
                       t.TipoDestino == "Planta" &&
                       t.Estado == "Completado")
            .ToListAsync(ct);

        return traslados.Sum(t => t.CantidadLimpio + t.CantidadTratado);
    }

    private async Task<int?> ObtenerVentaHuevosAsync(string loteId, DateTime fecha, CancellationToken ct)
    {
        var ventas = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LoteId == loteId &&
                       t.FechaTraslado.Date == fecha.Date &&
                       t.TipoOperacion == "Venta" &&
                       t.Estado == "Completado")
            .ToListAsync(ct);

        var total = ventas.Sum(t => t.TotalHuevos);
        return total > 0 ? total : null;
    }

    private async Task<int?> ObtenerVentaHuevosPorLPPAsync(int lotePosturaProduccionId, DateTime fecha, CancellationToken ct)
    {
        var ventas = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LotePosturaProduccionId == lotePosturaProduccionId &&
                       t.FechaTraslado.Date == fecha.Date &&
                       t.TipoOperacion == "Venta" &&
                       t.Estado == "Completado")
            .ToListAsync(ct);

        var total = ventas.Sum(t => t.CantidadLimpio + t.CantidadTratado + t.CantidadSucio + t.CantidadDeforme +
            t.CantidadBlanco + t.CantidadDobleYema + t.CantidadPiso + t.CantidadPequeno + t.CantidadRoto +
            t.CantidadDesecho + t.CantidadOtro);
        return total > 0 ? total : null;
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

    private async Task<(int total, int limpio, int tratado, int sucio, int deforme, int blanco,
                        int dobleYema, int piso, int pequeno, int roto, int desecho, int otro)>
        ObtenerTransferenciasHuevosPorLPPAsync(int lotePosturaProduccionId, DateTime fecha, CancellationToken ct)
    {
        var traslados = await _ctx.TrasladoHuevos
            .AsNoTracking()
            .Where(t => t.LotePosturaProduccionId == lotePosturaProduccionId &&
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
                PorcentajeEnviadoPlanta: g.Sum(d => d.HuevosTotales) > 0 
                    ? (decimal)g.Sum(d => d.HuevosEnviadosPlanta) / g.Sum(d => d.HuevosTotales) * 100 
                    : 0,
                HuevosIncubables: g.Sum(d => d.HuevosIncubables),
                HuevosCargados: g.Sum(d => d.HuevosCargados),
                PorcentajeNacimientos: g.Where(d => d.PorcentajeNacimientos.HasValue).Select(d => d.PorcentajeNacimientos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PorcentajeNacimientos.HasValue).Select(d => d.PorcentajeNacimientos!.Value).Average()
                    : null,
                VentaHuevo: g.Where(d => d.VentaHuevo.HasValue).Sum(d => d.VentaHuevo!.Value) > 0
                    ? g.Where(d => d.VentaHuevo.HasValue).Sum(d => d.VentaHuevo!.Value)
                    : null,
                PollitosVendidos: g.Where(d => d.PollitosVendidos.HasValue).Sum(d => d.PollitosVendidos!.Value) > 0
                    ? g.Where(d => d.PollitosVendidos.HasValue).Sum(d => d.PollitosVendidos!.Value)
                    : null,
                PesoHembra: g.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).Average()
                    : null,
                PesoMachos: g.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).Average()
                    : null,
                PesoHuevo: g.Average(d => d.PesoHuevo),
                PorcentajeGrasaCorporal: g.Where(d => d.PorcentajeGrasaCorporal.HasValue).Select(d => d.PorcentajeGrasaCorporal!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)g.Where(d => d.PorcentajeGrasaCorporal.HasValue).Select(d => d.PorcentajeGrasaCorporal!.Value).Average()
                    : null,
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
                PorcentajeEnviadoPlantaPromedio: datosSemana.Average(d => d.PorcentajeEnviadoPlanta),
                HuevosIncubablesSemanal: datosSemana.Sum(d => d.HuevosIncubables),
                HuevosCargadosSemanal: datosSemana.Sum(d => d.HuevosCargados),
                PorcentajeNacimientosPromedio: datosSemana.Where(d => d.PorcentajeNacimientos.HasValue).Select(d => d.PorcentajeNacimientos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PorcentajeNacimientos.HasValue).Select(d => d.PorcentajeNacimientos!.Value).Average()
                    : null,
                VentaHuevoSemanal: datosSemana.Where(d => d.VentaHuevo.HasValue).Sum(d => d.VentaHuevo!.Value) > 0
                    ? datosSemana.Where(d => d.VentaHuevo.HasValue).Sum(d => d.VentaHuevo!.Value)
                    : null,
                PollitosVendidosSemanal: datosSemana.Where(d => d.PollitosVendidos.HasValue).Sum(d => d.PollitosVendidos!.Value) > 0
                    ? datosSemana.Where(d => d.PollitosVendidos.HasValue).Sum(d => d.PollitosVendidos!.Value)
                    : null,
                PesoHembraPromedio: datosSemana.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PesoHembra.HasValue).Select(d => d.PesoHembra!.Value).Average()
                    : null,
                PesoMachosPromedio: datosSemana.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PesoMachos.HasValue).Select(d => d.PesoMachos!.Value).Average()
                    : null,
                PesoHuevoPromedio: datosSemana.Average(d => d.PesoHuevo),
                PorcentajeGrasaCorporalPromedio: datosSemana.Where(d => d.PorcentajeGrasaCorporal.HasValue).Select(d => d.PorcentajeGrasaCorporal!.Value).DefaultIfEmpty(0).Average() > 0
                    ? (decimal?)datosSemana.Where(d => d.PorcentajeGrasaCorporal.HasValue).Select(d => d.PorcentajeGrasaCorporal!.Value).Average()
                    : null,
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
        foreach (var sublote in sublotes)
        {
            var loteProd = await ObtenerLoteProduccionAsync(sublote, ct);
            var fechaInicioSublote = loteProd?.FechaInicioProduccion ?? sublote.FechaEncaset ?? DateTime.Today;
            var loteIdSeguimiento = (loteProd ?? sublote).LoteId ?? sublote.LoteId;

            var edadInicioSemana = (semana - 1) * 7;
            var fechaInicioSemana = fechaInicioSublote.AddDays(edadInicioSemana);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            var edadFinSemana = CalcularEdadDias(fechaInicioSublote, fechaFinSemana);
            if (edadFinSemana < 6)
                return false;

            if (!loteIdSeguimiento.HasValue)
                return false;
            var diasConDatos = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == TipoProduccion &&
                            s.LoteId == loteIdSeguimiento.Value.ToString() &&
                            s.Fecha >= fechaInicioSemana &&
                            s.Fecha <= fechaFinSemana)
                .CountAsync(ct);

            if (diasConDatos < 7)
                return false;
        }

        return true;
    }

    /// <summary>Consolida semanas para reporte con sublotes LPP (lote_postura_produccion).</summary>
    private async Task<List<ReporteTecnicoProduccionSemanalDto>> ConsolidarSemanalesConsolidadoLPPAsync(
        List<ReporteTecnicoProduccionDiarioDto> datosConsolidados,
        DateTime fechaInicioProduccion,
        List<LotePosturaProduccion> sublotesLpp,
        CancellationToken ct)
    {
        var semanasCompletas = new List<int>();
        var semanasUnicas = datosConsolidados.Select(d => d.Semana).Distinct().OrderBy(s => s).ToList();

        foreach (var semana in semanasUnicas)
        {
            var esCompleta = await EsSemanaCompletaConsolidadaLPPAsync(semana, sublotesLpp, fechaInicioProduccion, ct);
            if (esCompleta)
                semanasCompletas.Add(semana);
        }

        var datosFiltrados = datosConsolidados
            .Where(d => semanasCompletas.Contains(d.Semana))
            .ToList();

        return ConsolidarSemanales(datosFiltrados, fechaInicioProduccion);
    }

    private async Task<bool> EsSemanaCompletaConsolidadaLPPAsync(
        int semana,
        List<LotePosturaProduccion> sublotesLpp,
        DateTime fechaInicioProduccion,
        CancellationToken ct)
    {
        foreach (var sublote in sublotesLpp)
        {
            var lppId = sublote.LotePosturaProduccionId ?? 0;
            if (lppId <= 0) return false;

            var fechaInicioSublote = sublote.FechaInicioProduccion ?? sublote.FechaEncaset ?? DateTime.Today;
            var edadInicioSemana = Math.Max(0, (semana - 26) * 7);
            var fechaInicioSemana = fechaInicioSublote.AddDays(edadInicioSemana);
            var fechaFinSemana = fechaInicioSemana.AddDays(6);

            var edadFinSemana = CalcularEdadDias(fechaInicioSublote, fechaFinSemana);
            if (edadFinSemana < 6) return false;

            var diasConDatos = await _ctx.SeguimientoDiario
                .AsNoTracking()
                .Where(s => s.TipoSeguimiento == TipoProduccion &&
                            s.LotePosturaProduccionId == lppId &&
                            s.Fecha >= fechaInicioSemana &&
                            s.Fecha <= fechaFinSemana)
                .CountAsync(ct);

            if (diasConDatos < 7)
                return false;
        }

        return true;
    }

    /// <summary>Obtiene el lote en fase Producción (mismo lote o hijo). Opción B.</summary>
    private async Task<Lote?> ObtenerLoteProduccionAsync(Lote lote, CancellationToken ct = default)
    {
        if (lote.Fase == "Produccion" && lote.LoteId.HasValue)
            return lote;
        if (!lote.LoteId.HasValue) return null;
        return await _ctx.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LotePadreId == lote.LoteId && l.Fase == "Produccion" && l.DeletedAt == null, ct);
    }

    private ReporteTecnicoProduccionLoteInfoDto MapearInformacionLote(Lote lote, Lote? loteProd)
    {
        return new ReporteTecnicoProduccionLoteInfoDto(
            LoteId: lote.LoteId ?? 0,
            LoteNombre: lote.LoteNombre,
            Raza: lote.Raza,
            Linea: lote.Linea,
            FechaInicioProduccion: loteProd?.FechaInicioProduccion ?? lote.FechaEncaset,
            NumeroHembrasIniciales: loteProd?.HembrasInicialesProd ?? lote.HembrasL,
            NumeroMachosIniciales: loteProd?.MachosInicialesProd ?? lote.MachosL,
            Galpon: lote.GalponId != null ? int.TryParse(lote.GalponId, out var g) ? g : null : null,
            Tecnico: lote.Tecnico,
            GranjaNombre: lote.Farm?.Name,
            NucleoNombre: lote.Nucleo?.NucleoNombre
        );
    }

    /// <summary>Mapea LotePosturaProduccion a DTO de información del lote.</summary>
    private ReporteTecnicoProduccionLoteInfoDto MapearInformacionLoteFromLPP(LotePosturaProduccion lpp)
    {
        return new ReporteTecnicoProduccionLoteInfoDto(
            LoteId: lpp.LotePosturaProduccionId ?? lpp.LoteId ?? 0,
            LoteNombre: lpp.LoteNombre ?? "",
            Raza: lpp.Raza,
            Linea: lpp.Linea,
            FechaInicioProduccion: lpp.FechaInicioProduccion ?? lpp.FechaEncaset,
            NumeroHembrasIniciales: lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? lpp.HembrasL,
            NumeroMachosIniciales: lpp.AvesMInicial ?? lpp.MachosInicialesProd ?? lpp.MachosL,
            Galpon: lpp.GalponId != null && int.TryParse(lpp.GalponId, out var g) ? g : null,
            Tecnico: lpp.Tecnico,
            GranjaNombre: lpp.Farm?.Name,
            NucleoNombre: lpp.Nucleo?.NucleoNombre
        );
    }

    public async Task<ReporteTecnicoProduccionCompletoDto> GenerarReporteDiarioAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        if (consolidarSublotes)
        {
            var request = new GenerarReporteTecnicoProduccionRequestDto(
                TipoReporte: "diario",
                TipoConsolidacion: "consolidado",
                LoteId: loteId,
                LoteNombreBase: null,
                FechaInicio: fechaInicio,
                FechaFin: fechaFin,
                Semana: null
            );
            return await GenerarReporteConsolidadoAsync(request, ct);
        }
        else
        {
            return await GenerarReporteSubloteAsync(loteId, fechaInicio, fechaFin, ct);
        }
    }

    public async Task<List<string>> ObtenerSublotesAsync(string loteNombreBase, CancellationToken ct = default)
    {
        // Priorizar LPP (lote_postura_produccion)
        var sublotesLpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(loteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteNombre!)
            .OrderBy(n => n)
            .ToListAsync(ct);

        if (sublotesLpp.Any())
        {
            return sublotesLpp
                .Select(n => ExtraerSublote(n) ?? n)
                .Distinct()
                .ToList();
        }

        var sublotesLegacy = await _ctx.Lotes
            .AsNoTracking()
            .Where(l => l.LoteNombre != null && l.LoteNombre.StartsWith(loteNombreBase) &&
                       l.CompanyId == _currentUser.CompanyId &&
                       l.DeletedAt == null)
            .Select(l => l.LoteNombre!)
            .OrderBy(n => n)
            .ToListAsync(ct);

        return sublotesLegacy
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

    public async Task<ReporteTecnicoProduccionCuadroCompletoDto> GenerarReporteCuadroAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        // Obtener el reporte semanal completo primero (loteId = LotePosturaProduccionId)
        var reporteCompleto = await GenerarReporteDiarioAsync(loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

        // Obtener LPP para guía genética (loteId es LotePosturaProduccionId)
        var lpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null, ct);

        if (lpp == null)
            throw new InvalidOperationException($"Lote producción con ID {loteId} no encontrado");

        // Obtener datos de guía genética si están disponibles
        var guiasProduccion = new List<GuiaGeneticaDto>();
        if (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue)
        {
            try
            {
                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(
                    lpp.Raza,
                    lpp.AnoTablaGenetica.Value);
                guiasProduccion = guias.ToList();
            }
            catch
            {
                // Si no hay guía genética, continuar sin valores amarillos
            }
        }

        // Obtener datos completos de ProduccionAvicolaRaw para valores adicionales
        var guiasCompletas = new List<Domain.Entities.ProduccionAvicolaRaw>();
        if (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue)
        {
            var razaNorm = lpp.Raza.Trim().ToLower();
            var ano = lpp.AnoTablaGenetica.Value.ToString();

            guiasCompletas = await _ctx.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p =>
                    p.Raza != null && p.AnioGuia != null &&
                    EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                    p.AnioGuia.Trim() == ano)
                .ToListAsync(ct);
        }

        // Convertir datos semanales a formato Cuadro con valores de guía genética
        var datosCuadro = new List<ReporteTecnicoProduccionCuadroDto>();
        
        foreach (var semanal in reporteCompleto.DatosSemanales)
        {
            // Obtener guía genética para esta semana (edad en semanas de producción)
            var edadProduccionSemanas = semanal.EdadInicioSemanas;
            var guiaSemana = guiasProduccion.FirstOrDefault(g => g.Edad == edadProduccionSemanas);
            
            // Helper para parsear edad en ProduccionAvicolaRaw
            int? TryParseEdad(string? edadStr)
            {
                if (string.IsNullOrWhiteSpace(edadStr)) return null;
                var s = edadStr.Trim().Replace(",", ".");
                if (int.TryParse(s, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out var n))
                    return n;
                var match = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n2))
                    return n2;
                return null;
            }

            var guiaCompletaSemana = guiasCompletas
                .Where(g =>
                {
                    var edad = TryParseEdad(g.Edad);
                    return edad.HasValue && edad.Value == edadProduccionSemanas;
                })
                .FirstOrDefault();

            // Helper para parsear valores decimales
            decimal? ParseDecimal(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                var clean = value.Trim().Replace(",", ".");
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
                return null;
            }

            // Calcular valores acumulados y promedios
            var datosHastaSemana = reporteCompleto.DatosSemanales
                .Where(d => d.Semana <= semanal.Semana)
                .ToList();

            // Mortalidad acumulada
            var mortalidadAcumHembras = datosHastaSemana.Sum(d => d.MortalidadHembrasSemanal);
            var mortalidadAcumMachos = datosHastaSemana.Sum(d => d.MortalidadMachosSemanal);
            var avesInicialesHembras = reporteCompleto.LoteInfo.NumeroHembrasIniciales ?? 0;
            var avesInicialesMachos = reporteCompleto.LoteInfo.NumeroMachosIniciales ?? 0;
            
            var mortalidadAcumPorcentajeHembras = avesInicialesHembras > 0
                ? (decimal)mortalidadAcumHembras / avesInicialesHembras * 100
                : 0;
            var mortalidadAcumPorcentajeMachos = avesInicialesMachos > 0
                ? (decimal)mortalidadAcumMachos / avesInicialesMachos * 100
                : 0;

            // Huevos acumulados
            var huevosAcum = datosHastaSemana.Sum(d => d.HuevosTotalesSemanal);
            
            // Consumo acumulado
            var consumoAcumHembras = datosHastaSemana.Sum(d => d.KilosAlimentoHembrasSemanal);
            var consumoAcumMachos = datosHastaSemana.Sum(d => d.KilosAlimentoMachosSemanal);

            // Crear DTO del cuadro
            var cuadro = new ReporteTecnicoProduccionCuadroDto(
                Semana: semanal.Semana,
                Fecha: semanal.FechaInicioSemana,
                EdadProduccionSemanas: edadProduccionSemanas,
                AvesFinHembras: semanal.SaldoFinHembras,
                AvesFinMachos: semanal.SaldoFinMachos,
                // MORTALIDAD HEMBRAS
                MortalidadHembrasN: semanal.MortalidadHembrasSemanal,
                MortalidadHembrasDescPorcentajeSem: avesInicialesHembras > 0
                    ? (decimal)semanal.MortalidadHembrasSemanal / avesInicialesHembras * 100
                    : 0,
                MortalidadHembrasPorcentajeAcum: mortalidadAcumPorcentajeHembras,
                MortalidadHembrasStandarM: guiaSemana != null ? (decimal?)guiaSemana.MortalidadHembras : null,
                MortalidadHembrasAcumStandar: null, // Se calcularía acumulando valores de guía
                // MORTALIDAD MACHOS
                MortalidadMachosN: semanal.MortalidadMachosSemanal,
                MortalidadMachosDescPorcentajeSem: avesInicialesMachos > 0
                    ? (decimal)semanal.MortalidadMachosSemanal / avesInicialesMachos * 100
                    : 0,
                MortalidadMachosPorcentajeAcum: mortalidadAcumPorcentajeMachos,
                MortalidadMachosStandarM: guiaSemana != null ? (decimal?)guiaSemana.MortalidadMachos : null,
                MortalidadMachosAcumStandar: null,
                // PRODUCCION TOTAL DE HUEVOS
                HuevosVentaSemana: semanal.HuevosTotalesSemanal,
                HuevosAcum: huevosAcum,
                PorcentajeSem: semanal.PorcentajePosturaPromedio,
                PorcentajeRoss: ParseDecimal(guiaCompletaSemana?.ProdPorcentaje),
                Taa: datosHastaSemana.Count > 0 ? huevosAcum / datosHastaSemana.Count : 0,
                TaaRoss: ParseDecimal(guiaCompletaSemana?.HTotalAa),
                // HUEVOS ENVIADOS PLANTA
                EnviadosPlanta: semanal.HuevosEnviadosPlantaSemanal,
                AcumEnviaP: datosHastaSemana.Sum(d => d.HuevosEnviadosPlantaSemanal),
                PorcentajeEnviaP: semanal.PorcentajeEnviadoPlantaPromedio,
                PorcentajeHala: null, // % HALA - se obtendría de guía genética si existe
                // HUEVO INCUBABLE
                HuevosIncub: semanal.HuevosIncubablesSemanal,
                PorcentajeDescarte: semanal.HuevosTotalesSemanal > 0
                    ? (decimal)(semanal.HuevosTotalesSemanal - semanal.HuevosIncubablesSemanal) / semanal.HuevosTotalesSemanal * 100
                    : 0,
                PorcentajeAcumIncub: datosHastaSemana.Sum(d => d.HuevosTotalesSemanal) > 0
                    ? (decimal)datosHastaSemana.Sum(d => d.HuevosIncubablesSemanal) / datosHastaSemana.Sum(d => d.HuevosTotalesSemanal) * 100
                    : 0,
                Laa: datosHastaSemana.Count > 0 
                    ? (decimal)datosHastaSemana.Sum(d => d.HuevosIncubablesSemanal) / datosHastaSemana.Count
                    : 0,
                StdRoss: ParseDecimal(guiaCompletaSemana?.HIncAa),
                // HUEVOS CARGADOS Y POLLITOS
                HCarga: semanal.HuevosCargadosSemanal,
                HCargaAcu: datosHastaSemana.Sum(d => d.HuevosCargadosSemanal),
                VHuevo: semanal.VentaHuevoSemanal ?? 0,
                VHuevoPollitos: semanal.PollitosVendidosSemanal ?? 0,
                PollAcum: datosHastaSemana.Sum(d => d.PollitosVendidosSemanal ?? 0),
                Paa: datosHastaSemana.Count > 0
                    ? (decimal)datosHastaSemana.Sum(d => d.PollitosVendidosSemanal ?? 0) / datosHastaSemana.Count
                    : 0,
                PaaRoss: ParseDecimal(guiaCompletaSemana?.PollitoAa),
                // CONSUMO DE ALIMENTO HEMBRA
                KgSemHembra: semanal.KilosAlimentoHembrasSemanal,
                AcumHembra: consumoAcumHembras,
                AcumAaHembra: datosHastaSemana.Count > 0 ? consumoAcumHembras / datosHastaSemana.Count : 0,
                StAcumHembra: guiaSemana != null ? (decimal?)guiaSemana.ConsumoHembras * 7 / 1000 : null, // Convertir gramos/día a kg/semana
                LoteHembra: null,
                StGrHembra: guiaSemana != null ? (decimal?)guiaSemana.ConsumoHembras : null,
                // CONSUMO DE ALIMENTO MACHO
                KgSemMachos: semanal.KilosAlimentoMachosSemanal,
                AcumMachos: consumoAcumMachos,
                AcumAaMachos: datosHastaSemana.Count > 0 ? consumoAcumMachos / datosHastaSemana.Count : 0,
                StAcumMachos: guiaSemana != null ? (decimal?)guiaSemana.ConsumoMachos * 7 / 1000 : null,
                GrDiaMachos: semanal.KilosAlimentoMachosSemanal * 1000 / 7, // Convertir kg/semana a gramos/día
                StGrMachos: guiaSemana != null ? (decimal?)guiaSemana.ConsumoMachos : null,
                // PESOS
                PesoHembraKg: semanal.PesoHembraPromedio,
                PesoHembraStd: guiaSemana != null ? (decimal?)guiaSemana.PesoHembras / 1000 : null, // Convertir gramos a kg
                PesoMachosKg: semanal.PesoMachosPromedio,
                PesoMachosStd: guiaSemana != null ? (decimal?)guiaSemana.PesoMachos / 1000 : null,
                PesoHuevoSem: semanal.PesoHuevoPromedio,
                PesoHuevoStd: ParseDecimal(guiaCompletaSemana?.PesoHuevo),
                MasaSem: semanal.PesoHuevoPromedio * (decimal)semanal.PorcentajePosturaPromedio / 100, // Aproximación
                MasaStd: ParseDecimal(guiaCompletaSemana?.MasaHuevo),
                // % APROV
                PorcentajeAprovSem: null, // Se calcularía si hay datos de aprovechamiento
                PorcentajeAprovStd: ParseDecimal(guiaCompletaSemana?.AprovSem),
                // TIPO DE ALIMENTO
                TipoAlimento: null, // Se obtendría de seguimiento si existe
                // OBSERVACIONES
                Observaciones: null
            );

            datosCuadro.Add(cuadro);
        }

        return new ReporteTecnicoProduccionCuadroCompletoDto(
            reporteCompleto.LoteInfo,
            datosCuadro
        );
    }

    public async Task<ReporteClasificacionHuevoComercioCompletoDto> GenerarReporteClasificacionHuevoComercioAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        // loteId = LotePosturaProduccionId (flujo LPP)
        var lpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null, ct);

        if (lpp == null)
            throw new InvalidOperationException($"Lote producción con ID {loteId} no encontrado");

        var fechaInicioProduccion = lpp.FechaInicioProduccion ?? lpp.FechaEncaset ?? DateTime.Today;

        var seguimientos = await ObtenerSeguimientosProduccionPorLPPAsync(loteId, fechaInicio, fechaFin, ct);

        var loteInfoClasificacion = MapearInformacionLoteFromLPP(lpp);

        if (!seguimientos.Any())
        {
            return new ReporteClasificacionHuevoComercioCompletoDto(
                loteInfoClasificacion,
                new List<ReporteClasificacionHuevoComercioDto>()
            );
        }

        // Agrupar por semana
        var datosPorSemana = seguimientos
            .GroupBy(s =>
            {
                var edadDias = CalcularEdadDias(fechaInicioProduccion, s.Fecha);
                return CalcularSemana(edadDias);
            })
            .OrderBy(g => g.Key)
            .ToList();

        var datosClasificacion = new List<ReporteClasificacionHuevoComercioDto>();

        foreach (var grupoSemana in datosPorSemana)
        {
            var semana = grupoSemana.Key;
            var seguimientosSemana = grupoSemana.OrderBy(s => s.Fecha).ToList();
            var fechaInicioSemana = seguimientosSemana.First().Fecha;
            var fechaFinSemana = seguimientosSemana.Last().Fecha;

            // Calcular totales semanales
            var incubableLimpio = seguimientosSemana.Sum(s => s.HuevoLimpio);
            var huevoTratado = seguimientosSemana.Sum(s => s.HuevoTratado);
            var huevoDY = seguimientosSemana.Sum(s => s.HuevoDobleYema);
            var huevoRoto = seguimientosSemana.Sum(s => s.HuevoRoto);
            var huevoDeforme = seguimientosSemana.Sum(s => s.HuevoDeforme);
            var huevoPiso = seguimientosSemana.Sum(s => s.HuevoPiso);
            var huevoDesecho = seguimientosSemana.Sum(s => s.HuevoDesecho);
            var huevoPIP = seguimientosSemana.Sum(s => s.HuevoPequeno); // PIP = Pequeño
            var huevoSucioDeBanda = seguimientosSemana.Sum(s => s.HuevoSucio);
            var totalPN = seguimientosSemana.Sum(s => s.HuevoTot);

            // Calcular porcentajes
            var porcentajeTratado = totalPN > 0 ? (decimal)huevoTratado / totalPN * 100 : 0;
            var porcentajeDY = totalPN > 0 ? (decimal)huevoDY / totalPN * 100 : 0;
            var porcentajeRoto = totalPN > 0 ? (decimal)huevoRoto / totalPN * 100 : 0;
            var porcentajeDeforme = totalPN > 0 ? (decimal)huevoDeforme / totalPN * 100 : 0;
            var porcentajePiso = totalPN > 0 ? (decimal)huevoPiso / totalPN * 100 : 0;
            var porcentajeDesecho = totalPN > 0 ? (decimal)huevoDesecho / totalPN * 100 : 0;
            var porcentajePIP = totalPN > 0 ? (decimal)huevoPIP / totalPN * 100 : 0;
            var porcentajeSucioDeBanda = totalPN > 0 ? (decimal)huevoSucioDeBanda / totalPN * 100 : 0;
            var porcentajeTotal = 100m; // El total siempre es 100%

            // Obtener valores de guía genética si están disponibles
            // Por ahora, los valores de guía genética para clasificación de huevos no están en la tabla estándar
            // Se pueden agregar más adelante si se requiere
            var edadProduccionSemanas = CalcularEdadDias(fechaInicioProduccion, fechaInicioSemana) / 7;
            var guiasProduccion = new List<GuiaGeneticaDto>();
            var guiasCompletas = new List<Domain.Entities.ProduccionAvicolaRaw>();

            if (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue)
            {
                try
                {
                    var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(
                        lpp.Raza,
                        lpp.AnoTablaGenetica.Value);
                    guiasProduccion = guias.ToList();

                    var razaNorm = lpp.Raza.Trim().ToLower();
                    var ano = lpp.AnoTablaGenetica.Value.ToString();

                    guiasCompletas = await _ctx.ProduccionAvicolaRaw
                        .AsNoTracking()
                        .Where(p =>
                            p.Raza != null && p.AnioGuia != null &&
                            EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                            p.AnioGuia.Trim() == ano)
                        .ToListAsync(ct);
                }
                catch
                {
                    // Si no hay guía genética, continuar sin valores amarillos
                }
            }

            // Por ahora, los valores de guía genética para clasificación de huevos se dejan como null
            // Se pueden implementar más adelante si se agregan a la tabla de guía genética

            var clasificacion = new ReporteClasificacionHuevoComercioDto(
                Semana: semana,
                FechaInicioSemana: fechaInicioSemana,
                FechaFinSemana: fechaFinSemana,
                LoteNombre: lpp.LoteNombre ?? "",
                // Datos reales
                IncubableLimpio: incubableLimpio,
                HuevoTratado: huevoTratado,
                PorcentajeTratado: porcentajeTratado,
                HuevoDY: huevoDY,
                PorcentajeDY: porcentajeDY,
                HuevoRoto: huevoRoto,
                PorcentajeRoto: porcentajeRoto,
                HuevoDeforme: huevoDeforme,
                PorcentajeDeforme: porcentajeDeforme,
                HuevoPiso: huevoPiso,
                PorcentajePiso: porcentajePiso,
                HuevoDesecho: huevoDesecho,
                PorcentajeDesecho: porcentajeDesecho,
                HuevoPIP: huevoPIP,
                PorcentajePIP: porcentajePIP,
                HuevoSucioDeBanda: huevoSucioDeBanda,
                PorcentajeSucioDeBanda: porcentajeSucioDeBanda,
                TotalPN: totalPN,
                PorcentajeTotal: porcentajeTotal,
                // Valores de guía genética (amarillos) - Por ahora null, se implementarán más adelante
                IncubableLimpioGuia: null,
                HuevoTratadoGuia: null,
                PorcentajeTratadoGuia: null,
                HuevoDYGuia: null,
                PorcentajeDYGuia: null,
                HuevoRotoGuia: null,
                PorcentajeRotoGuia: null,
                HuevoDeformeGuia: null,
                PorcentajeDeformeGuia: null,
                HuevoPisoGuia: null,
                PorcentajePisoGuia: null,
                HuevoDesechoGuia: null,
                PorcentajeDesechoGuia: null,
                HuevoPIPGuia: null,
                PorcentajePIPGuia: null,
                HuevoSucioDeBandaGuia: null,
                PorcentajeSucioDeBandaGuia: null,
                TotalPNGuia: null,
                PorcentajeTotalGuia: null
            );

            datosClasificacion.Add(clasificacion);
        }

        return new ReporteClasificacionHuevoComercioCompletoDto(
            loteInfoClasificacion,
            datosClasificacion
        );
    }
}

