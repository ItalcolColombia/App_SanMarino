// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoProduccionExcelService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para exportar reportes técnicos de producción a Excel con múltiples hojas
/// </summary>
public class ReporteTecnicoProduccionExcelService
{
    static ReporteTecnicoProduccionExcelService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    /// <summary>
    /// Genera archivo Excel con todas las hojas (Reporte Diario, Cuadro, Clasificación)
    /// </summary>
    public byte[] GenerarExcelCompleto(
        ReporteTecnicoProduccionCompletoDto? reporteDiario,
        ReporteTecnicoProduccionCuadroCompletoDto? reporteCuadro,
        ReporteClasificacionHuevoComercioCompletoDto? reporteClasificacion)
    {
        using var package = new ExcelPackage();

        // Hoja 1: Reporte Diario
        if (reporteDiario != null && reporteDiario.DatosDiarios.Any())
        {
            var wsDiario = package.Workbook.Worksheets.Add("Reporte Diario");
            EscribirReporteDiario(wsDiario, reporteDiario);
        }

        // Hoja 2: Cuadro
        if (reporteCuadro != null && reporteCuadro.DatosCuadro.Any())
        {
            var wsCuadro = package.Workbook.Worksheets.Add("Cuadro");
            EscribirCuadro(wsCuadro, reporteCuadro);
        }

        // Hoja 3: Clasificación Huevo Comercio
        if (reporteClasificacion != null && reporteClasificacion.DatosClasificacion.Any())
        {
            var wsClasificacion = package.Workbook.Worksheets.Add("Clasificación Huevo");
            EscribirClasificacionHuevo(wsClasificacion, reporteClasificacion);
        }

        return package.GetAsByteArray();
    }

