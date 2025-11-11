// src/ZooSanMarino.Infrastructure/Services/LiquidacionTecnicaProduccionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class LiquidacionTecnicaProduccionService : ILiquidacionTecnicaProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly IGuiaGeneticaService _guiaGeneticaService;

    public LiquidacionTecnicaProduccionService(
        ZooSanMarinoContext context,
        ICurrentUser currentUser,
        IGuiaGeneticaService guiaGeneticaService)
    {
        _context = context;
        _currentUser = currentUser;
        _guiaGeneticaService = guiaGeneticaService;
    }

    public async Task<LiquidacionTecnicaProduccionDto> CalcularLiquidacionProduccionAsync(LiquidacionTecnicaProduccionRequest request)
    {
        // 1. Obtener datos del lote y producción inicial
        var lote = await ObtenerLoteAsync(request.LoteId);
        var produccionLote = await ObtenerProduccionLoteAsync(lote);
        
        if (produccionLote == null)
        {
            throw new InvalidOperationException($"No se encontró registro inicial de producción para el lote {request.LoteId}");
        }

        // 2. Calcular semana actual y fecha límite
        var fechaHasta = request.FechaHasta ?? DateTime.Today;
        var semanaActual = CalcularSemana(lote.FechaEncaset, fechaHasta);
        
        // 3. Obtener seguimientos desde semana 26
        var seguimientos = await ObtenerSeguimientosProduccionAsync(lote, fechaHasta);
        
        // 4. Filtrar seguimientos por semana >= 26
        var seguimientosProduccion = seguimientos
            .Where(s => CalcularSemana(lote.FechaEncaset, s.Fecha) >= 26)
            .OrderBy(s => s.Fecha)
            .ToList();

        // 5. Obtener producción inicial
        var hembrasIniciales = produccionLote.AvesInicialesH;
        var machosIniciales = produccionLote.AvesInicialesM;

        // 6. Calcular métricas por etapas
        var etapa1 = await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 1, 25, 33);
        var etapa2 = await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 2, 34, 50);
        var etapa3 = await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 3, 51, null);

        // 7. Calcular métricas acumuladas
        var totales = CalcularMetricasAcumuladas(produccionLote, seguimientosProduccion, lote);

        // 8. Obtener datos de guía genética y comparar
        var comparacionGuia = await CalcularComparacionConGuiaAsync(lote, seguimientosProduccion, semanaActual, totales);

        return new LiquidacionTecnicaProduccionDto(
            lote.LoteId?.ToString() ?? "0",
            lote.LoteNombre,
            lote.FechaEncaset ?? DateTime.MinValue,
            lote.Raza,
            lote.AnoTablaGenetica,
            produccionLote.AvesInicialesH,
            produccionLote.AvesInicialesM,
            produccionLote.HuevosIniciales,
            etapa1,
            etapa2,
            etapa3,
            totales,
            comparacionGuia,
            DateTime.UtcNow,
            seguimientosProduccion.Count,
            seguimientosProduccion.LastOrDefault()?.Fecha,
            semanaActual
        );
    }

    public async Task<bool> ValidarLoteParaLiquidacionProduccionAsync(int loteId)
    {
        var lote = await ObtenerLoteAsync(loteId);
        if (lote == null || !lote.FechaEncaset.HasValue) return false;

        var semanaActual = CalcularSemana(lote.FechaEncaset, DateTime.Today);
        if (semanaActual < 26) return false;

        var seguimientos = await ObtenerSeguimientosProduccionAsync(lote, DateTime.Today);
        return seguimientos.Any(s => CalcularSemana(lote.FechaEncaset, s.Fecha) >= 26);
    }

    public async Task<EtapaLiquidacionDto?> ObtenerResumenEtapaAsync(int loteId, int etapa)
    {
        var lote = await ObtenerLoteAsync(loteId);
        if (lote == null || !lote.FechaEncaset.HasValue) return null;

        var seguimientos = await ObtenerSeguimientosProduccionAsync(lote, DateTime.Today);
        var seguimientosProduccion = seguimientos
            .Where(s => CalcularSemana(lote.FechaEncaset, s.Fecha) >= 26)
            .ToList();

        var produccionLote = await ObtenerProduccionLoteAsync(lote);
        if (produccionLote == null) return null;

        return etapa switch
        {
            1 => await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 1, 25, 33),
            2 => await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 2, 34, 50),
            3 => await CalcularEtapaAsync(seguimientosProduccion, lote, produccionLote, 3, 51, null),
            _ => null
        };
    }

    #region Métodos Privados

    private async Task<Lote> ObtenerLoteAsync(int loteId)
    {
        var lote = await _context.Lotes
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.LoteId == loteId && l.CompanyId == _currentUser.CompanyId);

        if (lote == null)
            throw new ArgumentException($"Lote {loteId} no encontrado");

        return lote;
    }

    private async Task<ProduccionLote?> ObtenerProduccionLoteAsync(Lote lote)
    {
        var loteIdStr = lote.LoteId?.ToString();
        if (string.IsNullOrEmpty(loteIdStr)) return null;

        return await _context.ProduccionLotes
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.LoteId == loteIdStr && p.DeletedAt == null);
    }

    private async Task<List<SeguimientoProduccion>> ObtenerSeguimientosProduccionAsync(Lote lote, DateTime fechaHasta)
    {
        var loteIdStr = lote.LoteId?.ToString();
        if (string.IsNullOrEmpty(loteIdStr)) return new List<SeguimientoProduccion>();

        return await _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdStr && s.Fecha <= fechaHasta)
            .OrderBy(s => s.Fecha)
            .ToListAsync();
    }

    private static int CalcularSemana(DateTime? fechaEncaset, DateTime fechaRegistro)
    {
        if (!fechaEncaset.HasValue) return 0;
        var dias = (fechaRegistro.Date - fechaEncaset.Value.Date).Days;
        return Math.Max(0, (dias / 7) + 1);
    }

    private Task<EtapaLiquidacionDto> CalcularEtapaAsync(
        List<SeguimientoProduccion> seguimientos,
        Lote lote,
        ProduccionLote produccionLote,
        int etapa,
        int semanaDesde,
        int? semanaHasta)
    {
        return Task.FromResult(CalcularEtapa(seguimientos, lote, produccionLote, etapa, semanaDesde, semanaHasta));
    }

    private EtapaLiquidacionDto CalcularEtapa(
        List<SeguimientoProduccion> seguimientos,
        Lote lote,
        ProduccionLote produccionLote,
        int etapa,
        int semanaDesde,
        int? semanaHasta)
    {
        var nombre = etapa switch
        {
            1 => "Etapa 1 (Semana 25-33)",
            2 => "Etapa 2 (Semana 34-50)",
            3 => "Etapa 3 (Semana >50)",
            _ => $"Etapa {etapa}"
        };

        // Filtrar seguimientos de esta etapa
        var seguimientosEtapa = seguimientos
            .Where(s =>
            {
                var semana = CalcularSemana(lote.FechaEncaset, s.Fecha);
                if (semanaHasta.HasValue)
                    return semana >= semanaDesde && semana <= semanaHasta.Value;
                return semana >= semanaDesde;
            })
            .ToList();

        if (seguimientosEtapa.Count == 0)
        {
            return CrearEtapaVacia(etapa, nombre, semanaDesde, semanaHasta);
        }

        // Calcular métricas
        var mortalidadH = seguimientosEtapa.Sum(s => s.MortalidadH);
        var mortalidadM = seguimientosEtapa.Sum(s => s.MortalidadM);
        var seleccionH = seguimientosEtapa.Sum(s => s.SelH);
        var consumoKgH = seguimientosEtapa.Sum(s => (decimal)s.ConsKgH);
        var consumoKgM = seguimientosEtapa.Sum(s => (decimal)s.ConsKgM);
        var huevosTotales = seguimientosEtapa.Sum(s => s.HuevoTot);
        var huevosIncubables = seguimientosEtapa.Sum(s => s.HuevoInc);

        // Obtener producción inicial para calcular porcentajes
        var hembrasIniciales = produccionLote.AvesInicialesH;
        var machosIniciales = produccionLote.AvesInicialesM;

        var porcMortalidadH = hembrasIniciales > 0 ? (decimal)mortalidadH / hembrasIniciales * 100 : 0;
        var porcMortalidadM = machosIniciales > 0 ? (decimal)mortalidadM / machosIniciales * 100 : 0;
        var porcSeleccionH = hembrasIniciales > 0 ? (decimal)seleccionH / hembrasIniciales * 100 : 0;

        // Promedios de huevos por día
        var diasConRegistro = seguimientosEtapa.Count;
        var promedioHuevosPorDia = diasConRegistro > 0 ? (decimal)huevosTotales / diasConRegistro : 0;
        
        // Eficiencia = (Huevos Incubables / Huevos Totales) * 100
        var eficiencia = huevosTotales > 0 ? (decimal)huevosIncubables / huevosTotales * 100 : 0;

        // Último registro de la etapa (para peso y uniformidad)
        var ultimoRegistro = seguimientosEtapa.LastOrDefault();
        var pesoH = ultimoRegistro?.PesoH;
        var pesoM = ultimoRegistro?.PesoM;
        var uniformidad = ultimoRegistro?.Uniformidad;

        // Clasificadora de huevos (totales de la etapa)
        var huevosLimpios = seguimientosEtapa.Sum(s => s.HuevoLimpio);
        var huevosTratados = seguimientosEtapa.Sum(s => s.HuevoTratado);
        var huevosSucios = seguimientosEtapa.Sum(s => s.HuevoSucio);
        var huevosDeformes = seguimientosEtapa.Sum(s => s.HuevoDeforme);
        var huevosBlancos = seguimientosEtapa.Sum(s => s.HuevoBlanco);
        var huevosDobleYema = seguimientosEtapa.Sum(s => s.HuevoDobleYema);
        var huevosPiso = seguimientosEtapa.Sum(s => s.HuevoPiso);
        var huevosPequenos = seguimientosEtapa.Sum(s => s.HuevoPequeno);
        var huevosRotos = seguimientosEtapa.Sum(s => s.HuevoRoto);
        var huevosDesecho = seguimientosEtapa.Sum(s => s.HuevoDesecho);
        var huevosOtro = seguimientosEtapa.Sum(s => s.HuevoOtro);

        // Pesaje semanal (promedio de registros con datos)
        var registrosConPesaje = seguimientosEtapa.Where(s => s.PesoH.HasValue || s.PesoM.HasValue).ToList();
        var pesoPromedioH = registrosConPesaje.Count > 0 
            ? (decimal?)registrosConPesaje.Average(s => (double?)(s.PesoH ?? 0))
            : null;
        var pesoPromedioM = registrosConPesaje.Count > 0 
            ? (decimal?)registrosConPesaje.Average(s => (double?)(s.PesoM ?? 0))
            : null;
        var uniformidadPromedio = registrosConPesaje.Where(s => s.Uniformidad.HasValue).Count() > 0
            ? (decimal?)registrosConPesaje.Where(s => s.Uniformidad.HasValue).Average(s => (double)s.Uniformidad!.Value)
            : null;
        var cvPromedio = registrosConPesaje.Where(s => s.CoeficienteVariacion.HasValue).Count() > 0
            ? (decimal?)registrosConPesaje.Where(s => s.CoeficienteVariacion.HasValue).Average(s => (double)s.CoeficienteVariacion!.Value)
            : null;

        return new EtapaLiquidacionDto(
            etapa,
            nombre,
            semanaDesde,
            semanaHasta,
            seguimientosEtapa.Count,
            mortalidadH,
            mortalidadM,
            porcMortalidadH,
            porcMortalidadM,
            seleccionH,
            porcSeleccionH,
            consumoKgH,
            consumoKgM,
            consumoKgH + consumoKgM,
            huevosTotales,
            huevosIncubables,
            promedioHuevosPorDia,
            eficiencia,
            pesoH,
            pesoM,
            uniformidad,
            huevosLimpios,
            huevosTratados,
            huevosSucios,
            huevosDeformes,
            huevosBlancos,
            huevosDobleYema,
            huevosPiso,
            huevosPequenos,
            huevosRotos,
            huevosDesecho,
            huevosOtro,
            pesoPromedioH,
            pesoPromedioM,
            uniformidadPromedio,
            cvPromedio
        );
    }

    private static EtapaLiquidacionDto CrearEtapaVacia(int etapa, string nombre, int semanaDesde, int? semanaHasta)
    {
        return new EtapaLiquidacionDto(
            Etapa: etapa,
            Nombre: nombre,
            SemanaDesde: semanaDesde,
            SemanaHasta: semanaHasta,
            TotalRegistros: 0,
            MortalidadHembras: 0,
            MortalidadMachos: 0,
            PorcentajeMortalidadHembras: 0,
            PorcentajeMortalidadMachos: 0,
            SeleccionHembras: 0,
            PorcentajeSeleccionHembras: 0,
            ConsumoKgHembras: 0,
            ConsumoKgMachos: 0,
            ConsumoTotalKg: 0,
            HuevosTotales: 0,
            HuevosIncubables: 0,
            PromedioHuevosPorDia: 0,
            EficienciaProduccion: 0,
            PesoHembras: null,
            PesoMachos: null,
            Uniformidad: null,
            HuevosLimpios: 0,
            HuevosTratados: 0,
            HuevosSucios: 0,
            HuevosDeformes: 0,
            HuevosBlancos: 0,
            HuevosDobleYema: 0,
            HuevosPiso: 0,
            HuevosPequenos: 0,
            HuevosRotos: 0,
            HuevosDesecho: 0,
            HuevosOtro: 0,
            PesoPromedioHembras: null,
            PesoPromedioMachos: null,
            UniformidadPromedio: null,
            CoeficienteVariacionPromedio: null
        );
    }

    private MetricasAcumuladasProduccionDto CalcularMetricasAcumuladas(
        ProduccionLote produccionLote,
        List<SeguimientoProduccion> seguimientos,
        Lote lote)
    {
        var hembrasIniciales = produccionLote.AvesInicialesH;
        var machosIniciales = produccionLote.AvesInicialesM;

        var totalMortalidadH = seguimientos.Sum(s => s.MortalidadH);
        var totalMortalidadM = seguimientos.Sum(s => s.MortalidadM);
        var totalSeleccionH = seguimientos.Sum(s => s.SelH);

        var porcMortalidadAcumH = hembrasIniciales > 0 
            ? (decimal)totalMortalidadH / hembrasIniciales * 100 
            : 0;
        var porcMortalidadAcumM = machosIniciales > 0 
            ? (decimal)totalMortalidadM / machosIniciales * 100 
            : 0;
        var porcSeleccionAcumH = hembrasIniciales > 0 
            ? (decimal)totalSeleccionH / hembrasIniciales * 100 
            : 0;

        var consumoTotalKgH = seguimientos.Sum(s => (decimal)s.ConsKgH);
        var consumoTotalKgM = seguimientos.Sum(s => (decimal)s.ConsKgM);
        var consumoTotalKg = consumoTotalKgH + consumoTotalKgM;

        var diasConRegistro = seguimientos.Count;
        var consumoPromedioDiario = diasConRegistro > 0 ? consumoTotalKg / diasConRegistro : 0;

        var totalHuevosTotales = seguimientos.Sum(s => s.HuevoTot);
        var totalHuevosIncubables = seguimientos.Sum(s => s.HuevoInc);
        var promedioHuevosPorDia = diasConRegistro > 0 ? (decimal)totalHuevosTotales / diasConRegistro : 0;
        var eficienciaTotal = totalHuevosTotales > 0 
            ? (decimal)totalHuevosIncubables / totalHuevosTotales * 100 
            : 0;

        // Aves actuales = iniciales - mortalidad - selección
        var avesHembrasActuales = Math.Max(0, hembrasIniciales - totalMortalidadH - totalSeleccionH);
        var avesMachosActuales = Math.Max(0, machosIniciales - totalMortalidadM);
        var totalAvesActuales = avesHembrasActuales + avesMachosActuales;

        return new MetricasAcumuladasProduccionDto(
            totalMortalidadH,
            totalMortalidadM,
            porcMortalidadAcumH,
            porcMortalidadAcumM,
            totalSeleccionH,
            porcSeleccionAcumH,
            consumoTotalKgH,
            consumoTotalKgM,
            consumoTotalKg,
            consumoPromedioDiario,
            totalHuevosTotales,
            totalHuevosIncubables,
            promedioHuevosPorDia,
            eficienciaTotal,
            avesHembrasActuales,
            avesMachosActuales,
            totalAvesActuales
        );
    }

    private async Task<ComparacionGuiaProduccionDto?> CalcularComparacionConGuiaAsync(
        Lote lote,
        List<SeguimientoProduccion> seguimientos,
        int semanaActual,
        MetricasAcumuladasProduccionDto totales)
    {
        if (string.IsNullOrWhiteSpace(lote.Raza) || !lote.AnoTablaGenetica.HasValue)
            return null;

        try
        {
            // Obtener datos de guía genética desde semana 26
            var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(
                lote.Raza, 
                lote.AnoTablaGenetica.Value);

            if (!guias.Any()) return null;

            // Obtener guía para la semana actual (o la más cercana)
            var guiaActual = guias
                .OrderBy(g => Math.Abs(g.Edad - semanaActual))
                .FirstOrDefault();

            if (guiaActual == null) return null;

            // Obtener datos completos de la guía genética (ProduccionAvicolaRaw) para más campos
            var razaNorm = lote.Raza.Trim().ToLower();
            var ano = lote.AnoTablaGenetica.Value.ToString();
            var edadGuia = guiaActual.Edad;
            
            var guiasCompletas = await _context.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p =>
                    p.Raza != null && p.AnioGuia != null &&
                    EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                    p.AnioGuia.Trim() == ano)
                .ToListAsync();

            // Helper para parsear edad
            int? TryParseEdadLocal(string? edadStr)
            {
                if (string.IsNullOrWhiteSpace(edadStr)) return null;
                var s = edadStr.Trim().Replace(",", ".");
                if (int.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var n))
                    return n;
                var match = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n2))
                    return n2;
                return null;
            }

            var guiaCompletaFiltrada = guiasCompletas
                .Where(g =>
                {
                    var edad = TryParseEdadLocal(g.Edad);
                    return edad.HasValue && edad.Value == edadGuia;
                })
                .FirstOrDefault();

            // Obtener datos reales del último seguimiento
            var ultimoSeguimiento = seguimientos.LastOrDefault();
            if (ultimoSeguimiento == null) return null;

            // Calcular diferencias
            var consumoRealH = (decimal)ultimoSeguimiento.ConsKgH * 1000; // Convertir a gramos
            var consumoRealM = (decimal)ultimoSeguimiento.ConsKgM * 1000;
            var consumoGuiaH = (decimal)guiaActual.ConsumoHembras;
            var consumoGuiaM = (decimal)guiaActual.ConsumoMachos;

            var difConsumoH = CalcularDiferenciaPorcentual(consumoRealH, consumoGuiaH);
            var difConsumoM = CalcularDiferenciaPorcentual(consumoRealM, consumoGuiaM);

            var pesoRealH = ultimoSeguimiento.PesoH;
            var pesoRealM = ultimoSeguimiento.PesoM;
            var pesoGuiaH = (decimal)guiaActual.PesoHembras;
            var pesoGuiaM = (decimal)guiaActual.PesoMachos;

            var difPesoH = CalcularDiferenciaPorcentual(pesoRealH, pesoGuiaH);
            var difPesoM = CalcularDiferenciaPorcentual(pesoRealM, pesoGuiaM);

            var mortalidadRealH = seguimientos.Sum(s => s.MortalidadH);
            var mortalidadRealM = seguimientos.Sum(s => s.MortalidadM);
            var mortalidadGuiaH = (decimal)guiaActual.MortalidadHembras;
            var mortalidadGuiaM = (decimal)guiaActual.MortalidadMachos;

            var difMortalidadH = CalcularDiferenciaPorcentual(mortalidadRealH, mortalidadGuiaH);
            var difMortalidadM = CalcularDiferenciaPorcentual(mortalidadRealM, mortalidadGuiaM);

            var uniformidadReal = ultimoSeguimiento.Uniformidad;
            var uniformidadGuia = (decimal)guiaActual.Uniformidad;
            var difUniformidad = CalcularDiferenciaPorcentual(uniformidadReal, uniformidadGuia);

            // Parsear datos adicionales de la guía genética (ProduccionAvicolaRaw)
            var parseDouble = (string? value) => {
                if (string.IsNullOrWhiteSpace(value)) return (decimal?)null;
                var clean = value.Trim().Replace(",", ".");
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
                return null;
            };

            var parseInt = (string? value) => {
                if (string.IsNullOrWhiteSpace(value)) return (decimal?)null;
                var clean = value.Trim().Replace(",", ".");
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
                return null;
            };

            // Datos de producción de huevos de la guía
            var huevosTotalesGuia = parseDouble(guiaCompletaFiltrada?.HTotalAa);
            var porcentajeProduccionGuia = parseDouble(guiaCompletaFiltrada?.ProdPorcentaje);
            var huevosIncubablesGuia = parseDouble(guiaCompletaFiltrada?.HIncAa);
            var pesoHuevoGuia = parseDouble(guiaCompletaFiltrada?.PesoHuevo);
            var masaHuevoGuia = parseDouble(guiaCompletaFiltrada?.MasaHuevo);
            var gramosHuevoTotalGuia = parseDouble(guiaCompletaFiltrada?.GrHuevoT);
            var gramosHuevoIncubableGuia = parseDouble(guiaCompletaFiltrada?.GrHuevoInc);
            var aprovechamientoSemanalGuia = parseDouble(guiaCompletaFiltrada?.AprovSem);
            var aprovechamientoAcumuladoGuia = parseDouble(guiaCompletaFiltrada?.AprovAc);
            var nacimientoPorcentajeGuia = parseDouble(guiaCompletaFiltrada?.NacimPorcentaje);
            var pollitosAveAlojadaGuia = parseDouble(guiaCompletaFiltrada?.PollitoAa);
            var gramosPollitoGuia = parseDouble(guiaCompletaFiltrada?.GrPollito);
            var apareoGuia = parseDouble(guiaCompletaFiltrada?.Apareo);
            var kcalAveDiaHGuia = parseDouble(guiaCompletaFiltrada?.KcalAveDiaH);
            var kcalAveDiaMGuia = parseDouble(guiaCompletaFiltrada?.KcalAveDiaM);
            var retiroAcumuladoHembrasGuia = parseDouble(guiaCompletaFiltrada?.RetiroAcH);
            var retiroAcumuladoMachosGuia = parseDouble(guiaCompletaFiltrada?.RetiroAcM);

            // Calcular datos reales de producción
            var totalRegistros = seguimientos.Count;
            var huevosTotalesRealPromedio = totalRegistros > 0 
                ? seguimientos.Average(s => (decimal)s.HuevoTot)
                : 0m;
            var huevosTotalesReal = totalRegistros > 0 ? (decimal?)huevosTotalesRealPromedio : null;
            
            var huevosIncubablesRealPromedio = totalRegistros > 0
                ? seguimientos.Average(s => (decimal)s.HuevoInc)
                : 0m;
            var huevosIncubablesReal = totalRegistros > 0 ? (decimal?)huevosIncubablesRealPromedio : null;
            
            var registrosConPesoHuevo = seguimientos.Where(s => s.PesoHuevo > 0).ToList();
            var pesoHuevoReal = registrosConPesoHuevo.Count > 0
                ? (decimal?)registrosConPesoHuevo.Average(s => (double)s.PesoHuevo)
                : null;
            // Calcular porcentaje de producción real: (Huevos promedio / Aves actuales) * 100
            // Pero necesitamos considerar que los huevos son por día y las aves son totales
            // Por lo tanto, usamos la eficiencia total ya calculada
            var porcentajeProduccionReal = totales.TotalAvesActuales > 0 && huevosTotalesReal.HasValue
                ? (decimal?)((huevosTotalesReal.Value / totales.TotalAvesActuales) * 100)
                : null;
            var eficienciaReal = huevosTotalesReal.HasValue && huevosTotalesReal.Value > 0
                ? (decimal?)((huevosIncubablesReal ?? 0) / huevosTotalesReal.Value * 100)
                : null;

            // Calcular diferencias
            var difHuevosTotales = CalcularDiferenciaPorcentual(huevosTotalesReal, huevosTotalesGuia);
            var difPorcentajeProduccion = CalcularDiferenciaPorcentual(porcentajeProduccionReal, porcentajeProduccionGuia);
            var difHuevosIncubables = CalcularDiferenciaPorcentual(huevosIncubablesReal, huevosIncubablesGuia);
            var difPesoHuevo = CalcularDiferenciaPorcentual(pesoHuevoReal, pesoHuevoGuia);
            var masaHuevoReal = pesoHuevoReal.HasValue && huevosTotalesReal.HasValue
                ? pesoHuevoReal.Value * huevosTotalesReal.Value
                : (decimal?)null;
            var difMasaHuevo = CalcularDiferenciaPorcentual(masaHuevoReal, masaHuevoGuia);

            return new ComparacionGuiaProduccionDto(
                consumoGuiaH,
                consumoGuiaM,
                difConsumoH,
                difConsumoM,
                pesoGuiaH,
                pesoGuiaM,
                difPesoH,
                difPesoM,
                mortalidadGuiaH,
                mortalidadGuiaM,
                difMortalidadH,
                difMortalidadM,
                uniformidadGuia,
                uniformidadReal,
                difUniformidad,
                huevosTotalesGuia,
                porcentajeProduccionGuia,
                huevosIncubablesGuia,
                pesoHuevoGuia,
                masaHuevoGuia,
                gramosHuevoTotalGuia,
                gramosHuevoIncubableGuia,
                aprovechamientoSemanalGuia,
                aprovechamientoAcumuladoGuia,
                huevosTotalesReal,
                porcentajeProduccionReal,
                huevosIncubablesReal,
                pesoHuevoReal,
                eficienciaReal,
                difHuevosTotales,
                difPorcentajeProduccion,
                difHuevosIncubables,
                difPesoHuevo,
                difMasaHuevo,
                nacimientoPorcentajeGuia,
                pollitosAveAlojadaGuia,
                gramosPollitoGuia,
                apareoGuia,
                kcalAveDiaHGuia,
                kcalAveDiaMGuia,
                retiroAcumuladoHembrasGuia,
                retiroAcumuladoMachosGuia
            );
        }
        catch
        {
            return null;
        }
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorGuia)
    {
        if (!valorReal.HasValue || !valorGuia.HasValue || valorGuia.Value == 0)
            return null;

        return ((valorReal.Value - valorGuia.Value) / valorGuia.Value) * 100;
    }

    #endregion
}

