// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionService.cs
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService : IReporteTecnicoProduccionService
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

    // Helpers cross-concern: usados por Diario, Semanal, Sublotes y Tabs (partial: visibles entre
    // todos los archivos de Funciones/).

    /// <summary>
    /// Flag `companies.clasificacion_huevo_por_items` de la empresa activa: con clasificación por
    /// ítem, Incubable/Cargado (Diario), HuevosIncub/HCarga (Cuadro) y toda la hoja "Clasificación
    /// Huevo Comercio" salen de `huevo_inc`/las 11 columnas legacy fijas, siempre en 0 -- el
    /// desglose real vive en metadata.huevoItems, que estos reportes no leen. Mismo patrón que
    /// `DiasAlimentoPrevioEncaset` en ReporteContableService.
    /// </summary>
    private async Task<bool> ResolverClasificacionHuevoPorItemsAsync(CancellationToken ct) =>
        await _ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == _currentUser.CompanyId)
            .Select(c => c.ClasificacionHuevoPorItems)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Flag `companies.semanas_ciclo_postura_por_raza`: la empresa corta las etapas del ave por
    /// raza (8 alistamiento / 16 levante / 4 levante-en-producción / 74 u 84 de postura) en vez de
    /// los cortes fijos históricos. Ver <see cref="SemanasCicloPosturaCalculos"/>.
    /// </summary>
    private async Task<bool> ResolverSemanasCicloPorRazaAsync(CancellationToken ct) =>
        await _ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == _currentUser.CompanyId)
            .Select(c => c.SemanasCicloPosturaPorRaza)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Proyecta filas de guía a la forma que consume <see cref="GuiaMetricasDisponiblesCalculos"/>.
    /// Vive acá (helper cross-concern del ancla) porque lo usan Tabs y Cuadro.
    /// </summary>
    private static List<FilaGuiaMetricas> AFilasGuiaMetricas(
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
    /// Semana MÍNIMA con guía cargada, o <c>null</c> si no hay guía. Es lo que deja al reporte
    /// avisar "la guía de esta línea arranca en la semana N" en vez de mostrar una columna vacía
    /// sin explicación.
    /// </summary>
    private static int? SemanaMinimaConGuia(IEnumerable<Domain.Entities.ProduccionAvicolaRaw> guias)
    {
        int? minima = null;
        foreach (var g in guias)
        {
            var edad = ParsearEdadGuia(g.Edad);
            if (edad.HasValue && (!minima.HasValue || edad.Value < minima.Value))
                minima = edad.Value;
        }
        return minima;
    }

    /// <summary>
    /// Edad (semana) de una fila de guía. La guía compartida la guarda como texto y admite comas y
    /// sufijos, por eso el fallback por regex -- misma tolerancia que ya tenían Cuadro y Tabs inline.
    /// </summary>
    private static int? ParsearEdadGuia(string? val)
    {
        if (string.IsNullOrWhiteSpace(val)) return null;
        var s = val.Trim().Replace(",", ".");
        if (int.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var n)) return n;
        var m = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n2) ? n2 : null;
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
}

