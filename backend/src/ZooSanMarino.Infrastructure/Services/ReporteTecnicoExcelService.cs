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
}


