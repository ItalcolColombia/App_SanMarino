// MovimientoAves/Funciones/MovimientoAvesService.LoteInfo.cs
// Contexto de lote para movimientos: información consolidada del lote (aves iniciales/actuales
// calculadas desde seguimiento + movimientos), validación de existencia de Seguimiento Diario
// para una fecha y último número de despacho. Lógica extraída del controller sin cambiar
// consultas ni aritmética (mismos Math.Max, mismos filtros, mismo orden).
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Domain.Entities;

namespace ZooSanMarino.Infrastructure.Services;

public partial class MovimientoAvesService
{
    /// <summary>
    /// Obtiene el último número de despacho generado (para Ecuador) y el siguiente sugerido.
    /// </summary>
    public async Task<UltimoNumeroDespachoDto> ObtenerUltimoNumeroDespachoAsync()
    {
        // Obtener el último movimiento de tipo despacho (o el último movimiento en general)
        var ultimoMovimiento = await _context.MovimientoAves
            .AsNoTracking()
            .Where(m => m.CompanyId == _currentUser.CompanyId &&
                       m.DeletedAt == null)
            .OrderByDescending(m => m.Id)
            .FirstOrDefaultAsync();

        var ultimoId = ultimoMovimiento?.Id ?? 0;
        var siguienteNumero = ultimoId + 1;

        return new UltimoNumeroDespachoDto(ultimoId, siguienteNumero);
    }

