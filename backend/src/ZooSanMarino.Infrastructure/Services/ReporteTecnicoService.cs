// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoService : IReporteTecnicoService
{
    private readonly ZooSanMarinoContext _ctx;
    private readonly ICurrentUser _currentUser;
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public ReporteTecnicoService(
        ZooSanMarinoContext ctx, 
        ICurrentUser currentUser,
        IGuiaGeneticaService guiaGeneticaService)
    {
        _ctx = ctx;
        _currentUser = currentUser;
        _guiaGeneticaService = guiaGeneticaService;
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

    // Helpers cross-concern: usados por Diario, Semanal, Sublotes, Alimento,
    // LevanteCompleto y LevanteTabs (partial: visibles entre todos los archivos
    // de Funciones/).

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
            NumeroMachos = lote.MachosL,
            Galpon = int.TryParse(lote.GalponId, out var galponId) ? galponId : null,
            Tecnico = lote.Tecnico,
            GranjaNombre = lote.Farm?.Name,
            NucleoNombre = lote.Nucleo?.NucleoNombre
        };
    }

    private ReporteTecnicoLoteInfoDto MapearInformacionLoteFromLPL(LotePosturaLevante lpl)
    {
        return new ReporteTecnicoLoteInfoDto
        {
            LoteId = lpl.LotePosturaLevanteId ?? 0,
            LoteNombre = lpl.LoteNombre,
            Raza = lpl.Raza,
            Linea = lpl.Linea,
            Etapa = "LEVANTE",
            FechaEncaset = lpl.FechaEncaset,
            NumeroHembras = lpl.HembrasL,
            NumeroMachos = lpl.MachosL,
            Galpon = int.TryParse(lpl.GalponId, out var gid) ? gid : null,
            Tecnico = lpl.Tecnico,
            GranjaNombre = lpl.Farm?.Name,
            NucleoNombre = lpl.Nucleo?.NucleoNombre
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
        // Normalizar ambas fechas a la misma zona horaria (local) y obtener solo la fecha
        // Esto evita problemas con zonas horarias diferentes
        var fechaEncasetLocal = fechaEncaset.Kind == DateTimeKind.Utc 
            ? fechaEncaset.ToLocalTime() 
            : fechaEncaset;
            
        var fechaRegistroLocal = fechaRegistro.Kind == DateTimeKind.Utc 
            ? fechaRegistro.ToLocalTime() 
            : fechaRegistro;
        
        // Obtener solo la fecha (sin hora) para comparar días completos
        var fechaEncasetDate = fechaEncasetLocal.Date;
        var fechaRegistroDate = fechaRegistroLocal.Date;
        
        // Calcular diferencia en días
        var diff = fechaRegistroDate - fechaEncasetDate;
        var diasDiferencia = diff.Days;
        
        // En avicultura: día 1 = día del encasetamiento
        // Si el registro es el mismo día del encaset = día 1
        // Si el registro es 1 día después = día 2
        // Por lo tanto: edad = diferencia + 1
        // Ejemplo: 
        // - Encaset: 28 enero, Registro: 28 enero → diferencia = 0 → edad = 1 día
        // - Encaset: 28 enero, Registro: 29 enero → diferencia = 1 → edad = 2 días
        return Math.Max(1, diasDiferencia + 1);
    }

    private int CalcularEdadSemanas(int edadDias)
    {
        // 7 días = 1 semana
        // Semana 1 = días 1-7
        // Semana 2 = días 8-14
        // etc.
        return (int)Math.Ceiling(edadDias / 7.0);
    }

    /// <summary>
    /// Proyecta filas de guía a la forma que consume <see cref="GuiaMetricasDisponiblesCalculos"/>.
    /// Espeja el helper del reporte de producción; vive por separado porque los dos servicios son
    /// clases distintas y unificarlo obligaría a un tipo compartido sin ganancia real.
    /// </summary>
    private static List<FilaGuiaMetricas> AFilasGuiaMetricasLevante(
        IEnumerable<Domain.Entities.ProduccionAvicolaRaw> guias) =>
        guias.Select(g => new FilaGuiaMetricas(
            ProdPorcentaje: g.ProdPorcentaje,
            PesoHuevo:      g.PesoHuevo,
            HTotalAa:       g.HTotalAa,
            Uniformidad:    g.Uniformidad,
            PesoH:          g.PesoH,
            PesoM:          g.PesoM,
            MortSemH:       g.MortSemH,
            MortSemM:       g.MortSemM,
            RetiroAcH:      g.RetiroAcH,
            RetiroAcM:      g.RetiroAcM,
            ConsAcH:        g.ConsAcH,
            ConsAcM:        g.ConsAcM,
            GrAveDiaH:      g.GrAveDiaH,
            GrAveDiaM:      g.GrAveDiaM)).ToList();

    /// <summary>
    /// Semana MÍNIMA con guía cargada, o <c>null</c> si no hay ninguna. Es lo que deja avisar en
    /// pantalla "la guía de esta línea arranca en la semana N" en vez de mostrar columnas vacías
    /// sin explicación. Sale de las filas CARGADAS: si el cliente completa el levante, el aviso
    /// desaparece solo.
    /// </summary>
    private static int? SemanaMinimaConGuiaLevante(
        IEnumerable<Domain.Entities.ProduccionAvicolaRaw> guias)
    {
        int? minima = null;
        foreach (var g in guias)
        {
            if (string.IsNullOrWhiteSpace(g.Edad)) continue;
            var txt = g.Edad.Trim().Replace(",", ".");
            int edad;
            if (!int.TryParse(txt, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out edad))
            {
                var m = System.Text.RegularExpressions.Regex.Match(txt, @"(\d+)");
                if (!m.Success || !int.TryParse(m.Groups[1].Value, out edad)) continue;
            }
            if (!minima.HasValue || edad < minima.Value) minima = edad;
        }
        return minima;
    }

    private int GetSemanaAno(DateTime fecha)
    {
        var calendar = System.Globalization.CultureInfo.CurrentCulture.Calendar;
        return calendar.GetWeekOfYear(fecha, 
            System.Globalization.CalendarWeekRule.FirstDay, 
            DayOfWeek.Monday);
    }
}

