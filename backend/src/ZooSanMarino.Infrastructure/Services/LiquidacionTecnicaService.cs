// src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LiquidacionTecnicaService : ILiquidacionTecnicaService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;

    public LiquidacionTecnicaService(ZooSanMarinoContext context, ICurrentUser currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<LiquidacionTecnicaDto> CalcularLiquidacionAsync(LiquidacionTecnicaRequest request)
    {
        // 1. Obtener datos del lote
        var lote = await ObtenerLoteAsync(request.LoteId);
        
        // 2. Obtener seguimiento hasta semana 25 o fecha especificada
        var seguimientos = await ObtenerSeguimientosAsync(request.LoteId, request.FechaHasta);
        
        // 3. Obtener datos de guía genética
        var datosGuia = await ObtenerDatosGuiaAsync(lote.Raza, lote.AnoTablaGenetica);
        
        // 4. Calcular métricas acumuladas
        var metricas = CalcularMetricasAcumuladas(lote, seguimientos);
        
        // 5. Calcular diferencias con guía
        var diferencias = CalcularDiferenciasConGuia(metricas, datosGuia);
        
        return new LiquidacionTecnicaDto(
            lote.LoteId?.ToString() ?? "0",
            lote.LoteNombre,
            lote.FechaEncaset ?? DateTime.MinValue,
            lote.Raza,
            lote.AnoTablaGenetica,
            lote.CodigoGuiaGenetica,
            
            // Datos iniciales
            lote.HembrasL,
            lote.MachosL,
            lote.AvesEncasetadas,
            
            // Mortalidad
            metricas.PorcentajeMortalidadHembras,
            metricas.PorcentajeMortalidadMachos,
            
            // Selección
            metricas.PorcentajeSeleccionHembras,
            metricas.PorcentajeSeleccionMachos,
            
            // Error de sexaje
            metricas.PorcentajeErrorSexajeHembras,
            metricas.PorcentajeErrorSexajeMachos,
            
            // Retiro total
            metricas.PorcentajeRetiroTotalHembras,
            metricas.PorcentajeRetiroTotalMachos,
            metricas.PorcentajeRetiroTotalGeneral,
            
            // Retiro guía
            diferencias.PorcentajeRetiroGuia,
            
            // Consumo
            metricas.ConsumoTotalGramos,
            diferencias.ConsumoGuiaGramos,
            diferencias.PorcentajeDiferenciaConsumo,
            
            // Peso
            metricas.PesoFinalHembras,
            metricas.PesoFinalMachos,
            diferencias.PesoGuiaHembras,
            diferencias.PesoGuiaMachos,
            diferencias.PorcentajeDiferenciaPesoHembras,
            diferencias.PorcentajeDiferenciaPesoMachos,
            
            // Uniformidad
            metricas.UniformidadFinalHembras,
            metricas.UniformidadFinalMachos,
            diferencias.UniformidadGuiaHembras,
            diferencias.UniformidadGuiaMachos,
            diferencias.PorcentajeDiferenciaUniformidadHembras,
            diferencias.PorcentajeDiferenciaUniformidadMachos,
            
            // Metadatos
            DateTime.UtcNow,
            seguimientos.Count,
            seguimientos.LastOrDefault()?.FechaRegistro
        );
    }

    public async Task<LiquidacionTecnicaCompletaDto> ObtenerLiquidacionCompletaAsync(LiquidacionTecnicaRequest request)
    {
        var liquidacion = await CalcularLiquidacionAsync(request);
        
        var seguimientos = await ObtenerSeguimientosAsync(request.LoteId, request.FechaHasta);
        var lote = await ObtenerLoteAsync(request.LoteId);
        
        var detalleSeguimiento = seguimientos.Select((s, index) => new DetalleSeguimientoLiquidacionDto(
            s.FechaRegistro,
            CalcularSemana(lote.FechaEncaset, s.FechaRegistro),
            s.MortalidadHembras,
            s.MortalidadMachos,
            s.SelH,
            s.SelM,
            s.ErrorSexajeHembras,
            s.ErrorSexajeMachos,
            s.ConsumoKgHembras,
            s.ConsumoKgMachos,
            s.PesoPromH,
            s.PesoPromM,
            s.UniformidadH,
            s.UniformidadM
        )).ToList();

        var datosGuia = await ObtenerDatosGuiaAsync(lote.Raza, lote.AnoTablaGenetica);

        return new LiquidacionTecnicaCompletaDto(
            liquidacion,
            detalleSeguimiento,
            datosGuia
        );
    }

    public async Task<bool> ValidarLoteParaLiquidacionAsync(int loteId)
    {
        var loteExiste = await _context.Lotes
            .Where(l => l.LoteId == loteId && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .AnyAsync();

        if (!loteExiste) return false;

        var tieneSeguimiento = await _context.SeguimientoLoteLevante
            .Where(s => s.LoteId == loteId)
            .AnyAsync();

        return tieneSeguimiento;
    }

    #region Métodos Privados

    private async Task<Domain.Entities.Lote> ObtenerLoteAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == loteId && 
                       l.CompanyId == _currentUser.CompanyId && 
                       l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null)
            throw new InvalidOperationException($"Lote '{loteId}' no encontrado o no pertenece a la compañía.");

        return lote;
    }

    private async Task<List<Domain.Entities.SeguimientoLoteLevante>> ObtenerSeguimientosAsync(int loteId, DateTime? fechaHasta)
    {
        var query = _context.SeguimientoLoteLevante
            .AsNoTracking()
            .Where(s => s.LoteId == loteId);

        if (fechaHasta.HasValue)
        {
            query = query.Where(s => s.FechaRegistro.Date <= fechaHasta.Value.Date);
        }

        // Filtrar hasta semana 25 (175 días aproximadamente)
        var lote = await ObtenerLoteAsync(loteId);
        if (lote.FechaEncaset.HasValue)
        {
            var fechaMaxima = lote.FechaEncaset.Value.AddDays(175); // Semana 25
            query = query.Where(s => s.FechaRegistro <= fechaMaxima);
        }

        return await query
            .OrderBy(s => s.FechaRegistro)
            .ToListAsync();
    }

    private async Task<DatosGuiaGeneticaDto?> ObtenerDatosGuiaAsync(string? raza, int? anoTablaGenetica)
    {
        if (string.IsNullOrEmpty(raza) || !anoTablaGenetica.HasValue)
            return null;

        // Intentar con diferentes formatos de edad: "175" (días) o "25" (semanas)
        var edadBuscar = new[] { "175", "25", "25.0" };
        Domain.Entities.ProduccionAvicolaRaw? datosGuia = null;

        foreach (var edad in edadBuscar)
        {
            datosGuia = await _context.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p => p.CompanyId == _currentUser.CompanyId &&
                           p.DeletedAt == null &&
                           p.Raza == raza &&
                           p.AnioGuia == anoTablaGenetica.ToString() &&
                           p.Edad == edad)
                .FirstOrDefaultAsync();

            if (datosGuia != null) break;
        }

        if (datosGuia == null) return null;

        return new DatosGuiaGeneticaDto(
            datosGuia.AnioGuia,
            datosGuia.Raza,
            datosGuia.Edad,
            ParseDecimal(datosGuia.PesoH),
            ParseDecimal(datosGuia.PesoM),
            ParseDecimal(datosGuia.Uniformidad),
            ParseDecimal(datosGuia.ConsAcH),
            ParseDecimal(datosGuia.RetiroAcH)
        );
    }

    private MetricasAcumuladas CalcularMetricasAcumuladas(Domain.Entities.Lote lote, List<Domain.Entities.SeguimientoLoteLevante> seguimientos)
    {
        var hembrasIniciales = lote.HembrasL ?? 0;
        var machosIniciales = lote.MachosL ?? 0;

        // Acumular mortalidad, selección y errores
        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadHembras);
        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadMachos);
        var totalSeleccionH = seguimientos.Sum(s => s.SelH);
        var totalSeleccionM = seguimientos.Sum(s => s.SelM);
        var totalErrorH = seguimientos.Sum(s => s.ErrorSexajeHembras);
        var totalErrorM = seguimientos.Sum(s => s.ErrorSexajeMachos);

        // Calcular porcentajes
        var porcMortalidadH = hembrasIniciales > 0 ? (decimal)totalMortalidadH / hembrasIniciales * 100 : 0;
        var porcMortalidadM = machosIniciales > 0 ? (decimal)totalMortalidadM / machosIniciales * 100 : 0;
        var porcSeleccionH = hembrasIniciales > 0 ? (decimal)totalSeleccionH / hembrasIniciales * 100 : 0;
        var porcSeleccionM = machosIniciales > 0 ? (decimal)totalSeleccionM / machosIniciales * 100 : 0;
        var porcErrorH = hembrasIniciales > 0 ? (decimal)totalErrorH / hembrasIniciales * 100 : 0;
        var porcErrorM = machosIniciales > 0 ? (decimal)totalErrorM / machosIniciales * 100 : 0;

        // Retiro total
        var porcRetiroH = porcMortalidadH + porcSeleccionH + porcErrorH;
        var porcRetiroM = porcMortalidadM + porcSeleccionM + porcErrorM;
        var totalAves = hembrasIniciales + machosIniciales;
        var porcRetiroGeneral = totalAves > 0 ? 
            (decimal)(totalMortalidadH + totalMortalidadM + totalSeleccionH + totalSeleccionM + totalErrorH + totalErrorM) / totalAves * 100 : 0;

        // Consumo total
        var consumoTotal = (decimal)seguimientos.Sum(s => s.ConsumoKgHembras + (s.ConsumoKgMachos ?? 0)) * 1000; // Convertir a gramos

        // Datos finales (último registro)
        var ultimoSeguimiento = seguimientos.LastOrDefault();
        var pesoFinalH = ultimoSeguimiento?.PesoPromH;
        var pesoFinalM = ultimoSeguimiento?.PesoPromM;
        var uniformidadFinalH = ultimoSeguimiento?.UniformidadH;
        var uniformidadFinalM = ultimoSeguimiento?.UniformidadM;

        return new MetricasAcumuladas(
            porcMortalidadH, porcMortalidadM,
            porcSeleccionH, porcSeleccionM,
            porcErrorH, porcErrorM,
            porcRetiroH, porcRetiroM, porcRetiroGeneral,
            consumoTotal,
            (decimal?)pesoFinalH, (decimal?)pesoFinalM,
            (decimal?)uniformidadFinalH, (decimal?)uniformidadFinalM
        );
    }

    private DiferenciasConGuia CalcularDiferenciasConGuia(MetricasAcumuladas metricas, DatosGuiaGeneticaDto? guia)
    {
        if (guia == null)
        {
            return new DiferenciasConGuia(null, null, null, null, null, null, null, null, null, null, null, null);
        }

        // Calcular diferencias porcentuales
        var difConsumo = CalcularDiferenciaPorcentual(metricas.ConsumoTotalGramos, guia.ConsumoAcumulado);
        var difPesoH = CalcularDiferenciaPorcentual(metricas.PesoFinalHembras, guia.PesoHembras);
        var difPesoM = CalcularDiferenciaPorcentual(metricas.PesoFinalMachos, guia.PesoMachos);
        var difUniformidadH = CalcularDiferenciaPorcentual(metricas.UniformidadFinalHembras, guia.Uniformidad);
        var difUniformidadM = CalcularDiferenciaPorcentual(metricas.UniformidadFinalMachos, guia.Uniformidad);

        return new DiferenciasConGuia(
            guia.PorcentajeRetiro,
            guia.ConsumoAcumulado,
            difConsumo,
            guia.PesoHembras,
            guia.PesoMachos,
            difPesoH,
            difPesoM,
            guia.Uniformidad,
            guia.Uniformidad,
            difUniformidadH,
            difUniformidadM,
            guia.ConsumoAcumulado
        );
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorGuia)
    {
        if (!valorReal.HasValue || !valorGuia.HasValue || valorGuia.Value == 0)
            return null;

        return ((valorReal.Value - valorGuia.Value) / valorGuia.Value) * 100;
    }

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return decimal.TryParse(value.Replace(",", "."), System.Globalization.NumberStyles.Float, 
                              System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static int CalcularSemana(DateTime? fechaEncaset, DateTime fechaRegistro)
    {
        if (!fechaEncaset.HasValue)
            return 0;

        var dias = (fechaRegistro - fechaEncaset.Value).Days;
        return (dias / 7) + 1;
    }

    #endregion

    #region Clases de Apoyo

    private record MetricasAcumuladas(
        decimal PorcentajeMortalidadHembras,
        decimal PorcentajeMortalidadMachos,
        decimal PorcentajeSeleccionHembras,
        decimal PorcentajeSeleccionMachos,
        decimal PorcentajeErrorSexajeHembras,
        decimal PorcentajeErrorSexajeMachos,
        decimal PorcentajeRetiroTotalHembras,
        decimal PorcentajeRetiroTotalMachos,
        decimal PorcentajeRetiroTotalGeneral,
        decimal ConsumoTotalGramos,
        decimal? PesoFinalHembras,
        decimal? PesoFinalMachos,
        decimal? UniformidadFinalHembras,
        decimal? UniformidadFinalMachos
    );

    private record DiferenciasConGuia(
        decimal? PorcentajeRetiroGuia,
        decimal? ConsumoGuiaGramos,
        decimal? PorcentajeDiferenciaConsumo,
        decimal? PesoGuiaHembras,
        decimal? PesoGuiaMachos,
        decimal? PorcentajeDiferenciaPesoHembras,
        decimal? PorcentajeDiferenciaPesoMachos,
        decimal? UniformidadGuiaHembras,
        decimal? UniformidadGuiaMachos,
        decimal? PorcentajeDiferenciaUniformidadHembras,
        decimal? PorcentajeDiferenciaUniformidadMachos,
        decimal? ConsumoGuia
    );

    #endregion
}
