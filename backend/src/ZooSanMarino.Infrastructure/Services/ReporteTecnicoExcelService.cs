// src/ZooSanMarino.Infrastructure/Services/ReporteTecnicoExcelService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para exportar reportes técnicos a Excel
/// </summary>
public class ReporteTecnicoExcelService
{
    static ReporteTecnicoExcelService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    /// <summary>
    /// Genera archivo Excel para reporte técnico diario
    /// </summary>
    public byte[] GenerarExcelDiario(ReporteTecnicoCompletoDto reporte)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("DIARIO");

        // Configurar encabezado
        ConfigurarEncabezado(worksheet, reporte, "DIARIO");

        // Encabezados de columnas
        var headers = new[]
        {
            "FECHA DIARIA",
            "EDAD EN SEM.",
            "Nro. Aves Diario",
            "MORTALIDAD",
            "",
            "",
            "ERRORES DE SEXAJE",
            "",
            "",
            "DESCARTES",
            "",
            "CONSUMIDO ALIMENTO",
            "",
            "",
            "",
            "PESO CORPORAL Grs.",
            "",
            "",
            "",
            "INGRESOS ALIMENTO",
            "TRASLADOS ALIMENTO",
            "SELECCIÓN VENTAS"
        };

        var subHeaders = new[]
        {
            "",
            "",
            "",
            "TOTAL DIARIO",
            "% MORT. DIARIA",
            "% MORT. ACUM.",
            "No. AVES",
            "%",
            "% ACUM.",
            "% DIA",
            "% ACUM.",
            "CONSUMO BULTO",
            "CONSUMO KILOS",
            "ACUM KILOS",
            "GRAM. AVE",
            "ACTUAL",
            "UNIFOR.",
            "GANAN",
            "COEF.V",
            "KILOS",
            "KILOS",
            "NUMERO"
        };

