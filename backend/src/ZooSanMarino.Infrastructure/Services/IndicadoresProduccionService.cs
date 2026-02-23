using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;
using System.Globalization;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio de indicadores semanales de producción.
/// Alineado con ProduccionService: misma resolución de lote en producción (CompanyId + LotePadreId/LoteId)
/// y misma fuente de datos (tabla unificada seguimiento_diario, tipo produccion).
/// Incluye comparativos con guía genética cuando está disponible.
/// </summary>
public class IndicadoresProduccionService : IIndicadoresProduccionService
{
    private const string TipoProduccion = "produccion";
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

    /// <summary>
    /// Resuelve CompanyId activo: header X-Active-Company-Id (por nombre) o claim del usuario.
    /// Misma lógica que el resto del proyecto para filtrado por compañía.
    /// </summary>
    private async Task<int> ResolveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var byName = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName.Trim());
            if (byName.HasValue && byName.Value > 0)
                return byName.Value;
        }
        var claimId = _currentUser.CompanyId;
        if (claimId > 0)
            return claimId;
        throw new InvalidOperationException("No se pudo determinar la compañía activa (CompanyId). Verifique sesión o header X-Active-Company-Id.");
    }

    /// <summary>
    /// Obtiene indicadores semanales de producción.
    /// Flujo alineado con ProduccionService.ListarSeguimientoAsync: mismo criterio de lote en producción y misma tabla de datos.
    /// </summary>
    public async Task<IndicadoresProduccionResponse> ObtenerIndicadoresSemanalesAsync(IndicadoresProduccionRequest request)
    {
        // ─── 1) Resolver compañía (igual que en el resto del módulo Producción) ───
        int companyId;
        try
        {
            companyId = await ResolveCompanyIdAsync();
        }
        catch (InvalidOperationException ex)
        {
            throw new ArgumentException(ex.Message, ex);
        }

        // ─── 2) Resolver lote en producción (LPP o legacy) ───
        Lote? loteProd = null;
        DateTime fechaEncaset;
        int avesHembrasIniciales;
        int avesMachosIniciales;
        string raza = "";
        int? anoTablaGenetica = null;

        if (request.LotePosturaProduccionId.HasValue && request.LotePosturaProduccionId.Value > 0)
        {
            // ─── Flujo LPP: lote_postura_produccion ───
            var lppId = request.LotePosturaProduccionId.Value;
            var lpp = await _context.LotePosturaProduccion
                .AsNoTracking()
                .FirstOrDefaultAsync(l =>
                    l.LotePosturaProduccionId == lppId
                    && l.CompanyId == companyId
                    && l.DeletedAt == null);

            if (lpp == null)
            {
                throw new ArgumentException(
                    $"No se encontró lote postura producción {lppId}. " +
                    "Verifique que pertenezca a su compañía activa.");
            }

            // Fecha base: fecha_inicio_produccion o fecha_encaset desde Levante
            var fechaRef = lpp.FechaInicioProduccion;
            if (!fechaRef.HasValue && lpp.LotePosturaLevanteId.HasValue)
            {
                var lev = await _context.LotePosturaLevante
                    .AsNoTracking()
                    .Where(x => x.LotePosturaLevanteId == lpp.LotePosturaLevanteId && x.DeletedAt == null)
                    .Select(x => x.FechaEncaset)
                    .FirstOrDefaultAsync();
                fechaRef = lev;
            }
            if (!fechaRef.HasValue && lpp.FechaEncaset.HasValue)
                fechaRef = lpp.FechaEncaset;

            if (!fechaRef.HasValue)
            {
                throw new ArgumentException(
                    $"El lote postura producción {lppId} no tiene fecha de inicio de producción ni fecha de encaset. " +
                    "Necesaria para calcular semanas.");
            }

            fechaEncaset = fechaRef.Value;
            avesHembrasIniciales = lpp.AvesHInicial ?? lpp.HembrasInicialesProd ?? 0;
            avesMachosIniciales = lpp.AvesMInicial ?? lpp.MachosInicialesProd ?? 0;
            raza = lpp.Raza ?? "";
            anoTablaGenetica = lpp.AnoTablaGenetica;

            var fromLpp = _context.SeguimientoDiario
                .AsNoTracking()
                .Where(s =>
                    s.TipoSeguimiento == TipoProduccion
                    && s.LotePosturaProduccionId == lppId);

            if (request.FechaDesde.HasValue)
                fromLpp = fromLpp.Where(s => s.Fecha >= request.FechaDesde.Value);
            if (request.FechaHasta.HasValue)
                fromLpp = fromLpp.Where(s => s.Fecha <= request.FechaHasta.Value);

            var seguimientosLpp = await fromLpp
                .OrderBy(s => s.Fecha)
                .Select(s => new SeguimientoProduccionRegistroDto(
                    s.Fecha,
                    s.MortalidadHembras ?? 0,
                    s.MortalidadMachos ?? 0,
                    s.SelH ?? 0,
                    s.SelM ?? 0,
                    (decimal)(s.ConsumoKgHembras ?? 0),
                    (decimal)(s.ConsumoKgMachos ?? 0),
                    s.HuevoTot ?? 0,
                    s.HuevoInc ?? 0,
                    s.HuevoLimpio ?? 0,
                    s.HuevoTratado ?? 0,
                    s.HuevoSucio ?? 0,
                    s.HuevoDeforme ?? 0,
                    s.HuevoBlanco ?? 0,
                    s.HuevoDobleYema ?? 0,
                    s.HuevoPiso ?? 0,
                    s.HuevoPequeno ?? 0,
                    s.HuevoRoto ?? 0,
                    s.HuevoDesecho ?? 0,
                    s.HuevoOtro ?? 0,
                    (decimal)(s.PesoHuevo ?? 0),
                    s.Etapa ?? 0,
                    s.PesoH,
                    s.PesoM,
                    s.Uniformidad,
                    s.CoeficienteVariacion,
                    s.ObservacionesPesaje))
                .ToListAsync();

            return await CalcularIndicadoresAsync(
                seguimientosLpp,
                fechaEncaset,
                avesHembrasIniciales,
                avesMachosIniciales,
                raza,
                anoTablaGenetica,
                companyId,
                request);
        }

        // ─── Flujo legacy: Lote en fase Producción ───
        var loteId = request.LoteId;
        if (loteId <= 0)
        {
            throw new ArgumentException(
                "Debe especificar LoteId (legacy) o LotePosturaProduccionId (flujo LPP) para obtener indicadores semanales.");
        }

        loteProd = await _context.Lotes
            .AsNoTracking()
            .Where(l =>
                l.CompanyId == companyId
                && l.DeletedAt == null
                && l.Fase == "Produccion"
                && l.LotePadreId == loteId)
            .OrderBy(l => l.LoteId)
            .FirstOrDefaultAsync();

        if (loteProd == null)
        {
            loteProd = await _context.Lotes
                .AsNoTracking()
                .Where(l =>
                    l.CompanyId == companyId
                    && l.DeletedAt == null
                    && l.Fase == "Produccion"
                    && l.LoteId == loteId)
                .FirstOrDefaultAsync();
        }

        if (loteProd == null)
        {
            throw new ArgumentException(
                $"No se encontró lote en producción para el lote {loteId}. " +
                "Verifique que: 1) El lote esté en fase Producción, 2) Pertenezca a su compañía activa, 3) Exista registro inicial de producción.");
        }

        var fechaReferencia = loteProd.FechaInicioProduccion;
        if (!fechaReferencia.HasValue && loteProd.LotePadreId.HasValue)
        {
            var padre = await _context.Lotes
                .AsNoTracking()
                .Where(l => l.LoteId == loteProd.LotePadreId && l.DeletedAt == null)
                .Select(l => l.FechaEncaset)
                .FirstOrDefaultAsync();
            fechaReferencia = padre;
        }
        if (!fechaReferencia.HasValue)
        {
            throw new ArgumentException(
                $"El lote en producción {loteProd.LoteId} no tiene fecha de inicio de producción ni fecha de encaset (lote padre). " +
                "Necesaria para calcular semanas.");
        }

        fechaEncaset = fechaReferencia.Value;
        avesHembrasIniciales = loteProd.HembrasInicialesProd ?? 0;
        avesMachosIniciales = loteProd.MachosInicialesProd ?? 0;
        var loteIdStr = loteProd.LoteId!.Value.ToString();

        var fromUnificado = _context.SeguimientoDiario
            .AsNoTracking()
            .Where(s => s.TipoSeguimiento == TipoProduccion && s.LoteId == loteIdStr);

        if (request.FechaDesde.HasValue)
            fromUnificado = fromUnificado.Where(s => s.Fecha >= request.FechaDesde.Value);
        if (request.FechaHasta.HasValue)
            fromUnificado = fromUnificado.Where(s => s.Fecha <= request.FechaHasta.Value);

        var seguimientos = await fromUnificado
            .OrderBy(s => s.Fecha)
            .Select(s => new SeguimientoProduccionRegistroDto(
                s.Fecha,
                s.MortalidadHembras ?? 0,
                s.MortalidadMachos ?? 0,
                s.SelH ?? 0,
                s.SelM ?? 0,
                (decimal)(s.ConsumoKgHembras ?? 0),
                (decimal)(s.ConsumoKgMachos ?? 0),
                s.HuevoTot ?? 0,
                s.HuevoInc ?? 0,
                s.HuevoLimpio ?? 0,
                s.HuevoTratado ?? 0,
                s.HuevoSucio ?? 0,
                s.HuevoDeforme ?? 0,
                s.HuevoBlanco ?? 0,
                s.HuevoDobleYema ?? 0,
                s.HuevoPiso ?? 0,
                s.HuevoPequeno ?? 0,
                s.HuevoRoto ?? 0,
                s.HuevoDesecho ?? 0,
                s.HuevoOtro ?? 0,
                (decimal)(s.PesoHuevo ?? 0),
                s.Etapa ?? 0,
                s.PesoH,
                s.PesoM,
                s.Uniformidad,
                s.CoeficienteVariacion,
                s.ObservacionesPesaje))
            .ToListAsync();

        if (seguimientos.Count == 0)
        {
            return new IndicadoresProduccionResponse(
                new List<IndicadorProduccionSemanalDto>(),
                0,
                0,
                0,
                false);
        }

        raza = loteProd.Raza ?? "";
        anoTablaGenetica = loteProd.AnoTablaGenetica;
        if ((string.IsNullOrWhiteSpace(raza) || !anoTablaGenetica.HasValue) && loteProd.LotePadreId.HasValue)
        {
            var padre = await _context.Lotes
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.LoteId == loteProd.LotePadreId && l.DeletedAt == null);
            if (padre != null)
            {
                raza = padre.Raza ?? "";
                anoTablaGenetica = padre.AnoTablaGenetica;
            }
        }

        return await CalcularIndicadoresAsync(
            seguimientos,
            fechaEncaset,
            avesHembrasIniciales,
            avesMachosIniciales,
            raza,
            anoTablaGenetica,
            companyId,
            request);
    }

    private async Task<IndicadoresProduccionResponse> CalcularIndicadoresAsync(
        List<SeguimientoProduccionRegistroDto> seguimientos,
        DateTime fechaEncaset,
        int avesHembrasIniciales,
        int avesMachosIniciales,
        string raza,
        int? anoTablaGenetica,
        int companyId,
        IndicadoresProduccionRequest request)
    {
        var tieneGuiaGenetica = !string.IsNullOrWhiteSpace(raza) && anoTablaGenetica.HasValue;
        var guias = new List<ZooSanMarino.Application.DTOs.GuiaGeneticaDto>();

        if (tieneGuiaGenetica && anoTablaGenetica.HasValue)
        {
            try
            {
                guias = (await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(
                    raza!,
                    anoTablaGenetica.Value)).ToList();
            }
            catch
            {
                tieneGuiaGenetica = false;
            }
        }

        // ─── 6) Guía raw (ProduccionAvicolaRaw) para comparativos de huevos ───
        var guiaRawBySemana = new Dictionary<int, ProduccionAvicolaRaw>();
        if (tieneGuiaGenetica && anoTablaGenetica.HasValue)
        {
            var razaNorm = raza!.Trim().ToLowerInvariant();
            var ano = anoTablaGenetica.Value.ToString(CultureInfo.InvariantCulture);
            var raw = await _context.ProduccionAvicolaRaw
                .AsNoTracking()
                .Where(p =>
                    p.CompanyId == companyId
                    && p.DeletedAt == null
                    && p.Raza != null
                    && p.AnioGuia != null
                    && EF.Functions.Like(p.Raza.Trim().ToLower(), razaNorm)
                    && p.AnioGuia.Trim() == ano)
                .ToListAsync();

            guiaRawBySemana = raw
                .Select(r => new { r, edad = TryParseEdadNumerica(r.Edad) })
                .Where(x => x.edad.HasValue)
                .GroupBy(x => x.edad!.Value)
                .ToDictionary(g => g.Key, g => g.First().r);
        }

        // ─── 7) Agrupar por semana (semana 1 = días 0-6 desde fechaEncaset) ───
        var seguimientosPorSemana = seguimientos
            .GroupBy(s =>
            {
                var dias = (s.Fecha.Date - fechaEncaset.Date).Days;
                return (dias / 7) + 1;
            })
            .OrderBy(g => g.Key)
            .ToList();

        var semanasFiltradas = seguimientosPorSemana
            .Where(g =>
                (!request.SemanaDesde.HasValue || g.Key >= request.SemanaDesde.Value)
                && (!request.SemanaHasta.HasValue || g.Key <= request.SemanaHasta.Value))
            .ToList();

        // ─── 8) Calcular indicadores por semana (con comparativos cuando hay guía) ───
        var indicadores = new List<IndicadorProduccionSemanalDto>();
        var avesHembrasActuales = avesHembrasIniciales;
        var avesMachosActuales = avesMachosIniciales;

        foreach (var grupoSemana in semanasFiltradas)
        {
            var semana = grupoSemana.Key;
            var seguimientosSemana = grupoSemana.OrderBy(s => s.Fecha).ToList();

            var diasInicio = (semana - 1) * 7;
            var fechaInicioSemana = fechaEncaset.AddDays(diasInicio).Date;
            var fechaFinSemana = fechaInicioSemana.AddDays(6).Date;

            var mortalidadH = seguimientosSemana.Sum(s => s.MortalidadH);
            var mortalidadM = seguimientosSemana.Sum(s => s.MortalidadM);
            var seleccionH = seguimientosSemana.Sum(s => s.SelH);
            var consumoKgH = seguimientosSemana.Sum(s => (decimal)s.ConsKgH);
            var consumoKgM = seguimientosSemana.Sum(s => (decimal)s.ConsKgM);
            var huevosTotales = seguimientosSemana.Sum(s => s.HuevoTot);
            var huevosIncubables = seguimientosSemana.Sum(s => s.HuevoInc);
            var promedioHuevosPorDia = seguimientosSemana.Count > 0 ? (decimal)huevosTotales / seguimientosSemana.Count : 0;

            var eficiencia = avesHembrasActuales + avesMachosActuales > 0
                ? (decimal)promedioHuevosPorDia / (avesHembrasActuales + avesMachosActuales) * 100
                : 0;

            var pesoPromedioH = seguimientosSemana.Where(s => s.PesoH.HasValue).Count() > 0
                ? (decimal?)seguimientosSemana.Where(s => s.PesoH.HasValue).Average(s => (double)s.PesoH!.Value)
                : null;
            var pesoPromedioM = seguimientosSemana.Where(s => s.PesoM.HasValue).Count() > 0
                ? (decimal?)seguimientosSemana.Where(s => s.PesoM.HasValue).Average(s => (double)s.PesoM!.Value)
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

            var porcMortalidadH = avesHembrasActuales > 0 ? (decimal)mortalidadH / avesHembrasActuales * 100 : 0;
            var porcMortalidadM = avesMachosActuales > 0 ? (decimal)mortalidadM / avesMachosActuales * 100 : 0;
            var porcSeleccionH = avesHembrasActuales > 0 ? (decimal)seleccionH / avesHembrasActuales * 100 : 0;

            var avesHembrasInicioSemana = avesHembrasActuales + mortalidadH + seleccionH;
            var avesMachosInicioSemana = avesMachosActuales + mortalidadM;

            // Comparativos con guía: la guía genética usa Edad = semanas de VIDA (26, 27, 28...).
            // Nuestra "semana" es semanas desde inicio de producción (1, 2, 3...). Semana producción 1 = edad 26.
            const int EdadInicioProduccion = 26;
            var edadGuia = (EdadInicioProduccion - 1) + semana; // producción 1 -> 26, 2 -> 27, etc.
            var guiaSemana = guias.FirstOrDefault(g => g.Edad == edadGuia);
            decimal? consumoGuiaH = null, consumoGuiaM = null, mortalidadGuiaH = null, mortalidadGuiaM = null;
            decimal? pesoGuiaH = null, pesoGuiaM = null, uniformidadGuia = null;
            decimal? huevosTotalesGuia = null, huevosIncubablesGuia = null, porcentajeProduccionGuia = null, pesoHuevoGuia = null;

            if (guiaSemana != null)
            {
                consumoGuiaH = (decimal)guiaSemana.ConsumoHembras;
                consumoGuiaM = (decimal)guiaSemana.ConsumoMachos;
                mortalidadGuiaH = (decimal)guiaSemana.MortalidadHembras;
                mortalidadGuiaM = (decimal)guiaSemana.MortalidadMachos;
                pesoGuiaH = (decimal)guiaSemana.PesoHembras / 1000m;
                pesoGuiaM = (decimal)guiaSemana.PesoMachos / 1000m;
                uniformidadGuia = (decimal)guiaSemana.Uniformidad;
            }
            // ProduccionAvicolaRaw también está indexado por Edad (semanas de vida)
            if (guiaRawBySemana.TryGetValue(edadGuia, out var rawSemana) && rawSemana != null)
            {
                huevosTotalesGuia = ParseDecimal(rawSemana.HTotalAa);
                huevosIncubablesGuia = ParseDecimal(rawSemana.HIncAa);
                porcentajeProduccionGuia = ParseDecimal(rawSemana.ProdPorcentaje);
                pesoHuevoGuia = ParseDecimal(rawSemana.PesoHuevo);
            }

            var consumoRealH = seguimientosSemana.Count > 0 && avesHembrasInicioSemana > 0
                ? (decimal?)(consumoKgH * 1000 / (seguimientosSemana.Count * avesHembrasInicioSemana))
                : null;
            var consumoRealM = seguimientosSemana.Count > 0 && avesMachosInicioSemana > 0
                ? (decimal?)(consumoKgM * 1000 / (seguimientosSemana.Count * avesMachosInicioSemana))
                : null;

            var difConsumoH = CalcularDiferenciaPorcentual(consumoRealH, consumoGuiaH);
            var difConsumoM = CalcularDiferenciaPorcentual(consumoRealM, consumoGuiaM);
            var difMortalidadH = CalcularDiferenciaPorcentual((decimal?)porcMortalidadH, mortalidadGuiaH);
            var difMortalidadM = CalcularDiferenciaPorcentual((decimal?)porcMortalidadM, mortalidadGuiaM);
            var difPesoH = CalcularDiferenciaPorcentual(pesoPromedioH, pesoGuiaH);
            var difPesoM = CalcularDiferenciaPorcentual(pesoPromedioM, pesoGuiaM);
            var difUniformidad = CalcularDiferenciaPorcentual(uniformidadPromedio, uniformidadGuia);
            var difHuevosTotales = CalcularDiferenciaPorcentual((decimal?)promedioHuevosPorDia, huevosTotalesGuia);
            var difHuevosIncubables = seguimientosSemana.Count > 0
                ? CalcularDiferenciaPorcentual((decimal?)huevosIncubables / seguimientosSemana.Count, huevosIncubablesGuia)
                : null;
            var difPorcentajeProduccion = CalcularDiferenciaPorcentual((decimal?)eficiencia, porcentajeProduccionGuia);
            var difPesoHuevo = CalcularDiferenciaPorcentual(pesoHuevoPromedio, pesoHuevoGuia);

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
                seguimientosSemana.Sum(s => s.HuevoLimpio),
                seguimientosSemana.Sum(s => s.HuevoTratado),
                seguimientosSemana.Sum(s => s.HuevoSucio),
                seguimientosSemana.Sum(s => s.HuevoDeforme),
                seguimientosSemana.Sum(s => s.HuevoBlanco),
                seguimientosSemana.Sum(s => s.HuevoDobleYema),
                seguimientosSemana.Sum(s => s.HuevoPiso),
                seguimientosSemana.Sum(s => s.HuevoPequeno),
                seguimientosSemana.Sum(s => s.HuevoRoto),
                seguimientosSemana.Sum(s => s.HuevoDesecho),
                seguimientosSemana.Sum(s => s.HuevoOtro),
                avesHembrasInicioSemana,
                avesMachosInicioSemana,
                avesHembrasActuales,
                avesMachosActuales));

        }

        var semanaInicial = semanasFiltradas.Any() ? semanasFiltradas.Min(g => g.Key) : 0;
        var semanaFinal = semanasFiltradas.Any() ? semanasFiltradas.Max(g => g.Key) : 0;

        var tieneDatosGuia = tieneGuiaGenetica && guias.Any();
        string? mensajeGuia = null;
        if (tieneGuiaGenetica && !guias.Any())
            mensajeGuia = $"El lote tiene Raza ({raza}) y Año genético ({anoTablaGenetica}) pero no hay datos de guía cargados para esa combinación en su compañía. Cargue la guía genética (produccion_avicola_raw) para Raza/Ano correspondiente.";

        return new IndicadoresProduccionResponse(
            indicadores,
            indicadores.Count,
            semanaInicial,
            semanaFinal,
            tieneDatosGuia,
            mensajeGuia);
    }

    public async Task<IndicadorProduccionSemanalDto?> ObtenerIndicadorSemanaAsync(int loteId, int semana)
    {
        var request = new IndicadoresProduccionRequest(loteId, SemanaDesde: semana, SemanaHasta: semana);
        var response = await ObtenerIndicadoresSemanalesAsync(request);
        return response.Indicadores.FirstOrDefault();
    }

    #region Helpers

    private static int? TryParseEdadNumerica(string? edadStr)
    {
        if (string.IsNullOrWhiteSpace(edadStr)) return null;
        var s = edadStr.Trim().Replace(",", ".");
        if (int.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var n))
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
        return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : null;
    }

    private static decimal? CalcularDiferenciaPorcentual(decimal? valorReal, decimal? valorGuia)
    {
        if (!valorReal.HasValue || !valorGuia.HasValue || valorGuia.Value == 0)
            return null;
        return ((valorReal.Value - valorGuia.Value) / valorGuia.Value) * 100;
    }

    #endregion
}
