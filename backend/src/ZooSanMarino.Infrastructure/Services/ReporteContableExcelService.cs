// src/ZooSanMarino.Infrastructure/Services/ReporteContableExcelService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para exportar reportes contables a Excel
/// </summary>
public class ReporteContableExcelService
{
    static ReporteContableExcelService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    /// <summary>
    /// Genera archivo Excel para reporte contable
    /// </summary>
    public byte[] GenerarExcel(ReporteContableCompletoDto reporte)
    {
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("REPORTE CONTABLE");

        // Configurar encabezado
        ConfigurarEncabezado(worksheet, reporte);

        // Escribir resumen semanal
        var rowInicio = EscribirResumenSemanal(worksheet, reporte, 10);

        // Escribir detalle diario por semana
        var rowActual = EscribirDetalleDiario(worksheet, reporte, rowInicio + 5);

        // Autoajustar columnas
        worksheet.Cells.AutoFitColumns();

        return package.GetAsByteArray();
    }

    private void ConfigurarEncabezado(ExcelWorksheet worksheet, ReporteContableCompletoDto reporte)
    {
        // Título principal
        worksheet.Cells[1, 1].Value = "INFORME CONTABLE";
        worksheet.Cells[1, 1].Style.Font.Size = 18;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1, 1, 8].Merge = true;
        worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        // Información del lote padre
        worksheet.Cells[2, 1].Value = "Lote Padre:";
        worksheet.Cells[2, 2].Value = reporte.LotePadreNombre;
        worksheet.Cells[2, 2].Style.Font.Bold = true;

        worksheet.Cells[3, 1].Value = "Granja:";
        worksheet.Cells[3, 2].Value = reporte.GranjaNombre;

        if (!string.IsNullOrEmpty(reporte.NucleoNombre))
        {
            worksheet.Cells[4, 1].Value = "Núcleo:";
            worksheet.Cells[4, 2].Value = reporte.NucleoNombre;
        }

        worksheet.Cells[5, 1].Value = "Fecha Primera Llegada:";
        worksheet.Cells[5, 2].Value = reporte.FechaPrimeraLlegada.ToString("dd/MM/yyyy");
        worksheet.Cells[5, 2].Style.Font.Bold = true;

        worksheet.Cells[6, 1].Value = "Semana Contable Actual:";
        worksheet.Cells[6, 2].Value = reporte.SemanaContableActual;
        worksheet.Cells[6, 2].Style.Font.Bold = true;

        worksheet.Cells[7, 1].Value = "Período Actual:";
        worksheet.Cells[7, 2].Value = $"{reporte.FechaInicioSemanaActual:dd/MM/yyyy} - {reporte.FechaFinSemanaActual:dd/MM/yyyy}";

        // Información de elaboración
        worksheet.Cells[2, 6].Value = "Elaborado por:";
        worksheet.Cells[2, 7].Value = "Líder Técnico";
        worksheet.Cells[2, 7].Style.Font.Bold = true;

        worksheet.Cells[3, 6].Value = "Enviado a:";
        worksheet.Cells[3, 7].Value = "Contabilidad";
        worksheet.Cells[3, 7].Style.Font.Bold = true;

        worksheet.Cells[4, 6].Value = "Frecuencia:";
        worksheet.Cells[4, 7].Value = "Semanal";

        worksheet.Cells[5, 6].Value = "Fecha de Generación:";
        worksheet.Cells[5, 7].Value = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
        worksheet.Cells[5, 7].Style.Font.Bold = true;