        // Escribir encabezados
        var row = 8;
        for (int col = 1; col <= headers.Length; col++)
        {
            worksheet.Cells[row, col].Value = headers[col - 1];
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        row++;
        for (int col = 1; col <= subHeaders.Length; col++)
        {
            worksheet.Cells[row, col].Value = subHeaders[col - 1];
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        // Escribir datos
        row++;
        foreach (var dato in reporte.DatosDiarios.OrderBy(d => d.Fecha))
        {
            worksheet.Cells[row, 1].Value = dato.Fecha.ToString("dd-MMM-yy");
            worksheet.Cells[row, 2].Value = dato.EdadSemanas;
            worksheet.Cells[row, 3].Value = dato.NumeroAves;
            worksheet.Cells[row, 4].Value = dato.MortalidadTotal;
            worksheet.Cells[row, 5].Value = dato.MortalidadPorcentajeDiario;
            worksheet.Cells[row, 6].Value = dato.MortalidadPorcentajeAcumulado;
            worksheet.Cells[row, 7].Value = dato.ErrorSexajeNumero;
            worksheet.Cells[row, 8].Value = dato.ErrorSexajePorcentaje;
            worksheet.Cells[row, 9].Value = dato.ErrorSexajePorcentajeAcumulado;
            worksheet.Cells[row, 10].Value = dato.DescartePorcentajeDiario;
            worksheet.Cells[row, 11].Value = dato.DescartePorcentajeAcumulado;
            worksheet.Cells[row, 12].Value = dato.ConsumoBultos;
            worksheet.Cells[row, 13].Value = dato.ConsumoKilos;
            worksheet.Cells[row, 14].Value = dato.ConsumoKilosAcumulado;
            worksheet.Cells[row, 15].Value = dato.ConsumoGramosPorAve;
            worksheet.Cells[row, 16].Value = dato.PesoActual;
            worksheet.Cells[row, 17].Value = dato.Uniformidad;
            worksheet.Cells[row, 18].Value = dato.GananciaPeso;
            worksheet.Cells[row, 19].Value = dato.CoeficienteVariacion;
            worksheet.Cells[row, 20].Value = dato.IngresosAlimentoKilos;
            worksheet.Cells[row, 21].Value = dato.TrasladosAlimentoKilos;
            worksheet.Cells[row, 22].Value = dato.SeleccionVentasNumero;

            // Formato de números
            worksheet.Cells[row, 5].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 8].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 9].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 10].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 11].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 13].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 14].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 15].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 16].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 17].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 18].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 19].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 20].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 21].Style.Numberformat.Format = "0.0";

            // Resaltar mortalidad alta (amarillo)
            if (dato.MortalidadTotal > 50)
            {
                worksheet.Cells[row, 4].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 4].Style.Fill.BackgroundColor.SetColor(Color.Yellow);
            }

            // Resaltar descarte (rojo)
            if (dato.DescarteNumero > 0)
            {
                worksheet.Cells[row, 10].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, 10].Style.Fill.BackgroundColor.SetColor(Color.Red);
            }

            row++;
        }

        // Autoajustar columnas
        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    /// <summary>
    /// Genera archivo Excel para reporte técnico semanal
    /// </summary>
    public byte[] GenerarExcelSemanal(ReporteTecnicoCompletoDto reporte)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("SEMANAL");

        // Configurar encabezado
        ConfigurarEncabezado(worksheet, reporte, "SEMANAL");

        // Encabezados de columnas semanales
        var headers = new[]
        {
            "SEMANA",
            "FECHA INICIO",
            "FECHA FIN",
            "EDAD INICIO",
            "EDAD FIN",
            "AVES INICIO",
            "AVES FIN",
            "MORTALIDAD TOTAL",
            "% MORTALIDAD",
            "CONSUMO KILOS",
            "GRAMOS/AVE",
            "PESO PROMEDIO",
            "UNIFORMIDAD",
            "SELECCIÓN VENTAS",
            "INGRESOS ALIMENTO",
            "TRASLADOS ALIMENTO"
        };

        var row = 8;
        for (int col = 1; col <= headers.Length; col++)
        {
            worksheet.Cells[row, col].Value = headers[col - 1];
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        // Escribir datos semanales
        row++;
        foreach (var semana in reporte.DatosSemanales.OrderBy(s => s.Semana))
        {
            worksheet.Cells[row, 1].Value = semana.Semana;
            worksheet.Cells[row, 2].Value = semana.FechaInicio.ToString("dd-MMM-yy");
            worksheet.Cells[row, 3].Value = semana.FechaFin.ToString("dd-MMM-yy");
            worksheet.Cells[row, 4].Value = semana.EdadInicioSemanas;
            worksheet.Cells[row, 5].Value = semana.EdadFinSemanas;
            worksheet.Cells[row, 6].Value = semana.AvesInicioSemana;
            worksheet.Cells[row, 7].Value = semana.AvesFinSemana;
            worksheet.Cells[row, 8].Value = semana.MortalidadTotalSemana;
            worksheet.Cells[row, 9].Value = semana.MortalidadPorcentajeSemana;
            worksheet.Cells[row, 10].Value = semana.ConsumoKilosSemana;
            worksheet.Cells[row, 11].Value = semana.ConsumoGramosPorAveSemana;
            worksheet.Cells[row, 12].Value = semana.PesoPromedioSemana;
            worksheet.Cells[row, 13].Value = semana.UniformidadPromedioSemana;
            worksheet.Cells[row, 14].Value = semana.SeleccionVentasSemana;
            worksheet.Cells[row, 15].Value = semana.IngresosAlimentoKilosSemana;
            worksheet.Cells[row, 16].Value = semana.TrasladosAlimentoKilosSemana;

            // Formato de números
            worksheet.Cells[row, 9].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 10].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 11].Style.Numberformat.Format = "0.00";
            worksheet.Cells[row, 12].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 13].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 15].Style.Numberformat.Format = "0.0";
            worksheet.Cells[row, 16].Style.Numberformat.Format = "0.0";

            row++;
        }

        worksheet.Cells.AutoFitColumns();
        return package.GetAsByteArray();
    }

    private void ConfigurarEncabezado(ExcelWorksheet worksheet, ReporteTecnicoCompletoDto reporte, string tipoReporte)
    {
        var info = reporte.InformacionLote;

        // Título principal
        worksheet.Cells[1, 1].Value = "SANMARINO";
        worksheet.Cells[1, 1].Style.Font.Size = 16;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1, 1, 5].Merge = true;

        // Información del lote
        worksheet.Cells[2, 1].Value = "LÍNEA:";
        worksheet.Cells[2, 2].Value = info.Linea ?? "";
        worksheet.Cells[2, 4].Value = info.LoteNombre;
        worksheet.Cells[2, 5].Value = info.Sublote != null ? $"({info.Sublote})" : "";

        worksheet.Cells[3, 1].Value = "RAZA:";
        worksheet.Cells[3, 2].Value = info.Raza ?? "";
        worksheet.Cells[3, 4].Value = "NÚMERO DE HEMBRAS:";
        worksheet.Cells[3, 5].Value = info.NumeroHembras ?? 0;

        worksheet.Cells[4, 1].Value = "ETAPA:";
        worksheet.Cells[4, 2].Value = info.Etapa ?? "";
        worksheet.Cells[4, 4].Value = "ENCASETAMIENTO:";
        worksheet.Cells[4, 5].Value = info.FechaEncaset?.ToString("dd-MMM-yy") ?? "";

        worksheet.Cells[5, 4].Value = "GALPÓN:";
        worksheet.Cells[5, 5].Value = info.Galpon ?? 0;

        worksheet.Cells[6, 4].Value = "LOTE N°:";
        worksheet.Cells[6, 5].Value = info.LoteNombre;

        // Título del reporte
        worksheet.Cells[7, 1].Value = $"ETAPA DE {info.Etapa}";
        worksheet.Cells[7, 2].Value = tipoReporte;
        worksheet.Cells[7, 1, 7, 5].Merge = true;
        worksheet.Cells[7, 1].Style.Font.Bold = true;
        worksheet.Cells[7, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
    }

    /// <summary>
    /// Genera nombre de archivo para el reporte
    /// </summary>
    public string GenerarNombreArchivo(ReporteTecnicoCompletoDto reporte, string tipoReporte)
    {
        var info = reporte.InformacionLote;
        var fecha = DateTime.Now.ToString("yyyyMMdd");
        var nombreBase = info.LoteNombre.Replace(" ", "_");
        var sublote = info.Sublote != null ? $"_{info.Sublote}" : "";
        var raza = info.Raza?.Replace(" ", "_") ?? "";
        var tipo = tipoReporte.ToUpper();

        if (reporte.EsConsolidado)
        {
            return $"Lote_{nombreBase}_General_{raza}_{tipo}_{fecha}.xlsx";
        }
        else
        {
            return $"Lote_{nombreBase}{sublote}_{raza}_{tipo}_{fecha}.xlsx";
        }
    }

    /// <summary>
    /// Genera archivo Excel completo para reporte técnico de Levante con múltiples hojas
    /// Hoja 1: Diario Hembras, Hoja 2: Diario Machos, Hoja 3: Semanal Hembras, Hoja 4: Semanal Machos
    /// </summary>
    public byte[] GenerarExcelCompletoLevante(ReporteTecnicoLevanteConTabsDto reporte)
    {
        using var package = new ExcelPackage();

        // Hoja 1: Diario Hembras
        if (reporte.DatosDiariosHembras.Any())
        {
            var wsHembras = package.Workbook.Worksheets.Add("Diario Hembras");
            EscribirDiarioHembras(wsHembras, reporte);
        }

        // Hoja 2: Diario Machos
        if (reporte.DatosDiariosMachos.Any())
        {
            var wsMachos = package.Workbook.Worksheets.Add("Diario Machos");
            EscribirDiarioMachos(wsMachos, reporte);
        }

        // Hoja 3: Semanal Hembras
        if (reporte.DatosSemanales.Any())
        {
            var wsSemanalHembras = package.Workbook.Worksheets.Add("Semanal Hembras");
            EscribirSemanalHembras(wsSemanalHembras, reporte);
        }

        // Hoja 4: Semanal Machos
        if (reporte.DatosSemanales.Any())
        {
            var wsSemanalMachos = package.Workbook.Worksheets.Add("Semanal Machos");
            EscribirSemanalMachos(wsSemanalMachos, reporte);
        }

        return package.GetAsByteArray();
    }

    private void EscribirDiarioHembras(ExcelWorksheet ws, ReporteTecnicoLevanteConTabsDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO LEVANTE - DIARIO HEMBRAS";
        ws.Cells[row, 1, row, 20].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLoteLevante(ws, reporte.InformacionLote, ref row);

        row += 2;

        // Encabezados de columnas
        var headers = new[]
        {
            "FECHA", "EDAD DÍAS", "EDAD SEM.", "SALDO HEMBRAS",
            "MORTALIDAD", "", "", "",
            "SELECCIÓN", "", "", "",
            "TRASLADOS", "", "ERROR SEXAJE", "", "", "",
            "CONSUMO ALIMENTO", "", "", "PESO", "UNIF.", "CV", "GANANCIA",
            "INGRESOS ALIMENTO", "TRASLADOS ALIMENTO"
        };

        var subHeaders = new[]
        {
            "", "", "", "",
            "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "DIARIA", "ACUM.", "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "KG DIA", "KG ACUM.", "GR/AVE",
            "PROM.", "", "", "",
            "KG", "KG"
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
        foreach (var dato in reporte.DatosDiariosHembras.OrderBy(d => d.Fecha))
        {
            ws.Cells[row, 1].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 2].Value = dato.EdadDias;
            ws.Cells[row, 3].Value = dato.EdadSemanas;
            ws.Cells[row, 4].Value = dato.SaldoHembras;
            ws.Cells[row, 5].Value = dato.MortalidadHembras;
            ws.Cells[row, 6].Value = dato.MortalidadHembrasAcumulada;
            ws.Cells[row, 7].Value = dato.MortalidadHembrasPorcentajeDiario;
            ws.Cells[row, 8].Value = dato.MortalidadHembrasPorcentajeAcumulado;
            ws.Cells[row, 9].Value = dato.SeleccionHembras;
            ws.Cells[row, 10].Value = dato.SeleccionHembrasAcumulada;
            ws.Cells[row, 11].Value = dato.SeleccionHembrasPorcentajeDiario;
            ws.Cells[row, 12].Value = dato.SeleccionHembrasPorcentajeAcumulado;
            ws.Cells[row, 13].Value = dato.TrasladosHembras;
            ws.Cells[row, 14].Value = dato.TrasladosHembrasAcumulados;
            ws.Cells[row, 15].Value = dato.ErrorSexajeHembras;
            ws.Cells[row, 16].Value = dato.ErrorSexajeHembrasAcumulado;
            ws.Cells[row, 17].Value = dato.ErrorSexajeHembrasPorcentajeDiario;
            ws.Cells[row, 18].Value = dato.ErrorSexajeHembrasPorcentajeAcumulado;
            ws.Cells[row, 19].Value = dato.ConsumoKgHembras;
            ws.Cells[row, 20].Value = dato.ConsumoKgHembrasAcumulado;
            ws.Cells[row, 21].Value = dato.ConsumoGramosPorAveHembras;
            ws.Cells[row, 22].Value = dato.PesoPromedioHembras;
            ws.Cells[row, 23].Value = dato.UniformidadHembras;
            ws.Cells[row, 24].Value = dato.CoeficienteVariacionHembras;
            ws.Cells[row, 25].Value = dato.GananciaPesoHembras;
            ws.Cells[row, 26].Value = dato.IngresosAlimentoKilos;
            ws.Cells[row, 27].Value = dato.TrasladosAlimentoKilos;

            // Formato de números
            ws.Cells[row, 7].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 8].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 17].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 18].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 19].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 20].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 21].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 22].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 23].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 24].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 25].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 26].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 27].Style.Numberformat.Format = "0.0";

            row++;
        }

        ws.Cells.AutoFitColumns();
    }

    private void EscribirDiarioMachos(ExcelWorksheet ws, ReporteTecnicoLevanteConTabsDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO LEVANTE - DIARIO MACHOS";
        ws.Cells[row, 1, row, 20].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLoteLevante(ws, reporte.InformacionLote, ref row);

        row += 2;

        // Encabezados de columnas (mismo formato que hembras)
        var headers = new[]
        {
            "FECHA", "EDAD DÍAS", "EDAD SEM.", "SALDO MACHOS",
            "MORTALIDAD", "", "", "",
            "SELECCIÓN", "", "", "",
            "TRASLADOS", "", "ERROR SEXAJE", "", "", "",
            "CONSUMO ALIMENTO", "", "", "PESO", "UNIF.", "CV", "GANANCIA",
            "INGRESOS ALIMENTO", "TRASLADOS ALIMENTO"
        };

        var subHeaders = new[]
        {
            "", "", "", "",
            "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "DIARIA", "ACUM.", "DIARIA", "ACUM.", "% DIA", "% ACUM.",
            "KG DIA", "KG ACUM.", "GR/AVE",
            "PROM.", "", "", "",
            "KG", "KG"
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
        foreach (var dato in reporte.DatosDiariosMachos.OrderBy(d => d.Fecha))
        {
            ws.Cells[row, 1].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 2].Value = dato.EdadDias;
            ws.Cells[row, 3].Value = dato.EdadSemanas;
            ws.Cells[row, 4].Value = dato.SaldoMachos;
            ws.Cells[row, 5].Value = dato.MortalidadMachos;
            ws.Cells[row, 6].Value = dato.MortalidadMachosAcumulada;
            ws.Cells[row, 7].Value = dato.MortalidadMachosPorcentajeDiario;
            ws.Cells[row, 8].Value = dato.MortalidadMachosPorcentajeAcumulado;
            ws.Cells[row, 9].Value = dato.SeleccionMachos;
            ws.Cells[row, 10].Value = dato.SeleccionMachosAcumulada;
            ws.Cells[row, 11].Value = dato.SeleccionMachosPorcentajeDiario;
            ws.Cells[row, 12].Value = dato.SeleccionMachosPorcentajeAcumulado;
            ws.Cells[row, 13].Value = dato.TrasladosMachos;
            ws.Cells[row, 14].Value = dato.TrasladosMachosAcumulados;
            ws.Cells[row, 15].Value = dato.ErrorSexajeMachos;
            ws.Cells[row, 16].Value = dato.ErrorSexajeMachosAcumulado;
            ws.Cells[row, 17].Value = dato.ErrorSexajeMachosPorcentajeDiario;
            ws.Cells[row, 18].Value = dato.ErrorSexajeMachosPorcentajeAcumulado;
            ws.Cells[row, 19].Value = dato.ConsumoKgMachos;
            ws.Cells[row, 20].Value = dato.ConsumoKgMachosAcumulado;
            ws.Cells[row, 21].Value = dato.ConsumoGramosPorAveMachos;
            ws.Cells[row, 22].Value = dato.PesoPromedioMachos;
            ws.Cells[row, 23].Value = dato.UniformidadMachos;
            ws.Cells[row, 24].Value = dato.CoeficienteVariacionMachos;
            ws.Cells[row, 25].Value = dato.GananciaPesoMachos;
            ws.Cells[row, 26].Value = dato.IngresosAlimentoKilos;
            ws.Cells[row, 27].Value = dato.TrasladosAlimentoKilos;

            // Formato de números
            ws.Cells[row, 7].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 8].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 17].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 18].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 19].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 20].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 21].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 22].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 23].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 24].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 25].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 26].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 27].Style.Numberformat.Format = "0.0";

            row++;
        }

        ws.Cells.AutoFitColumns();
    }

    private void EscribirSemanalHembras(ExcelWorksheet ws, ReporteTecnicoLevanteConTabsDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO LEVANTE - SEMANAL HEMBRAS";
        ws.Cells[row, 1, row, 30].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLoteLevante(ws, reporte.InformacionLote, ref row);

        row += 2;

        // Encabezados simplificados para semanal hembras
        var headers = new[]
        {
            "SEMANA", "FECHA", "EDAD", "SALDO H", "MORT H", "% MORT H", "% MORT H GUIA",
            "SEL H", "% SEL H", "ERROR H", "% ERROR H", "CONS KG H", "CONS ACUM H",
            "GR/AVE H", "GR/AVE H GUIA", "PESO H", "PESO H GUIA", "UNIF H", "UNIF H GUIA"
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

        // Escribir datos semanales (solo columnas de hembras)
        foreach (var dato in reporte.DatosSemanales.OrderBy(d => d.Semana))
        {
            ws.Cells[row, 1].Value = dato.Semana;
            ws.Cells[row, 2].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 3].Value = dato.Edad;
            ws.Cells[row, 4].Value = dato.Hembra;
            ws.Cells[row, 5].Value = dato.MortH;
            ws.Cells[row, 6].Value = dato.PorcMortH;
            ws.Cells[row, 7].Value = dato.PorcMortHGUIA;
            ws.Cells[row, 8].Value = dato.SelH;
            ws.Cells[row, 9].Value = dato.PorcSelH;
            ws.Cells[row, 10].Value = dato.ErrorH;
            ws.Cells[row, 11].Value = dato.PorcErrH;
            ws.Cells[row, 12].Value = dato.ConsKgH;
            ws.Cells[row, 13].Value = dato.AcConsH;
            ws.Cells[row, 14].Value = dato.GrAveDiaH;
            ws.Cells[row, 15].Value = dato.GrAveDiaGUIAH;
            ws.Cells[row, 16].Value = dato.PesoH;
            ws.Cells[row, 17].Value = dato.PesoHGUIA;
            ws.Cells[row, 18].Value = dato.UniformH;
            ws.Cells[row, 19].Value = dato.UnifHGUIA;

            // Resaltar valores de guía genética (amarillo)
            if (dato.PorcMortHGUIA.HasValue)
            {
                ws.Cells[row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 7].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.GrAveDiaGUIAH.HasValue)
            {
                ws.Cells[row, 15].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 15].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PesoHGUIA.HasValue)
            {
                ws.Cells[row, 17].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 17].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.UnifHGUIA.HasValue)
            {
                ws.Cells[row, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 19].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }

            // Formato de números
            ws.Cells[row, 6].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 7].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 9].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 13].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 14].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 15].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 16].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 17].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 18].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 19].Style.Numberformat.Format = "0.0";

            row++;
        }

        ws.Cells.AutoFitColumns();
    }

    private void EscribirSemanalMachos(ExcelWorksheet ws, ReporteTecnicoLevanteConTabsDto reporte)
    {
        var row = 1;

        // Encabezado
        ws.Cells[row, 1].Value = "REPORTE TÉCNICO LEVANTE - SEMANAL MACHOS";
        ws.Cells[row, 1, row, 30].Merge = true;
        ws.Cells[row, 1].Style.Font.Size = 16;
        ws.Cells[row, 1].Style.Font.Bold = true;
        ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        row += 2;

        // Información del lote
        EscribirInfoLoteLevante(ws, reporte.InformacionLote, ref row);

        row += 2;

        // Encabezados simplificados para semanal machos
        var headers = new[]
        {
            "SEMANA", "FECHA", "EDAD", "SALDO M", "MORT M", "% MORT M", "% MORT M GUIA",
            "SEL M", "% SEL M", "ERROR M", "% ERROR M", "CONS KG M", "CONS ACUM M",
            "GR/AVE M", "GR/AVE M GUIA", "PESO M", "PESO M GUIA", "UNIF M", "UNIF M GUIA"
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

        // Escribir datos semanales (solo columnas de machos)
        foreach (var dato in reporte.DatosSemanales.OrderBy(d => d.Semana))
        {
            ws.Cells[row, 1].Value = dato.Semana;
            ws.Cells[row, 2].Value = dato.Fecha.ToString("dd/MM/yyyy");
            ws.Cells[row, 3].Value = dato.Edad;
            ws.Cells[row, 4].Value = dato.SaldoMacho;
            ws.Cells[row, 5].Value = dato.MortM;
            ws.Cells[row, 6].Value = dato.PorcMortM;
            ws.Cells[row, 7].Value = dato.PorcMortMGUIA;
            ws.Cells[row, 8].Value = dato.SelM;
            ws.Cells[row, 9].Value = dato.PorcSelM;
            ws.Cells[row, 10].Value = dato.ErrorM;
            ws.Cells[row, 11].Value = dato.PorcErrM;
            ws.Cells[row, 12].Value = dato.ConsKgM;
            ws.Cells[row, 13].Value = dato.AcConsM;
            ws.Cells[row, 14].Value = dato.GrAveDiaM;
            ws.Cells[row, 15].Value = dato.GrAveDiaMGUIA;
            ws.Cells[row, 16].Value = dato.PesoM;
            ws.Cells[row, 17].Value = dato.PesoMGUIA;
            ws.Cells[row, 18].Value = dato.UniformM;
            ws.Cells[row, 19].Value = dato.UnifMGUIA;

            // Resaltar valores de guía genética (amarillo)
            if (dato.PorcMortMGUIA.HasValue)
            {
                ws.Cells[row, 7].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 7].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.GrAveDiaMGUIA.HasValue)
            {
                ws.Cells[row, 15].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 15].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.PesoMGUIA.HasValue)
            {
                ws.Cells[row, 17].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 17].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }
            if (dato.UnifMGUIA.HasValue)
            {
                ws.Cells[row, 19].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, 19].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(255, 243, 199));
            }

            // Formato de números
            ws.Cells[row, 6].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 7].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 9].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 11].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 13].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 14].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 15].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 16].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 17].Style.Numberformat.Format = "0.00";
            ws.Cells[row, 18].Style.Numberformat.Format = "0.0";
            ws.Cells[row, 19].Style.Numberformat.Format = "0.0";

            row++;
        }

        ws.Cells.AutoFitColumns();
    }

    private void EscribirInfoLoteLevante(ExcelWorksheet ws, ReporteTecnicoLoteInfoDto loteInfo, ref int row)
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

        if (loteInfo.NumeroHembras.HasValue)
        {
            ws.Cells[row, 1].Value = "HEMBRAS INICIALES:";
            ws.Cells[row, 2].Value = loteInfo.NumeroHembras.Value;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (loteInfo.NumeroMachos.HasValue)
        {
            ws.Cells[row, 1].Value = "MACHOS INICIALES:";
            ws.Cells[row, 2].Value = loteInfo.NumeroMachos.Value;
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }

        if (loteInfo.FechaEncaset.HasValue)
        {
            ws.Cells[row, 1].Value = "FECHA ENCASET:";
            ws.Cells[row, 2].Value = loteInfo.FechaEncaset.Value.ToString("dd/MM/yyyy");
            ws.Cells[row, 1].Style.Font.Bold = true;
            row++;
        }
    }

    /// <summary>
    /// Genera nombre de archivo para reporte de Levante con tabs
    /// </summary>
    public string GenerarNombreArchivoLevante(ReporteTecnicoLevanteConTabsDto reporte, string tipo = "completo")
    {
        var loteNombre = reporte.InformacionLote.LoteNombre?.Replace(" ", "_") ?? "Lote";
        var fecha = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"Reporte_Tecnico_Levante_{loteNombre}_{tipo}_{fecha}.xlsx";
    }
}