    /// <summary>
    /// Obtiene información del lote para movimientos (etapa, aves disponibles, etc.).
    /// Calcula las aves actuales desde los registros diarios de seguimiento (Producción o Levante).
    /// Devuelve <c>null</c> si el lote no existe.
    /// </summary>
    public async Task<InformacionLoteMovimientoDto?> ObtenerInformacionLoteAsync(int loteId)
    {
        var ctx = await ObtenerContextoLoteAsync(loteId);
        if (ctx == null)
            return null;

        var lote = ctx.Lote;
        var etapa = ctx.Etapa;
        var tipoLote = ctx.TipoLote;
        var lotePosturaLev = ctx.LotePosturaLevante;

        int hembrasIniciales = 0;
        int machosIniciales = 0;
        int hembrasActuales = 0;
        int machosActuales = 0;
        int mixtasActuales = 0;

        if (tipoLote == "Produccion")
        {
            var lotePosturaProd = ctx.LotePosturaProduccion;

            if (lotePosturaProd != null)
            {
                hembrasIniciales = lotePosturaProd.AvesHInicial ?? lotePosturaProd.HembrasInicialesProd ?? 0;
                machosIniciales = lotePosturaProd.AvesMInicial ?? lotePosturaProd.MachosInicialesProd ?? 0;

                var mortalidadProd = await _context.SeguimientoProduccion
                    .AsNoTracking()
                    .Where(s => s.LoteId == loteId)
                    .GroupBy(_ => 1)
                    .Select(g => new {
                        MortH = g.Sum(x => (int?)x.MortalidadH) ?? 0,
                        MortM = g.Sum(x => (int?)x.MortalidadM) ?? 0,
                        SelH  = g.Sum(x => (int?)x.SelH) ?? 0,
                        SelM  = g.Sum(x => (int?)x.SelM) ?? 0
                    })
                    .FirstOrDefaultAsync();

                var movSalidaProd = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                var movEntradaProd = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                hembrasActuales = Math.Max(0, hembrasIniciales
                    - (mortalidadProd?.MortH ?? 0) - (mortalidadProd?.SelH ?? 0)
                    - movSalidaProd.Sum(m => m.CantidadHembras)
                    + movEntradaProd.Sum(m => m.CantidadHembras));
                machosActuales = Math.Max(0, machosIniciales
                    - (mortalidadProd?.MortM ?? 0) - (mortalidadProd?.SelM ?? 0)
                    - movSalidaProd.Sum(m => m.CantidadMachos)
                    + movEntradaProd.Sum(m => m.CantidadMachos));
                mixtasActuales = movEntradaProd.Sum(m => m.CantidadMixtas);
            }
            else
            {
                var loteProd = lote.Fase == "Produccion" ? lote : await _context.Lotes
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null);

                if (loteProd != null)
                {
                    hembrasIniciales = loteProd.HembrasInicialesProd ?? 0;
                    machosIniciales = loteProd.MachosInicialesProd ?? 0;
                    var loteIdSeguimiento = loteProd.LoteId ?? loteId;

                    var seguimientos = await _context.SeguimientoProduccion
                        .AsNoTracking()
                        .Where(s => s.LoteId == loteIdSeguimiento)
                        .ToListAsync();

                    var totalMortalidadH = seguimientos.Sum(s => s.MortalidadH);
                    var totalMortalidadM = seguimientos.Sum(s => s.MortalidadM);
                    var totalSeleccionH = seguimientos.Sum(s => s.SelH);
                    var totalSeleccionM = seguimientos.Sum(s => s.SelM);

                    var movimientosSalida = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    var movimientosEntrada = await _context.MovimientoAves
                        .AsNoTracking()
                        .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                        .ToListAsync();

                    hembrasActuales = Math.Max(0, hembrasIniciales - totalMortalidadH - totalSeleccionH
                        - movimientosSalida.Sum(m => m.CantidadHembras)
                        + movimientosEntrada.Sum(m => m.CantidadHembras));
                    machosActuales = Math.Max(0, machosIniciales - totalMortalidadM - totalSeleccionM
                        - movimientosSalida.Sum(m => m.CantidadMachos)
                        + movimientosEntrada.Sum(m => m.CantidadMachos));
                    mixtasActuales = movimientosEntrada.Sum(m => m.CantidadMixtas);
                }
            }
        }
        else
        {
            if (lotePosturaLev != null)
            {
                hembrasIniciales = lotePosturaLev.AvesHInicial ?? 0;
                machosIniciales = lotePosturaLev.AvesMInicial ?? 0;
                var mortCajaHLev = lote.MortCajaH ?? 0;
                var mortCajaMlev = lote.MortCajaM ?? 0;

                var mortalidadLev = await _context.SeguimientoDiario
                    .AsNoTracking()
                    .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString())
                    .GroupBy(_ => 1)
                    .Select(g => new {
                        MortH = g.Sum(x => x.MortalidadHembras ?? 0),
                        MortM = g.Sum(x => x.MortalidadMachos ?? 0),
                        SelH  = g.Sum(x => x.SelH ?? 0),
                        SelM  = g.Sum(x => x.SelM ?? 0),
                        ErrH  = g.Sum(x => x.ErrorSexajeHembras ?? 0),
                        ErrM  = g.Sum(x => x.ErrorSexajeMachos ?? 0)
                    })
                    .FirstOrDefaultAsync();

                var movSalidaLev = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                var movEntradaLev = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                hembrasActuales = Math.Max(0, hembrasIniciales - mortCajaHLev
                    - (mortalidadLev?.MortH ?? 0) - (mortalidadLev?.SelH ?? 0) - (mortalidadLev?.ErrH ?? 0)
                    - movSalidaLev.Sum(m => m.CantidadHembras)
                    + movEntradaLev.Sum(m => m.CantidadHembras));
                machosActuales = Math.Max(0, machosIniciales - mortCajaMlev
                    - (mortalidadLev?.MortM ?? 0) - (mortalidadLev?.SelM ?? 0) - (mortalidadLev?.ErrM ?? 0)
                    - movSalidaLev.Sum(m => m.CantidadMachos)
                    + movEntradaLev.Sum(m => m.CantidadMachos));
                mixtasActuales = movEntradaLev.Sum(m => m.CantidadMixtas);
            }
            else
            {
                hembrasIniciales = lote.HembrasL ?? 0;
                machosIniciales = lote.MachosL ?? 0;
                var mortCajaH = lote.MortCajaH ?? 0;
                var mortCajaM = lote.MortCajaM ?? 0;

                var seguimientos = await _context.SeguimientoDiario
                    .AsNoTracking()
                    .Where(s => s.TipoSeguimiento == "levante" && s.LoteId == loteId.ToString())
                    .ToListAsync();

                var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras ?? 0);
                var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos ?? 0);
                var totalSeleccionH = seguimientos.Sum(s => s.SelH ?? 0);
                var totalSeleccionM = seguimientos.Sum(s => s.SelM ?? 0);
                var totalErrorSexajeH = seguimientos.Sum(s => s.ErrorSexajeHembras ?? 0);
                var totalErrorSexajeM = seguimientos.Sum(s => s.ErrorSexajeMachos ?? 0);

                var movimientosSalida = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteOrigenId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                var movimientosEntrada = await _context.MovimientoAves
                    .AsNoTracking()
                    .Where(m => m.LoteDestinoId == loteId && m.Estado == "Completado" && m.DeletedAt == null)
                    .ToListAsync();

                hembrasActuales = Math.Max(0, hembrasIniciales - mortCajaH - totalMortalidadH - totalSeleccionH - totalErrorSexajeH
                    - movimientosSalida.Sum(m => m.CantidadHembras)
                    + movimientosEntrada.Sum(m => m.CantidadHembras));
                machosActuales = Math.Max(0, machosIniciales - mortCajaM - totalMortalidadM - totalSeleccionM - totalErrorSexajeM
                    - movimientosSalida.Sum(m => m.CantidadMachos)
                    + movimientosEntrada.Sum(m => m.CantidadMachos));
                mixtasActuales = movimientosEntrada.Sum(m => m.CantidadMixtas);
            }
        }

        var totalAvesActuales = hembrasActuales + machosActuales + mixtasActuales;

        DateTime? fechaInicioProduccion = null;
        if (tipoLote == "Produccion")
        {
            var loteProdFecha = lote.Fase == "Produccion" ? lote : await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LotePadreId == loteId && l.Fase == "Produccion" && l.DeletedAt == null);
            fechaInicioProduccion = loteProdFecha?.FechaInicioProduccion;
        }

        string? raza = lote!.Raza;
        int? anoTablaGenetica = lote.AnoTablaGenetica;
        if (lote.LotePadreId.HasValue && (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue))
        {
            var lotePadre = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == lote.LotePadreId.Value && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
                .FirstOrDefaultAsync();

            if (lotePadre != null)
            {
                if (string.IsNullOrEmpty(raza) && !string.IsNullOrEmpty(lotePadre.Raza))
                    raza = lotePadre.Raza;
                if (!anoTablaGenetica.HasValue && lotePadre.AnoTablaGenetica.HasValue)
                    anoTablaGenetica = lotePadre.AnoTablaGenetica;
            }
        }

        return new InformacionLoteMovimientoDto(
            LoteId: lote.LoteId,
            LoteNombre: lote.LoteNombre,
            GranjaId: lote.GranjaId,
            GranjaNombre: lote.Farm?.Name,
            NucleoId: lote.NucleoId,
            NucleoNombre: lote.Nucleo?.NucleoNombre,
            GalponId: lote.GalponId,
            GalponNombre: lote.Galpon?.GalponNombre,
            Etapa: etapa,
            TipoLote: tipoLote,
            LotePosturaLevanteId: ctx.LotePosturaLevante?.LotePosturaLevanteId,
            LotePosturaProduccionId: ctx.LotePosturaProduccion?.LotePosturaProduccionId,
            HembrasIniciales: hembrasIniciales,
            MachosIniciales: machosIniciales,
            CantidadHembras: hembrasActuales,
            CantidadMachos: machosActuales,
            CantidadMixtas: mixtasActuales,
            TotalAves: totalAvesActuales,
            FechaEncasetamiento: lote.FechaEncaset,
            FechaInicioProduccion: fechaInicioProduccion,
            DiasDesdeEncasetamiento: ctx.DiasDesdeEncaset,
            Raza: raza,
            AnoTablaGenetica: anoTablaGenetica);
    }

    /// <summary>
    /// Valida que exista un registro de Seguimiento Diario para el lote en la fecha indicada.
    /// Devuelve <c>null</c> si el lote no existe; en caso contrario un resultado con
    /// <see cref="ValidacionFechaSeguimientoResultado.Existe"/> indicando si hay registro.
    /// </summary>
    public async Task<ValidacionFechaSeguimientoResultado?> ValidarFechaSeguimientoAsync(int loteId, DateTime fecha)
    {
        var ctx = await ObtenerContextoLoteAsync(loteId);
        if (ctx == null)
            return null;

        var fechaNorm = fecha.Date;
        var loteIdStr = loteId.ToString();

        SeguimientoDiario? registro = null;

        if (ctx.TipoLote == "Produccion")
        {
            // Para producción: buscar por LotePosturaProduccionId si existe, o por lote_id + tipo
            if (ctx.LotePosturaProduccion?.LotePosturaProduccionId is int lppId)
            {
                registro = await _context.SeguimientoDiario
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.LotePosturaProduccionId == lppId &&
                        s.TipoSeguimiento == "produccion" &&
                        s.Fecha.Date == fechaNorm);
            }

            registro ??= await _context.SeguimientoDiario
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.LoteId == loteIdStr &&
                    s.TipoSeguimiento == "produccion" &&
                    s.Fecha.Date == fechaNorm);
        }
        else
        {
            // Para levante: buscar por LotePosturaLevanteId si existe, o por lote_id + tipo
            if (ctx.LotePosturaLevante?.LotePosturaLevanteId is int lplId)
            {
                registro = await _context.SeguimientoDiario
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.LotePosturaLevanteId == lplId &&
                        s.TipoSeguimiento == "levante" &&
                        s.Fecha.Date == fechaNorm);
            }

            registro ??= await _context.SeguimientoDiario
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.LoteId == loteIdStr &&
                    s.TipoSeguimiento == "levante" &&
                    s.Fecha.Date == fechaNorm);
        }

        if (registro is null)
        {
            return new ValidacionFechaSeguimientoResultado
            {
                Existe = false,
                FechaNormalizada = fechaNorm,
                TipoLote = ctx.TipoLote
            };
        }

        return new ValidacionFechaSeguimientoResultado
        {
            Existe = true,
            FechaNormalizada = fechaNorm,
            TipoLote = ctx.TipoLote,
            SeguimientoId = registro.Id,
            TipoSeguimiento = registro.TipoSeguimiento,
            LotePosturaLevanteId = registro.LotePosturaLevanteId,
            LotePosturaProduccionId = registro.LotePosturaProduccionId,
            Fecha = registro.Fecha,
            TrasladoAvesEntrante = registro.TrasladoAvesEntrante,
            TrasladoAvesSalida = registro.TrasladoAvesSalida,
            VentaAvesCantidad = registro.VentaAvesCantidad,
            VentaAvesMotivo = registro.VentaAvesMotivo
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed record ContextoLote(
        Lote Lote,
        int DiasDesdeEncaset,
        int Etapa,
        string TipoLote,
        LotePosturaLevante? LotePosturaLevante,
        LotePosturaProduccion? LotePosturaProduccion);

    private async Task<ContextoLote?> ObtenerContextoLoteAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .Include(l => l.Galpon)
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote is null) return null;

        var diasDesdeEncaset = lote.FechaEncaset.HasValue
            ? (DateTime.UtcNow.Date - lote.FechaEncaset.Value.Date).Days
            : 0;
        var etapa = diasDesdeEncaset > 0 ? (diasDesdeEncaset / 7) + 1 : 0;

        var lotePosturaLev = await _context.LotePosturaLevante
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        var tipoLote = lote.Fase ?? "Levante";
        if (tipoLote == "Levante" && (lotePosturaLev?.EstadoCierre == "Cerrado" || etapa >= 26))
            tipoLote = "Produccion";

        LotePosturaProduccion? lotePosturaProd = null;
        if (tipoLote == "Produccion")
        {
            lotePosturaProd = await _context.LotePosturaProduccion
                .AsNoTracking()
                .Where(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null)
                .FirstOrDefaultAsync();
        }

        return new ContextoLote(lote, diasDesdeEncaset, etapa, tipoLote, lotePosturaLev, lotePosturaProd);
    }
}