        // Sublotes incluidos
        if (reporte.ReportesSemanales.Any() && reporte.ReportesSemanales[0].Sublotes.Any())
        {
            worksheet.Cells[6, 6].Value = "Sublotes:";
            worksheet.Cells[6, 7].Value = string.Join(", ", reporte.ReportesSemanales[0].Sublotes);
        }
    }

    private int EscribirResumenSemanal(ExcelWorksheet worksheet, ReporteContableCompletoDto reporte, int rowInicio)
    {
        var row = rowInicio;

        // Título de sección
        worksheet.Cells[row, 1].Value = "RESUMEN SEMANAL DE CONSUMOS";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 8].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        row++;

        // Encabezados de columnas
        var headers = new[]
        {
            "Semana",
            "Período",
            "Alimento (kg)",
            "Agua (L)",
            "Medicamento",
            "Vacuna",
            "Otros",
            "Total General"
        };

        for (int col = 1; col <= headers.Length; col++)
        {
            worksheet.Cells[row, col].Value = headers[col - 1];
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        // Escribir datos semanales
        decimal totalAlimento = 0;
        decimal totalAgua = 0;
        decimal totalMedicamento = 0;
        decimal totalVacuna = 0;
        decimal totalOtros = 0;
        decimal totalGeneral = 0;

        foreach (var reporteSemanal in reporte.ReportesSemanales.OrderBy(r => r.SemanaContable))
        {
            worksheet.Cells[row, 1].Value = reporteSemanal.SemanaContable;
            worksheet.Cells[row, 2].Value = $"{reporteSemanal.FechaInicio:dd/MM} - {reporteSemanal.FechaFin:dd/MM}";
            worksheet.Cells[row, 3].Value = reporteSemanal.ConsumoTotalAlimento;
            worksheet.Cells[row, 4].Value = reporteSemanal.ConsumoTotalAgua;
            worksheet.Cells[row, 5].Value = reporteSemanal.ConsumoTotalMedicamento;
            worksheet.Cells[row, 6].Value = reporteSemanal.ConsumoTotalVacuna;
            worksheet.Cells[row, 7].Value = reporteSemanal.OtrosConsumos;
            worksheet.Cells[row, 8].Value = reporteSemanal.TotalGeneral;

            // Formato de números
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 8].Style.Font.Bold = true;

            // Bordes
            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            totalAlimento += reporteSemanal.ConsumoTotalAlimento;
            totalAgua += reporteSemanal.ConsumoTotalAgua;
            totalMedicamento += reporteSemanal.ConsumoTotalMedicamento;
            totalVacuna += reporteSemanal.ConsumoTotalVacuna;
            totalOtros += reporteSemanal.OtrosConsumos;
            totalGeneral += reporteSemanal.TotalGeneral;

            row++;
        }

        // Fila de totales
        worksheet.Cells[row, 1].Value = "TOTAL GENERAL";
        worksheet.Cells[row, 1, row, 2].Merge = true;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
        worksheet.Cells[row, 3].Value = totalAlimento;
        worksheet.Cells[row, 4].Value = totalAgua;
        worksheet.Cells[row, 5].Value = totalMedicamento;
        worksheet.Cells[row, 6].Value = totalVacuna;
        worksheet.Cells[row, 7].Value = totalOtros;
        worksheet.Cells[row, 8].Value = totalGeneral;

        // Formato de totales
        for (int col = 3; col <= 8; col++)
        {
            worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thick);
        }

        return row + 2;
    }

    private int EscribirDetalleDiario(ExcelWorksheet worksheet, ReporteContableCompletoDto reporte, int rowInicio)
    {
        var row = rowInicio;

        foreach (var reporteSemanal in reporte.ReportesSemanales.OrderBy(r => r.SemanaContable))
        {
            // Título de semana
            worksheet.Cells[row, 1].Value = $"DETALLE DIARIO - SEMANA {reporteSemanal.SemanaContable}";
            worksheet.Cells[row, 1].Style.Font.Size = 12;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1, row, 8].Merge = true;
            worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
            row++;

            // Encabezados de detalle diario
            var headers = new[]
            {
                "Fecha",
                "Lote",
                "Alimento (kg)",
                "Agua (L)",
                "Medicamento",
                "Vacuna",
                "Otros",
                "Total"
            };

            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cells[row, col].Value = headers[col - 1];
                worksheet.Cells[row, col].Style.Font.Bold = true;
                worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            }
            row++;

            // Escribir consumos diarios
            decimal subtotalAlimento = 0;
            decimal subtotalAgua = 0;
            decimal subtotalMedicamento = 0;
            decimal subtotalVacuna = 0;
            decimal subtotalOtros = 0;
            decimal subtotalGeneral = 0;

            foreach (var consumo in reporteSemanal.ConsumosDiarios.OrderBy(c => c.Fecha))
            {
                worksheet.Cells[row, 1].Value = consumo.Fecha.ToString("dd/MM/yyyy");
                worksheet.Cells[row, 2].Value = consumo.LoteNombre;
                worksheet.Cells[row, 3].Value = consumo.ConsumoAlimento;
                worksheet.Cells[row, 4].Value = consumo.ConsumoAgua;
                worksheet.Cells[row, 5].Value = consumo.ConsumoMedicamento;
                worksheet.Cells[row, 6].Value = consumo.ConsumoVacuna;
                worksheet.Cells[row, 7].Value = consumo.OtrosConsumos;
                worksheet.Cells[row, 8].Value = consumo.TotalConsumo;

                // Formato de números
                worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";

                // Bordes
                for (int col = 1; col <= headers.Length; col++)
                {
                    worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
                }

                subtotalAlimento += consumo.ConsumoAlimento;
                subtotalAgua += consumo.ConsumoAgua;
                subtotalMedicamento += consumo.ConsumoMedicamento;
                subtotalVacuna += consumo.ConsumoVacuna;
                subtotalOtros += consumo.OtrosConsumos;
                subtotalGeneral += consumo.TotalConsumo;

                row++;
            }

            // Subtotal de semana
            worksheet.Cells[row, 1].Value = $"Subtotal Semana {reporteSemanal.SemanaContable}";
            worksheet.Cells[row, 1, row, 2].Merge = true;
            worksheet.Cells[row, 1].Style.Font.Bold = true;
            worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
            worksheet.Cells[row, 3].Value = subtotalAlimento;
            worksheet.Cells[row, 4].Value = subtotalAgua;
            worksheet.Cells[row, 5].Value = subtotalMedicamento;
            worksheet.Cells[row, 6].Value = subtotalVacuna;
            worksheet.Cells[row, 7].Value = subtotalOtros;
            worksheet.Cells[row, 8].Value = subtotalGeneral;

            // Formato de subtotales
            for (int col = 3; col <= 8; col++)
            {
                worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
                worksheet.Cells[row, col].Style.Font.Bold = true;
                worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            row += 2; // Espacio entre semanas
        }

        return row;
    }

    /// <summary>
    /// Genera nombre de archivo para el reporte contable
    /// </summary>
    public string GenerarNombreArchivo(ReporteContableCompletoDto reporte, int? semanaContable = null)
    {
        var fecha = DateTime.Now.ToString("yyyyMMdd");
        var nombreBase = reporte.LotePadreNombre.Replace(" ", "_");
        var semana = semanaContable.HasValue ? $"Semana_{semanaContable.Value}" : "Completo";

        return $"Reporte_Contable_{nombreBase}_{semana}_{fecha}.xlsx";
    }
}

