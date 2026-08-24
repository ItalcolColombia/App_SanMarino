// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Diario.cs
// Reporte tecnico de PRODUCCION, diario: por sublote y consolidado, con ventas/traslados y huevo enviado a planta del dia.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
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
            ct,
            // La fuente canonica del seguimiento de produccion es seguimiento_diario_produccion.
            // El otro camino busca filas de produccion dentro de seguimiento_diario_levante
            // (tipo_seguimiento='produccion'), donde no existe ninguna: por eso este reporte
            // salia vacio para todas las empresas.
            usarProduccionDiaria: true);

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
                ct,
                // La fuente canonica del seguimiento de produccion es seguimiento_diario_produccion.
                // El otro camino busca filas de produccion dentro de seguimiento_diario_levante
                // (tipo_seguimiento='produccion'), donde no existe ninguna: por eso este reporte
                // salia vacio para todas las empresas.
                usarProduccionDiaria: true);

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
        CancellationToken ct,
        bool usarProduccionDiaria = false)
    {
        var seguimientos = usarProduccionDiaria
            ? await ObtenerSeguimientosDesdePDAsync(lotePosturaProduccionId, fechaInicio, fechaFin, ct)
            : await ObtenerSeguimientosProduccionPorLPPAsync(lotePosturaProduccionId, fechaInicio, fechaFin, ct);

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
}