    private void EscribirReporteDiario(ExcelWorksheet ws, ReporteTecnicoProduccionCompletoDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO PRODUCCIÓN SANMARINO - REPORTE DIARIO";
        ws.Cells[row, 1, row, 20].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLote(ws, reporte.LoteInfo, ref row);

        row += 2;

        // Encabezados de columnas
        var headers = new[]
        {
            "SEMANA", "FECHA", "MORTALIDAD", "", "SELECCIÓN", "", "SALDOS", "",
            "HUEVOS", "", "KILOS ALIMENTO", "", "ENVIADO PLANTA", "", "INCUBABLE",
            "CARGADO", "POLLITOS %", "VENTA HUEVO", "POLLITOS H. VENDIDO", "PESO", "% GRASA CORP"
        };

        var subHeaders = new[]
        {
            "", "", "MACHO", "HEMBRA", "MACHO", "HEMBRA", "MACHO", "HEMBRA",
            "POSTURA", "%", "MACHO", "HEMBRA", "TOTAL", "%", "", "", "Nacimientos",
            "", "", "", ""
        };

        // Escribir encabezados
        for (int col = 1; col <= headers.Length; col++)
        {
            ws.Cells[row, col].Value = headers[col - 1];
            ws.Cells[row, col].Style.Font.Bold = true;
            ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        for (int col = 1; col <= subHeaders.Length; col++)
        {
            ws.Cells[row, col].Value = subHeaders[col - 1];
            ws.Cells[row, col].Style.Font.Bold = true;
            ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        // Escribir datos
        foreach (var dato in reporte.DatosDiarios.OrderBy(d => d.Fecha))
        {
            ws.Cells[row, 1].Value = dato.Semana;
            ws.Cells[row, 2].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 3].Value = dato.MortalidadMachos;
            ws.Cells[row, 4].Value = dato.MortalidadHembras;
            ws.Cells[row, 5].Value = dato.SeleccionMachos;
            ws.Cells[row, 6].Value = dato.SeleccionHembras;
            ws.Cells[row, 7].Value = dato.SaldoMachos;
            ws.Cells[row, 8].Value = dato.SaldoHembras;
            ws.Cells[row, 9].Value = dato.HuevosTotales;
            ws.Cells[row, 10].Value = dato.PorcentajePostura;
            ws.Cells[row, 11].Value = dato.KilosAlimentoMachos;
            ws.Cells[row, 12].Value = dato.KilosAlimentoHembras;
            ws.Cells[row, 13].Value = dato.HuevosEnviadosPlanta;
            ws.Cells[row, 14].Value = dato.PorcentajeEnviadoPlanta;
            ws.Cells[row, 15].Value = dato.HuevosIncubables;
            ws.Cells[row, 16].Value = dato.HuevosCargados;
            ws.Cells[row, 17].Value = dato.PorcentajeNacimientos;
            ws.Cells[row, 18].Value = dato.VentaHuevo;
            ws.Cells[row, 19].Value = dato.PollitosVendidos;
            ws.Cells[row, 20].Value = dato.PesoHembra;
            ws.Cells[row, 21].Value = dato.PorcentajeGrasaCorporal;

            // Formato de números
            ws.Cells[row, 10].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 14].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 17].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 20].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 21].Style.Numberformat.Format = "0.00";

            row++;
        }

        // Autoajustar columnas
        ws.Cells.AutoFitColumns();
    }

    private void EscribirCuadro(ExcelWorksheet ws, ReporteTecnicoProduccionCuadroCompletoDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO PRODUCCIÓN SANMARINO - CUADRO";
        ws.Cells[row, 1, row, 30].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLote(ws, reporte.LoteInfo, ref row);

        row += 2;

        // Encabezados de columnas (simplificado para Excel)
        var headers = new[]
        {
            "SEMANA", "FECHA", "ED PrH", "AVES FIN", "", "MORTALIDAD HEMBRAS", "", "", "", "",
            "MORTALIDAD MACHOS", "", "", "", "", "PRODUCCION HUEVOS", "", "", "", "", "",
            "HUEVOS ENVIADOS PLANTA", "", "", "", "HUEVO INCUBABLE", "", "", "", "",
            "HUEVOS CARGADOS Y POLLITOS", "", "", "", "", "", "", "CONSUMO HEMBRA", "", "", "", "", "",
            "CONSUMO MACHO", "", "", "", "", "", "PESOS", "", "", "", "", "", "", ""
        };

        // Escribir encabezados (simplificado)
        ws.Cells[row, 1].Value = "SEMANA";
        ws.Cells[row, 2].Value = "FECHA";
        ws.Cells[row, 3].Value = "ED PrH";
        ws.Cells[row, 4].Value = "AVES FIN H";
        ws.Cells[row, 5].Value = "AVES FIN M";
        ws.Cells[row, 6].Value = "MORT H N";
        ws.Cells[row, 7].Value = "MORT H %";
        ws.Cells[row, 8].Value = "MORT H ACUM";
        ws.Cells[row, 9].Value = "MORT H STD";
        ws.Cells[row, 10].Value = "MORT M N";
        ws.Cells[row, 11].Value = "MORT M %";
        ws.Cells[row, 12].Value = "MORT M ACUM";
        ws.Cells[row, 13].Value = "MORT M STD";
        ws.Cells[row, 14].Value = "HUEVOS VENTA";
        ws.Cells[row, 15].Value = "HUEVOS ACUM";
        ws.Cells[row, 16].Value = "% SEM";
        ws.Cells[row, 17].Value = "% ROSS";
        ws.Cells[row, 18].Value = "TAA";
        ws.Cells[row, 19].Value = "TAA ROSS";
        ws.Cells[row, 20].Value = "ENVIADOS PLANTA";
        ws.Cells[row, 21].Value = "% ENVIA P";
        ws.Cells[row, 22].Value = "% HALA";
        ws.Cells[row, 23].Value = "HUEVOS INCUB";
        ws.Cells[row, 24].Value = "STD ROSS";
        ws.Cells[row, 25].Value = "H. CARGA";
        ws.Cells[row, 26].Value = "PAA";
        ws.Cells[row, 27].Value = "PAA ROSS";
        ws.Cells[row, 28].Value = "KG SEM H";
        ws.Cells[row, 29].Value = "ACUM H";
        ws.Cells[row, 30].Value = "ST ACUM H";
        ws.Cells[row, 31].Value = "ST GR H";
        ws.Cells[row, 32].Value = "KG SEM M";
        ws.Cells[row, 33].Value = "ACUM M";
        ws.Cells[row, 34].Value = "ST ACUM M";
        ws.Cells[row, 35].Value = "ST GR M";
        ws.Cells[row, 36].Value = "PESO H";
        ws.Cells[row, 37].Value = "PESO H STD";
        ws.Cells[row, 38].Value = "PESO M";
        ws.Cells[row, 39].Value = "PESO M STD";
        ws.Cells[row, 40].Value = "PESO HUEVO";
        ws.Cells[row, 41].Value = "PESO HUEVO STD";
        ws.Cells[row, 42].Value = "% APROV";
        ws.Cells[row, 43].Value = "% APROV STD";

        // Formato encabezados
        for (int col = 1; col <= 43; col++)
        {
            ws.Cells[row, col].Style.Font.Bold = true;
            ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        // Escribir datos
        foreach (var dato in reporte.DatosCuadro.OrderBy(d => d.Semana))
        {
            ws.Cells[row, 1].Value = dato.Semana;
            ws.Cells[row, 2].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 3].Value = dato.EdadProduccionSemanas;
            ws.Cells[row, 4].Value = dato.AvesFinHembras;
            ws.Cells[row, 5].Value = dato.AvesFinMachos;
            ws.Cells[row, 6].Value = dato.MortalidadHembrasN;
            ws.Cells[row, 7].Value = dato.MortalidadHembrasDescPorcentajeSem;
            ws.Cells[row, 8].Value = dato.MortalidadHembrasPorcentajeAcum;
            ws.Cells[row, 9].Value = dato.MortalidadHembrasStandarM;
            ws.Cells[row, 10].Value = dato.MortalidadMachosN;
            ws.Cells[row, 11].Value = dato.MortalidadMachosDescPorcentajeSem;
            ws.Cells[row, 12].Value = dato.MortalidadMachosPorcentajeAcum;
            ws.Cells[row, 13].Value = dato.MortalidadMachosStandarM;
            ws.Cells[row, 14].Value = dato.HuevosVentaSemana;
            ws.Cells[row, 15].Value = dato.HuevosAcum;
            ws.Cells[row, 16].Value = dato.PorcentajeSem;
            ws.Cells[row, 17].Value = dato.PorcentajeRoss;
            ws.Cells[row, 18].Value = dato.Taa;
            ws.Cells[row, 19].Value = dato.TaaRoss;
            ws.Cells[row, 20].Value = dato.EnviadosPlanta;
            ws.Cells[row, 21].Value = dato.PorcentajeEnviaP;
            ws.Cells[row, 22].Value = dato.PorcentajeHala;
            ws.Cells[row, 23].Value = dato.HuevosIncub;
            ws.Cells[row, 24].Value = dato.StdRoss;
            ws.Cells[row, 25].Value = dato.HCarga;
            ws.Cells[row, 26].Value = dato.Paa;
            ws.Cells[row, 27].Value = dato.PaaRoss;
            ws.Cells[row, 28].Value = dato.KgSemHembra;
            ws.Cells[row, 29].Value = dato.AcumHembra;
            ws.Cells[row, 30].Value = dato.StAcumHembra;
            ws.Cells[row, 31].Value = dato.StGrHembra;
            ws.Cells[row, 32].Value = dato.KgSemMachos;
            ws.Cells[row, 33].Value = dato.AcumMachos;
            ws.Cells[row, 34].Value = dato.StAcumMachos;
            ws.Cells[row, 35].Value = dato.StGrMachos;
            ws.Cells[row, 36].Value = dato.PesoHembraKg;
            ws.Cells[row, 37].Value = dato.PesoHembraStd;
            ws.Cells[row, 38].Value = dato.PesoMachosKg;
            ws.Cells[row, 39].Value = dato.PesoMachosStd;
            ws.Cells[row, 40].Value = dato.PesoHuevoSem;
            ws.Cells[row, 41].Value = dato.PesoHuevoStd;
            ws.Cells[row, 42].Value = dato.PorcentajeAprovSem;
            ws.Cells[row, 43].Value = dato.PorcentajeAprovStd;

            // Resaltar valores de guía genética (amarillo)
            if (dato.MortalidadHembrasStandarM.HasValue)
            {
                ws.Cells[row, 9].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 9].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.MortalidadMachosStandarM.HasValue)
            {
                ws.Cells[row, 13].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 13].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PorcentajeRoss.HasValue)
            {
                ws.Cells[row, 17].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 17].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.TaaRoss.HasValue)
            {
                ws.Cells[row, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 19].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PorcentajeHala.HasValue)
            {
                ws.Cells[row, 22].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 22].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.StdRoss.HasValue)
            {
                ws.Cells[row, 24].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 24].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PaaRoss.HasValue)
            {
                ws.Cells[row, 27].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 27].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.StAcumHembra.HasValue)
            {
                ws.Cells[row, 30].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 30].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.StGrHembra.HasValue)
            {
                ws.Cells[row, 31].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 31].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.StAcumMachos.HasValue)
            {
                ws.Cells[row, 34].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 34].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.StGrMachos.HasValue)
            {
                ws.Cells[row, 35].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 35].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PesoHembraStd.HasValue)
            {
                ws.Cells[row, 37].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 37].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PesoMachosStd.HasValue)
            {
                ws.Cells[row, 39].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 39].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PesoHuevoStd.HasValue)
            {
                ws.Cells[row, 41].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 41].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PorcentajeAprovStd.HasValue)
            {
                ws.Cells[row, 43].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 43].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }

            // Formato de números
            ws.Cells[row, 7].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 8].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 16].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 21].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 28].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 29].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 32].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 33].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 36].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 38].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 40].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 42].Style.Numberformat.Format = "0.00";

            row++;
        }

        // Autoajustar columnas
        ws.Cells.AutoFitColumns();
    }

    private void EscribirClasificacionHuevo(ExcelWorksheet ws, ReporteClasificacionHuevoComercioCompletoDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "% CLASIFICACIÓN HUEVO COMERCIO SEMANA";
        ws.Cells[row, 1, row, 22].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLote(ws, reporte.LoteInfo, ref row);

        // Escribir datos por semana
        foreach (var dato in reporte.DatosClasificacion.OrderBy(d => d.Semana))
        {
            row += 2;

            // Encabezado de semana
            ws.Cells[row, 1].Value = $"CLASIFICACIÓN HUEVO SEMANA DEL {dato.FechaInicioSemana:dd/MM/yyyy} AL {dato.FechaFinSemana:dd/MM/yyyy}";
            ws.Cells[row, 1, row, 22].Merge = true;
            ws.Cells[row, 1].Style.Font.Size = 12;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            row++;

            // Encabezados de columnas
            var headers = new[]
            {
                "LOTE", "SEMANA", "RESUMEN SEMANAL", "INCUBABLE LIMPIO",
                "HUEVO TRATADO", "% TRATADO", "HUEVO DY", "% DY",
                "HUEVO ROTO", "% ROTO", "HUEVO DEFORME", "% DEFORME",
                "HUEVO PISO", "% PISO", "HUEVO DESECHO", "% DESECHO",
                "HUEVO PIP", "% PIP", "HUEVO SUCIO DE BANDA", "% SUCIO DE BANDA",
                "TOTAL PN", "%"
            };

            for (int col = 1; col <= headers.Length; col++)
            {
                ws.Cells[row, col].Value = headers[col - 1];
                ws.Cells[row, col].Style.Font.Bold = true;
                ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                ws.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                ws.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            row++;

            // Fila de datos reales
            ws.Cells[row, 1].Value = dato.LoteNombre;
            ws.Cells[row, 2].Value = dato.Semana;
            ws.Cells[row, 3].Value = "REAL";
            ws.Cells[row, 4].Value = dato.IncubableLimpio;
            ws.Cells[row, 5].Value = dato.HuevoTratado;
            ws.Cells[row, 6].Value = dato.PorcentajeTratado;
            ws.Cells[row, 7].Value = dato.HuevoDY;
            ws.Cells[row, 8].Value = dato.PorcentajeDY;
            ws.Cells[row, 9].Value = dato.HuevoRoto;
            ws.Cells[row, 10].Value = dato.PorcentajeRoto;
            ws.Cells[row, 11].Value = dato.HuevoDeforme;
            ws.Cells[row, 12].Value = dato.PorcentajeDeforme;
            ws.Cells[row, 13].Value = dato.HuevoPiso;
            ws.Cells[row, 14].Value = dato.PorcentajePiso;
            ws.Cells[row, 15].Value = dato.HuevoDesecho;
            ws.Cells[row, 16].Value = dato.PorcentajeDesecho;
            ws.Cells[row, 17].Value = dato.HuevoPIP;
            ws.Cells[row, 18].Value = dato.PorcentajePIP;
            ws.Cells[row, 19].Value = dato.HuevoSucioDeBanda;
            ws.Cells[row, 20].Value = dato.PorcentajeSucioDeBanda;
            ws.Cells[row, 21].Value = dato.TotalPN;
            ws.Cells[row, 22].Value = dato.PorcentajeTotal;

            // Formato de porcentajes
            for (int col = 6; col <= 22; col += 2)
            {
                ws.Cells[row, col].Style.Numberformat.Format = "0.0";
            }
            row++;

            // Fila de valores de guía genética (amarillo)
            ws.Cells[row, 1].Value = dato.LoteNombre;
            ws.Cells[row, 2].Value = dato.Semana;
            ws.Cells[row, 3].Value = "GUÍA";
            ws.Cells[row, 4].Value = dato.IncubableLimpioGuia;
            ws.Cells[row, 5].Value = dato.HuevoTratadoGuia;
            ws.Cells[row, 6].Value = dato.PorcentajeTratadoGuia;
            ws.Cells[row, 7].Value = dato.HuevoDYGuia;
            ws.Cells[row, 8].Value = dato.PorcentajeDYGuia;
            ws.Cells[row, 9].Value = dato.HuevoRotoGuia;
            ws.Cells[row, 10].Value = dato.PorcentajeRotoGuia;
            ws.Cells[row, 11].Value = dato.HuevoDeformeGuia;
            ws.Cells[row, 12].Value = dato.PorcentajeDeformeGuia;
            ws.Cells[row, 13].Value = dato.HuevoPisoGuia;
            ws.Cells[row, 14].Value = dato.PorcentajePisoGuia;
            ws.Cells[row, 15].Value = dato.HuevoDesechoGuia;
            ws.Cells[row, 16].Value = dato.PorcentajeDesechoGuia;
            ws.Cells[row, 17].Value = dato.HuevoPIPGuia;
            ws.Cells[row, 18].Value = dato.PorcentajePIPGuia;
            ws.Cells[row, 19].Value = dato.HuevoSucioDeBandaGuia;
            ws.Cells[row, 20].Value = dato.PorcentajeSucioDeBandaGuia;
            ws.Cells[row, 21].Value = dato.TotalPNGuia;
            ws.Cells[row, 22].Value = dato.PorcentajeTotalGuia;

            // Resaltar toda la fila en amarillo
            for (int col = 1; col <= 22; col++)
            {
                ws.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
                ws.Cells[row, col].Style.Font.Bold = true;
            }

            // Formato de porcentajes
            for (int col = 6; col <= 22; col += 2)
            {
                ws.Cells[row, col].Style.Numberformat.Format = "0.0";
            }
            row++;
        }

        // Autoajustar columnas
        ws.Cells.AutoFitColumns();
    }

    private void EscribirInfoLote(ExcelWorksheet ws, ReporteTecnicoProduccionLoteInfoDto loteInfo, ref int row)
    {
        ws.Cells[row, 1].Value = "LOTE:";
        ws.Cells[row, 2].Value = loteInfo.LoteNombre;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 2].Style.Font.Bold = true;
        row++;

        if (!string.IsNullOrWhiteSpace(loteInfo.Raza))
        {
            ws.Cells[row, 1].Value = "RAZA:";
            ws.Cells[row, 2].Value = loteInfo.Raza;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (!string.IsNullOrWhiteSpace(loteInfo.Linea))
        {
            ws.Cells[row, 1].Value = "LÍNEA:";
            ws.Cells[row, 2].Value = loteInfo.Linea;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (!string.IsNullOrWhiteSpace(loteInfo.GranjaNombre))
        {
            ws.Cells[row, 1].Value = "GRANJA:";
            ws.Cells[row, 2].Value = loteInfo.GranjaNombre;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (loteInfo.NumeroHembrasIniciales.HasValue)
        {
            ws.Cells[row, 1].Value = "HEMBRAS INICIALES:";
            ws.Cells[row, 2].Value = loteInfo.NumeroHembrasIniciales.Value;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (loteInfo.NumeroMachosIniciales.HasValue)
        {
            ws.Cells[row, 1].Value = "MACHOS INICIALES:";
            ws.Cells[row, 2].Value = loteInfo.NumeroMachosIniciales.Value;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (loteInfo.FechaInicioProduccion.HasValue)
        {
            ws.Cells[row, 1].Value = "FECHA INICIO PRODUCCIÓN:";
            ws.Cells[row, 2].Value = loteInfo.FechaInicioProduccion.Value.ToString("dd/MM/yyyy");
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }
    }

    /// <summary>
    /// Genera nombre de archivo para el Excel
    /// </summary>
    public string GenerarNombreArchivo(ReporteTecnicoProduccionLoteInfoDto loteInfo, string tipo = "completo")
    {
        var loteNombre = loteInfo.LoteNombre?.Replace(" ", "_") ?? "Lote";
        var fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"Reporte_Tecnico_Produccion_{loteNombre}_{tipo}_{fecha}.xlsx";
    }
}
