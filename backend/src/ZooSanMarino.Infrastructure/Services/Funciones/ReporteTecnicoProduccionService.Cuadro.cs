// src/ZooSanMarino.Infrastructure/Services/Funciones/ReporteTecnicoProduccionService.Cuadro.cs
// Reporte de PRODUCCION en formato cuadro (matriz semana x indicador) para un lote/sublote.
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Domain.Entities;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ReporteTecnicoProduccionService
{
    public async Task<ReporteTecnicoProduccionCuadroCompletoDto> GenerarReporteCuadroAsync(
        int loteId,
        DateTime? fechaInicio,
        DateTime? fechaFin,
        bool consolidarSublotes = false,
        CancellationToken ct = default)
    {
        // Obtener el reporte semanal completo primero (loteId = LotePosturaProduccionId)
        var reporteCompleto = await GenerarReporteDiarioAsync(loteId, fechaInicio, fechaFin, consolidarSublotes, ct);

        // Obtener LPP para guía genética (loteId es LotePosturaProduccionId)
        var lpp = await _ctx.LotePosturaProduccion
            .AsNoTracking()
            .Include(l => l.Farm)
            .Include(l => l.Nucleo)
            .FirstOrDefaultAsync(l => l.LotePosturaProduccionId == loteId && l.CompanyId == _currentUser.CompanyId && l.DeletedAt == null, ct);

        if (lpp == null)
            throw new InvalidOperationException($"Lote producción con ID {loteId} no encontrado");

        // Obtener datos de guía genética si están disponibles
        var guiasProduccion = new List<GuiaGeneticaDto>();
        if (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue)
        {
            try
            {
                var guias = await _guiaGeneticaService.ObtenerGuiaGeneticaProduccionAsync(
                    lpp.Raza,
                    lpp.AnoTablaGenetica.Value);
                guiasProduccion = guias.ToList();
            }
            catch
            {
                // Si no hay guía genética, continuar sin valores amarillos
            }
        }

        // Obtener datos completos de ProduccionAvicolaRaw para valores adicionales
        var guiasCompletas = new List<Domain.Entities.ProduccionAvicolaRaw>();
        if (!string.IsNullOrWhiteSpace(lpp.Raza) && lpp.AnoTablaGenetica.HasValue)
        {
            var razaNorm = lpp.Raza.Trim().ToLower();
            var ano = lpp.AnoTablaGenetica.Value.ToString();

            // Santa Reyes tiene su guia en tabla propia (F2.2). Se pregunta primero; si la empresa
            // no tiene guia propia -Sanmarino, Panama, Ecuador- la lista vuelve vacia y corre la
            // consulta de siempre, sin tocarla.
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

        // Convertir datos semanales a formato Cuadro con valores de guía genética
        var datosCuadro = new List<ReporteTecnicoProduccionCuadroDto>();
        
        foreach (var semanal in reporteCompleto.DatosSemanales)
        {
            // Obtener guía genética para esta semana (edad en semanas de producción)
            var edadProduccionSemanas = semanal.EdadInicioSemanas;
            var guiaSemana = guiasProduccion.FirstOrDefault(g => g.Edad == edadProduccionSemanas);
            
            // Helper para parsear edad en ProduccionAvicolaRaw
            int? TryParseEdad(string? edadStr)
            {
                if (string.IsNullOrWhiteSpace(edadStr)) return null;
                var s = edadStr.Trim().Replace(",", ".");
                if (int.TryParse(s, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out var n))
                    return n;
                var match = System.Text.RegularExpressions.Regex.Match(s, @"(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var n2))
                    return n2;
                return null;
            }

            var guiaCompletaSemana = guiasCompletas
                .Where(g =>
                {
                    var edad = TryParseEdad(g.Edad);
                    return edad.HasValue && edad.Value == edadProduccionSemanas;
                })
                .FirstOrDefault();

            // Helper para parsear valores decimales
            decimal? ParseDecimal(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) return null;
                var clean = value.Trim().Replace(",", ".");
                if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var result))
                    return result;
                return null;
            }

            // Calcular valores acumulados y promedios
            var datosHastaSemana = reporteCompleto.DatosSemanales
                .Where(d => d.Semana <= semanal.Semana)
                .ToList();

            // Mortalidad acumulada
            var mortalidadAcumHembras = datosHastaSemana.Sum(d => d.MortalidadHembrasSemanal);
            var mortalidadAcumMachos = datosHastaSemana.Sum(d => d.MortalidadMachosSemanal);
            var avesInicialesHembras = reporteCompleto.LoteInfo.NumeroHembrasIniciales ?? 0;
            var avesInicialesMachos = reporteCompleto.LoteInfo.NumeroMachosIniciales ?? 0;
            
            var mortalidadAcumPorcentajeHembras = avesInicialesHembras > 0
                ? (decimal)mortalidadAcumHembras / avesInicialesHembras * 100
                : 0;
            var mortalidadAcumPorcentajeMachos = avesInicialesMachos > 0
                ? (decimal)mortalidadAcumMachos / avesInicialesMachos * 100
                : 0;

            // Huevos acumulados
            var huevosAcum = datosHastaSemana.Sum(d => d.HuevosTotalesSemanal);
            
            // Consumo acumulado
            var consumoAcumHembras = datosHastaSemana.Sum(d => d.KilosAlimentoHembrasSemanal);
            var consumoAcumMachos = datosHastaSemana.Sum(d => d.KilosAlimentoMachosSemanal);

            // Crear DTO del cuadro
            var cuadro = new ReporteTecnicoProduccionCuadroDto(
                Semana: semanal.Semana,
                Fecha: semanal.FechaInicioSemana,
                EdadProduccionSemanas: edadProduccionSemanas,
                AvesFinHembras: semanal.SaldoFinHembras,
                AvesFinMachos: semanal.SaldoFinMachos,
                // MORTALIDAD HEMBRAS
                MortalidadHembrasN: semanal.MortalidadHembrasSemanal,
                MortalidadHembrasDescPorcentajeSem: avesInicialesHembras > 0
                    ? (decimal)semanal.MortalidadHembrasSemanal / avesInicialesHembras * 100
                    : 0,
                MortalidadHembrasPorcentajeAcum: mortalidadAcumPorcentajeHembras,
                MortalidadHembrasStandarM: guiaSemana != null ? (decimal?)guiaSemana.MortalidadHembras : null,
                MortalidadHembrasAcumStandar: null, // Se calcularía acumulando valores de guía
                // MORTALIDAD MACHOS
                MortalidadMachosN: semanal.MortalidadMachosSemanal,
                MortalidadMachosDescPorcentajeSem: avesInicialesMachos > 0
                    ? (decimal)semanal.MortalidadMachosSemanal / avesInicialesMachos * 100
                    : 0,
                MortalidadMachosPorcentajeAcum: mortalidadAcumPorcentajeMachos,
                MortalidadMachosStandarM: guiaSemana != null ? (decimal?)guiaSemana.MortalidadMachos : null,
                MortalidadMachosAcumStandar: null,
                // PRODUCCION TOTAL DE HUEVOS
                HuevosVentaSemana: semanal.HuevosTotalesSemanal,
                HuevosAcum: huevosAcum,
                PorcentajeSem: semanal.PorcentajePosturaPromedio,
                PorcentajeRoss: ParseDecimal(guiaCompletaSemana?.ProdPorcentaje),
                Taa: datosHastaSemana.Count > 0 ? huevosAcum / datosHastaSemana.Count : 0,
                TaaRoss: ParseDecimal(guiaCompletaSemana?.HTotalAa),
                // HUEVOS ENVIADOS PLANTA
                EnviadosPlanta: semanal.HuevosEnviadosPlantaSemanal,
                AcumEnviaP: datosHastaSemana.Sum(d => d.HuevosEnviadosPlantaSemanal),
                PorcentajeEnviaP: semanal.PorcentajeEnviadoPlantaPromedio,
                PorcentajeHala: null, // % HALA - se obtendría de guía genética si existe
                // HUEVO INCUBABLE
                HuevosIncub: semanal.HuevosIncubablesSemanal,
                PorcentajeDescarte: semanal.HuevosTotalesSemanal > 0
                    ? (decimal)(semanal.HuevosTotalesSemanal - semanal.HuevosIncubablesSemanal) / semanal.HuevosTotalesSemanal * 100
                    : 0,
                PorcentajeAcumIncub: datosHastaSemana.Sum(d => d.HuevosTotalesSemanal) > 0
                    ? (decimal)datosHastaSemana.Sum(d => d.HuevosIncubablesSemanal) / datosHastaSemana.Sum(d => d.HuevosTotalesSemanal) * 100
                    : 0,
                Laa: datosHastaSemana.Count > 0 
                    ? (decimal)datosHastaSemana.Sum(d => d.HuevosIncubablesSemanal) / datosHastaSemana.Count
                    : 0,
                StdRoss: ParseDecimal(guiaCompletaSemana?.HIncAa),
                // HUEVOS CARGADOS Y POLLITOS
                HCarga: semanal.HuevosCargadosSemanal,
                HCargaAcu: datosHastaSemana.Sum(d => d.HuevosCargadosSemanal),
                VHuevo: semanal.VentaHuevoSemanal ?? 0,
                VHuevoPollitos: semanal.PollitosVendidosSemanal ?? 0,
                PollAcum: datosHastaSemana.Sum(d => d.PollitosVendidosSemanal ?? 0),
                Paa: datosHastaSemana.Count > 0
                    ? (decimal)datosHastaSemana.Sum(d => d.PollitosVendidosSemanal ?? 0) / datosHastaSemana.Count
                    : 0,
                PaaRoss: ParseDecimal(guiaCompletaSemana?.PollitoAa),
                // CONSUMO DE ALIMENTO HEMBRA
                KgSemHembra: semanal.KilosAlimentoHembrasSemanal,
                AcumHembra: consumoAcumHembras,
                AcumAaHembra: datosHastaSemana.Count > 0 ? consumoAcumHembras / datosHastaSemana.Count : 0,
                StAcumHembra: guiaSemana != null ? (decimal?)guiaSemana.ConsumoHembras * 7 / 1000 : null, // Convertir gramos/día a kg/semana
                LoteHembra: null,
                StGrHembra: guiaSemana != null ? (decimal?)guiaSemana.ConsumoHembras : null,
                // CONSUMO DE ALIMENTO MACHO
                KgSemMachos: semanal.KilosAlimentoMachosSemanal,
                AcumMachos: consumoAcumMachos,
                AcumAaMachos: datosHastaSemana.Count > 0 ? consumoAcumMachos / datosHastaSemana.Count : 0,
                StAcumMachos: guiaSemana != null ? (decimal?)guiaSemana.ConsumoMachos * 7 / 1000 : null,
                GrDiaMachos: semanal.KilosAlimentoMachosSemanal * 1000 / 7, // Convertir kg/semana a gramos/día
                StGrMachos: guiaSemana != null ? (decimal?)guiaSemana.ConsumoMachos : null,
                // PESOS
                PesoHembraKg: semanal.PesoHembraPromedio,
                PesoHembraStd: guiaSemana != null ? (decimal?)guiaSemana.PesoHembras / 1000 : null, // Convertir gramos a kg
                PesoMachosKg: semanal.PesoMachosPromedio,
                PesoMachosStd: guiaSemana != null ? (decimal?)guiaSemana.PesoMachos / 1000 : null,
                PesoHuevoSem: semanal.PesoHuevoPromedio,
                PesoHuevoStd: ParseDecimal(guiaCompletaSemana?.PesoHuevo),
                MasaSem: semanal.PesoHuevoPromedio * (decimal)semanal.PorcentajePosturaPromedio / 100, // Aproximación
                MasaStd: ParseDecimal(guiaCompletaSemana?.MasaHuevo),
                // % APROV
                PorcentajeAprovSem: null, // Se calcularía si hay datos de aprovechamiento
                PorcentajeAprovStd: ParseDecimal(guiaCompletaSemana?.AprovSem),
                // TIPO DE ALIMENTO
                TipoAlimento: null, // Se obtendría de seguimiento si existe
                // OBSERVACIONES
                Observaciones: null
            );

            datosCuadro.Add(cuadro);
        }

        return new ReporteTecnicoProduccionCuadroCompletoDto(
            reporteCompleto.LoteInfo,
            datosCuadro
        );
    }
}
