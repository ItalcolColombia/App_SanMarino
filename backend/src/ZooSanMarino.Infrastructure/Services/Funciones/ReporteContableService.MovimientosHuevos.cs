// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteContableService.MovimientosHuevos.cs
// Reporte de movimientos de huevos (entradas/salidas/traslados) por lote padre.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Traslados;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteContableService
{
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
}
