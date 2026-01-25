using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using System.Globalization;

namespace ZooSanMarino.Infrastructure.Services;

public class IndicadoresProduccionService : IIndicadoresProduccionService
{
    private readonly ZooSanMarinoContext _context;
    private readonly IGuiaGeneticaService _guiaGeneticaService;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    public IndicadoresProduccionService(
        ZooSanMarinoContext context,
        IGuiaGeneticaService guiaGeneticaService,
        ICurrentUser currentUser,
        ICompanyResolver companyResolver)
    {
        _context = context;
        _guiaGeneticaService = guiaGeneticaService;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int?> TryResolveCompanyIdFromHeaderAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (byName.HasValue) return byName.Value;
        }
        return null;
    }

    public async Task<IndicadoresProduccionResponse> ObtenerIndicadoresSemanalesAsync(IndicadoresProduccionRequest request)
    {
        // 1) Obtener lote (y su granja) sin depender del header, para inferir compañía correcta
        var lote = await _context.Lotes
            .AsNoTracking()
            .Where(l => l.LoteId == request.LoteId && l.DeletedAt == null)
            .FirstOrDefaultAsync();

        if (lote == null || !lote.FechaEncaset.HasValue)
            throw new ArgumentException($"Lote {request.LoteId} no encontrado o sin fecha de encaset");

        // 2) Resolver CompanyId "real" desde la granja (fuente de verdad)
        var farmCompanyId = await _context.Farms
            .AsNoTracking()
            .Where(f => f.Id == lote.GranjaId)
            .Select(f => (int?)f.CompanyId)
            .SingleOrDefaultAsync();

        var loteCompanyId = farmCompanyId ?? lote.CompanyId;
        if (loteCompanyId <= 0)
            throw new ArgumentException("No fue posible determinar la compañía del lote (CompanyId inválido).");

        // 3) Validación contra compañía activa (si viene header); si no viene, usa el claim
        var headerCompanyId = await TryResolveCompanyIdFromHeaderAsync();
        var userCompanyId = headerCompanyId ?? _currentUser.CompanyId;
        if (userCompanyId > 0 && userCompanyId != loteCompanyId)
            throw new ArgumentException("El lote no pertenece a la compañía activa.");

        var companyId = loteCompanyId;

        var loteIdStr = request.LoteId.ToString();
        var produccionLote = await _context.ProduccionLotes
            .AsNoTracking()
            // Compatibilidad: algunos registros antiguos pueden tener CompanyId=0
            .Where(p => p.LoteId == loteIdStr && p.DeletedAt == null && (p.CompanyId == companyId || p.CompanyId == 0))
            .FirstOrDefaultAsync();

        if (produccionLote == null)
            throw new ArgumentException($"No se encontró producción para el lote {request.LoteId}");

        // 2. Obtener seguimientos de producción diaria usando LoteId como string
        var seguimientosQuery = _context.SeguimientoProduccion
            .AsNoTracking()
            .Where(s => s.LoteId == loteIdStr);

        if (request.FechaDesde.HasValue)
            seguimientosQuery = seguimientosQuery.Where(s => s.Fecha >= request.FechaDesde.Value);

        if (request.FechaHasta.HasValue)
            seguimientosQuery = seguimientosQuery.Where(s => s.Fecha <= request.FechaHasta.Value);

        var seguimientos = await seguimientosQuery
            .OrderBy(s => s.Fecha)
            .ToListAsync();

        if (seguimientos.Count == 0)
            return new IndicadoresProduccionResponse(
                new List<IndicadorProduccionSemanalDto>(),
                0,
                0,
                0,
                false
            );

        // 3. Obtener datos de guía genética si están disponibles
        var tieneGuiaGenetica = !string.IsNullOrWhiteSpace(lote.Raza) && lote.AnoTablaGenetica.HasValue;
        var guias = tieneGuiaGenetica && lote.AnoTablaGenetica.HasValue
            ? (await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(lote.Raza!, lote.AnoTablaGenetica.Value)).ToList()
            : new List<ZooSanMarino.Application.DTOs.GuiaGeneticaDto>();

        // Prefetch de la guía completa (ProduccionAvicolaRaw) UNA sola vez para los campos de huevos
        Dictionary<int, ProduccionAvicolaRaw> guiaRawBySemana = new();
        if (tieneGuiaGenetica && lote.AnoTablaGenetica.HasValue)
        {
            var razaNorm = lote.Raza!.Trim().ToLowerInvariant();
            var ano = lote.AnoTablaGenetica.Value.ToString(CultureInfo.InvariantCulture);

            var raw = await _context.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p =>
                    p.CompanyId == companyId &&
                    p.DeletedAt == null &&
                    p.Raza != null &&
                    p.AnioGuia != null &&
                    EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm) &&
                    p.AnioGuia.Trim() == ano
                )
                .ToListAsync();

            guiaRawBySemana = raw
                .Select(r => new { r, edad = TryParseEdadNumerica(r.Edad) })
                .Where(x => x.edad.HasValue)
                .GroupBy(x => x.edad!.Value)
                .ToDictionary(g => g.Key, g => g.First().r);
        }

        // 4. Agrupar seguimientos por semana
        var fechaEncaset = lote.FechaEncaset.Value;
        var seguimientosPorSemana = seguimientos
            .GroupBy(s =>
            {
                var diasDesdeEncaset = (s.Fecha.Date - fechaEncaset.Date).Days;
                var semana = (diasDesdeEncaset / 7) + 1; // Semana 1 = días 0-6, Semana 2 = días 7-13, etc.
                return semana;
            })
            .OrderBy(g => g.Key)
            .ToList();

        // 5. Filtrar por rango de semanas si se especifica
        var semanasFiltradas = seguimientosPorSemana
            .Where(g =>
            {
                if (request.SemanaDesde.HasValue && g.Key < request.SemanaDesde.Value)
                    return false;
                if (request.SemanaHasta.HasValue && g.Key > request.SemanaHasta.Value)
                    return false;
                return true;
            })
            .ToList();

        // 6. Calcular indicadores para cada semana
        var indicadores = new List<IndicadorProduccionSemanalDto>();
        var avesHembrasActuales = produccionLote.AvesInicialesH;
        var avesMachosActuales = produccionLote.AvesInicialesM;

        foreach (var grupoSemana in semanasFiltradas)
        {
            var semana = grupoSemana.Key;
            var seguimientosSemana = grupoSemana.OrderBy(s => s.Fecha).ToList();
            
            // Calcular el período real de la semana basándose en fechaEncaset
            // Semana 1 = días 0-6 desde fechaEncaset
            // Semana 2 = días 7-13, etc.
            var diasDesdeInicioSemana = (semana - 1) * 7;
            var fechaInicioSemana = fechaEncaset.AddDays(diasDesdeInicioSemana).Date;
            var fechaFinSemana = fechaInicioSemana.AddDays(6).Date; // 6 días después (total 7 días)

            // Calcular métricas de la semana
            var mortalidadH = seguimientosSemana.Sum(s => s.MortalidadH);
            var mortalidadM = seguimientosSemana.Sum(s => s.MortalidadM);
            var seleccionH = seguimientosSemana.Sum(s => s.SelH);
            var consumoKgH = seguimientosSemana.Sum(s => (decimal)s.ConsKgH);
            var consumoKgM = seguimientosSemana.Sum(s => (decimal)s.ConsKgM);
            var huevosTotales = seguimientosSemana.Sum(s => s.HuevoTot);
            var huevosIncubables = seguimientosSemana.Sum(s => s.HuevoInc);
            var promedioHuevosPorDia = seguimientosSemana.Count > 0 ? (decimal)huevosTotales / seguimientosSemana.Count : 0;
            
            // Eficiencia basada en aves al inicio de la semana
            var eficiencia = avesHembrasActuales + avesMachosActuales > 0
                ? (decimal)promedioHuevosPorDia / (avesHembrasActuales + avesMachosActuales) * 100
                : 0;

            // Pesos promedio (de registros con datos)
            var registrosConPesoH = seguimientosSemana.Where(s => s.PesoH.HasValue).ToList();
            var pesoPromedioH = registrosConPesoH.Count > 0
                ? (decimal?)registrosConPesoH.Average(s => (double)s.PesoH!.Value)
                : null;

            var registrosConPesoM = seguimientosSemana.Where(s => s.PesoM.HasValue).ToList();
            var pesoPromedioM = registrosConPesoM.Count > 0
                ? (decimal?)registrosConPesoM.Average(s => (double)s.PesoM!.Value)
                : null;

            var uniformidadPromedio = seguimientosSemana.Where(s => s.Uniformidad.HasValue).Count() > 0
                ? (decimal?)seguimientosSemana.Where(s => s.Uniformidad.HasValue).Average(s => (double)s.Uniformidad!.Value)
                : null;

            var cvPromedio = seguimientosSemana.Where(s => s.CoeficienteVariacion.HasValue).Count() > 0
                ? (decimal?)seguimientosSemana.Where(s => s.CoeficienteVariacion.HasValue).Average(s => (double)s.CoeficienteVariacion!.Value)
                : null;

            var pesoHuevoPromedio = seguimientosSemana.Where(s => s.PesoHuevo > 0).Count() > 0
                ? (decimal?)seguimientosSemana.Where(s => s.PesoHuevo > 0).Average(s => (double)s.PesoHuevo)
                : null;

            // Clasificadora de huevos
            var huevosLimpios = seguimientosSemana.Sum(s => s.HuevoLimpio);
            var huevosTratados = seguimientosSemana.Sum(s => s.HuevoTratado);
            var huevosSucios = seguimientosSemana.Sum(s => s.HuevoSucio);
            var huevosDeformes = seguimientosSemana.Sum(s => s.HuevoDeforme);
            var huevosBlancos = seguimientosSemana.Sum(s => s.HuevoBlanco);
            var huevosDobleYema = seguimientosSemana.Sum(s => s.HuevoDobleYema);
            var huevosPiso = seguimientosSemana.Sum(s => s.HuevoPiso);
            var huevosPequenos = seguimientosSemana.Sum(s => s.HuevoPequeno);
            var huevosRotos = seguimientosSemana.Sum(s => s.HuevoRoto);
            var huevosDesecho = seguimientosSemana.Sum(s => s.HuevoDesecho);
            var huevosOtro = seguimientosSemana.Sum(s => s.HuevoOtro);

            // Porcentajes
            var porcMortalidadH = avesHembrasActuales > 0 ? (decimal)mortalidadH / avesHembrasActuales * 100 : 0;
            var porcMortalidadM = avesMachosActuales > 0 ? (decimal)mortalidadM / avesMachosActuales * 100 : 0;
            var porcSeleccionH = avesHembrasActuales > 0 ? (decimal)seleccionH / avesHembrasActuales * 100 : 0;

            // Obtener datos de guía genética para esta semana
            var guiaSemana = guias.FirstOrDefault(g => g.Edad == semana);
            
            // Calcular datos de guía genética
            decimal? consumoGuiaH = null;
            decimal? consumoGuiaM = null;
            decimal? mortalidadGuiaH = null;
            decimal? mortalidadGuiaM = null;
            decimal? pesoGuiaH = null;
            decimal? pesoGuiaM = null;
            decimal? uniformidadGuia = null;
            decimal? huevosTotalesGuia = null;
            decimal? huevosIncubablesGuia = null;
            decimal? porcentajeProduccionGuia = null;
            decimal? pesoHuevoGuia = null;

            if (guiaSemana != null)
            {
                consumoGuiaH = (decimal)guiaSemana.ConsumoHembras;
                consumoGuiaM = (decimal)guiaSemana.ConsumoMachos;
                mortalidadGuiaH = (decimal)guiaSemana.MortalidadHembras;
                mortalidadGuiaM = (decimal)guiaSemana.MortalidadMachos;
                pesoGuiaH = (decimal)guiaSemana.PesoHembras / 1000; // Convertir gramos a kg
                pesoGuiaM = (decimal)guiaSemana.PesoMachos / 1000;
                uniformidadGuia = (decimal)guiaSemana.Uniformidad;

                if (guiaRawBySemana.TryGetValue(semana, out var guiaCompletaSemana) && guiaCompletaSemana != null)
                {
                    huevosTotalesGuia = ParseDecimal(guiaCompletaSemana.HTotalAa);
                    huevosIncubablesGuia = ParseDecimal(guiaCompletaSemana.HIncAa);
                    porcentajeProduccionGuia = ParseDecimal(guiaCompletaSemana.ProdPorcentaje);
                    pesoHuevoGuia = ParseDecimal(guiaCompletaSemana.PesoHuevo);
                }
            }

            // Calcular aves al inicio de la semana (antes de mortalidad y selección)
            // Estas son las aves que teníamos al inicio para calcular promedios correctos
            var avesHembrasInicioSemana = avesHembrasActuales + mortalidadH + seleccionH;
            var avesMachosInicioSemana = avesMachosActuales + mortalidadM;

            // Calcular consumo real en g/ave/día
            // consumoKgH y consumoKgM son el total de kg consumidos en la semana
            // Para obtener g/ave/día: (totalKg * 1000) / (días con registro * aves al inicio de semana)
            var consumoRealH = seguimientosSemana.Count > 0 && avesHembrasInicioSemana > 0
                ? (decimal?)(consumoKgH * 1000 / (seguimientosSemana.Count * avesHembrasInicioSemana)) // g/ave/día
                : null;
            var consumoRealM = seguimientosSemana.Count > 0 && avesMachosInicioSemana > 0
                ? (decimal?)(consumoKgM * 1000 / (seguimientosSemana.Count * avesMachosInicioSemana)) // g/ave/día
                : null;

            var difConsumoH = CalcularDiferenciaPorcentual(consumoRealH, consumoGuiaH);
            var difConsumoM = CalcularDiferenciaPorcentual(consumoRealM, consumoGuiaM);
            var difMortalidadH = CalcularDiferenciaPorcentual((decimal?)porcMortalidadH, mortalidadGuiaH);
            var difMortalidadM = CalcularDiferenciaPorcentual((decimal?)porcMortalidadM, mortalidadGuiaM);
            var difPesoH = CalcularDiferenciaPorcentual(pesoPromedioH, pesoGuiaH);
            var difPesoM = CalcularDiferenciaPorcentual(pesoPromedioM, pesoGuiaM);
            var difUniformidad = CalcularDiferenciaPorcentual(uniformidadPromedio, uniformidadGuia);
            var difHuevosTotales = CalcularDiferenciaPorcentual((decimal?)promedioHuevosPorDia, huevosTotalesGuia);
            var difHuevosIncubables = CalcularDiferenciaPorcentual((decimal?)huevosIncubables / seguimientosSemana.Count, huevosIncubablesGuia);
            var difPorcentajeProduccion = CalcularDiferenciaPorcentual((decimal?)eficiencia, porcentajeProduccionGuia);
            var difPesoHuevo = CalcularDiferenciaPorcentual(pesoHuevoPromedio, pesoHuevoGuia);

            // Aves al final de la semana (actualizar para la siguiente iteración)
            avesHembrasActuales = Math.Max(0, avesHembrasActuales - mortalidadH - seleccionH);
            avesMachosActuales = Math.Max(0, avesMachosActuales - mortalidadM);

            indicadores.Add(new IndicadorProduccionSemanalDto(
                semana,
                fechaInicioSemana,
                fechaFinSemana,
                seguimientosSemana.Count,
                mortalidadH,
                mortalidadM,
                porcMortalidadH,
                porcMortalidadM,
                (int)(mortalidadGuiaH ?? 0),
                (int)(mortalidadGuiaM ?? 0),
                difMortalidadH,
                difMortalidadM,
                seleccionH,
                porcSeleccionH,
                consumoKgH,
                consumoKgM,
                consumoKgH + consumoKgM,
                seguimientosSemana.Count > 0 ? (consumoKgH + consumoKgM) / seguimientosSemana.Count : 0,
                consumoGuiaH,
                consumoGuiaM,
                difConsumoH,
                difConsumoM,
                huevosTotales,
                huevosIncubables,
                promedioHuevosPorDia,
                eficiencia,
                huevosTotalesGuia,
                huevosIncubablesGuia,
                porcentajeProduccionGuia,
                difHuevosTotales,
                difHuevosIncubables,
                difPorcentajeProduccion,
                pesoHuevoPromedio,
                pesoHuevoGuia,
                difPesoHuevo,
                pesoPromedioH,
                pesoPromedioM,
                pesoGuiaH,
                pesoGuiaM,
                difPesoH,
                difPesoM,
                uniformidadPromedio,
                uniformidadGuia,
                difUniformidad,
                cvPromedio,
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
                avesHembrasInicioSemana,
                avesMachosInicioSemana,
                avesHembrasActuales,
                avesMachosActuales
            ));
        }

        var semanaInicial = semanasFiltradas.Any() ? semanasFiltradas.Min(g => g.Key) : 0;
        var semanaFinal = semanasFiltradas.Any() ? semanasFiltradas.Max(g => g.Key) : 0;

        return new IndicadoresProduccionResponse(
            indicadores,
            indicadores.Count,
            semanaInicial,
            semanaFinal,
            tieneGuiaGenetica && guias.Any()
        );
    }

    public async Task<IndicadorProduccionSemanalDto?> ObtenerIndicadorSemanaAsync(int loteId, int semana)
    {
        var request = new IndicadoresProduccionRequest(
            loteId,
            SemanaDesde: semana,
            SemanaHasta: semana
        );

        var response = await ObtenerIndicadoresSemanalesAsync(request);
        return response.Indicadores.FirstOrDefault();
    }

    #region Helpers

    private static int? TryParseEdadNumerica(string? edadStr)
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

    private static decimal? ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var clean = value.Trim().Replace(",", ".");
        if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var result))
            return result;
        return null;
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorGuia)
    {
        if (!valorReal.HasValue || !valorGuia.HasValue || valorGuia.Value == 0)
            return null;
        return ((valorReal.Value - valorGuia.Value) / valorGuia.Value) * 100;
    }

    #endregion
}

