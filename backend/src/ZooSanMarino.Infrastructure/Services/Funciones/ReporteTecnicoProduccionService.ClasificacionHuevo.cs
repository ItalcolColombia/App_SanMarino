// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.ClasificacionHuevo.cs
// Reporte de clasificacion de huevo para comercio, leyendo desde produccion_diaria.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
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

        var seguimientos = await ObtenerSeguimientosDesdePDAsync(loteId, fechaInicio, fechaFin, ct);

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

                    // Santa Reyes tiene su guia en tabla propia (F2.2). Se pregunta primero; si la
                    // empresa no tiene guia propia la lista vuelve vacia y corre la de siempre.
                    guiasCompletas = await GuiaGeneticaLookup.ObtenerFilasPropiasAsync(
                        _ctx, _currentUser.CompanyId, razaNorm, ano, ct);

                    if (guiasCompletas.Count == 0)
                    {
                        guiasCompletas = await _ctx.ProduccionAvicolaRaw
                            .AsNoTracking()
                            .Where(p =>
                                p.CompanyId == _currentUser.CompanyId &&
                                p.Raza != null && p.AnioGuia != null &&
                                EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                                p.AnioGuia.Trim() == ano)
                            .ToListAsync(ct);
                    }
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

    /// <summary>
    /// Lee la serie diaria de producción vía <c>fn_seguimiento_diario_produccion</c> (única
    /// fórmula — antes leía <c>_ctx.SeguimientoProduccion</c> crudo, sin pasar por la fn: con el
    /// flag <c>permite_multiples_seguimientos_diarios</c> ON eso duplicaba el renglón del día en
    /// vez de mostrar el total agrupado). Filas movimiento-only (<c>SegId == null</c>) se excluyen
    /// — no son un registro editable, igual que en la grilla de <c>ListarSeguimientoAsync</c>.
    /// </summary>
    private async Task<List<SegProduccionParaReporte>> ObtenerSeguimientosDesdePDAsync(
        int lotePosturaProduccionId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        CancellationToken ct)
    {
        var filas = await _ctx.Database
            .SqlQueryRaw<SeguimientoProduccionTablaFilaDto>(
                "SELECT * FROM fn_seguimiento_diario_produccion({0}::int, NULL::int)",
                lotePosturaProduccionId)
            .ToListAsync(ct);

        IEnumerable<SeguimientoProduccionTablaFilaDto> visibles = filas.Where(f => f.SegId != null);
        if (fechaInicio.HasValue)
            visibles = visibles.Where(f => f.Fecha >= fechaInicio.Value);
        if (fechaFin.HasValue)
            visibles = visibles.Where(f => f.Fecha <= fechaFin.Value);

        return visibles
            .OrderBy(f => f.Fecha)
            .Select(f => new SegProduccionParaReporte
            {
                Fecha        = f.Fecha,
                MortalidadH  = f.MortalidadHembras ?? 0,
                MortalidadM  = f.MortalidadMachos ?? 0,
                SelH         = f.SelH ?? 0,
                SelM         = f.SelM ?? 0,
                ConsKgH      = (decimal)(f.ConsKgH ?? 0),
                ConsKgM      = (decimal)(f.ConsKgM ?? 0),
                HuevoTot     = f.HuevoTot ?? 0,
                HuevoInc     = f.HuevoInc ?? 0,
                HuevoLimpio  = f.HuevoLimpio ?? 0,
                HuevoTratado = f.HuevoTratado ?? 0,
                HuevoSucio   = f.HuevoSucio ?? 0,
                HuevoDeforme = f.HuevoDeforme ?? 0,
                HuevoBlanco  = f.HuevoBlanco ?? 0,
                HuevoDobleYema = f.HuevoDobleYema ?? 0,
                HuevoPiso    = f.HuevoPiso ?? 0,
                HuevoPequeno = f.HuevoPequeno ?? 0,
                HuevoRoto    = f.HuevoRoto ?? 0,
                HuevoDesecho = f.HuevoDesecho ?? 0,
                HuevoOtro    = f.HuevoOtro ?? 0,
                PesoH        = f.PesoH,
                PesoM        = f.PesoM,
                PesoHuevo    = (decimal)(f.PesoHuevo ?? 0)
            })
            .ToList();
    }
}
