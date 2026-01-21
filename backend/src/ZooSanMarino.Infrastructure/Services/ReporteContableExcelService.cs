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
    /// Genera archivo Excel para reporte contable con una hoja por semana
    /// </summary>
    public byte[] GenerarExcel(ReporteContableCompletoDto reporte)
    {
        using var package = new ExcelPackage();
        
        // Crear hoja de resumen
        var hojaResumen = package.Workbook.Worksheets.Add("RESUMEN");
        ConfigurarEncabezado(hojaResumen, reporte);
        var rowInicio = EscribirResumenSemanal(hojaResumen, reporte, 10);
        hojaResumen.Cells.AutoFitColumns();

        // Crear una hoja por cada semana
        foreach (var reporteSemanal in reporte.ReportesSemanales.OrderBy(r => r.SemanaContable))
        {
            // Crear nombre de hoja con semana y fechas
            var fechaInicio = reporteSemanal.FechaInicio.ToString("dd/MM");
            var fechaFin = reporteSemanal.FechaFin.ToString("dd/MM");
            var nombreHoja = $"Sem {reporteSemanal.SemanaContable} ({fechaInicio}-{fechaFin})";
            
            // Limitar nombre de hoja a 31 caracteres (límite de Excel)
            if (nombreHoja.Length > 31)
            {
                // Si es muy largo, usar formato más corto
                nombreHoja = $"S{reporteSemanal.SemanaContable} ({fechaInicio}-{fechaFin})";
                if (nombreHoja.Length > 31)
                {
                    // Si aún es muy largo, truncar fechas
                    nombreHoja = $"S{reporteSemanal.SemanaContable} ({fechaInicio.Substring(0, 2)}-{fechaFin})";
                    if (nombreHoja.Length > 31)
                    {
                        nombreHoja = nombreHoja.Substring(0, 31);
                    }
                }
            }
            
            var worksheet = package.Workbook.Worksheets.Add(nombreHoja);
            
            // Configurar encabezado para esta semana
            ConfigurarEncabezadoSemana(worksheet, reporte, reporteSemanal);
            
            // Escribir datos de la semana
            EscribirDatosSemana(worksheet, reporteSemanal, 10);
            
            // Autoajustar columnas
            worksheet.Cells.AutoFitColumns();
        }

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
        worksheet.Cells[row, 1].Value = "RESUMEN SEMANAL";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 13].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        row++;

        // Encabezados de columnas
        var headers = new[]
        {
            "Semana",
            "Período",
            "Mortalidad",
            "Traslados",
            "Ventas",
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
        int totalMortalidad = 0;
        int totalTraslados = 0;
        int totalVentas = 0;
        decimal totalAlimento = 0;
        decimal totalAgua = 0;
        decimal totalMedicamento = 0;
        decimal totalVacuna = 0;
        decimal totalOtros = 0;
        decimal totalGeneral = 0;

        foreach (var reporteSemanal in reporte.ReportesSemanales.OrderBy(r => r.SemanaContable))
        {
            // Calcular totales de aves
            var mortalidadTotal = reporteSemanal.MortalidadHembrasSemanal + reporteSemanal.MortalidadMachosSemanal;
            var trasladosTotal = reporteSemanal.TrasladosHembrasSemanal + reporteSemanal.TrasladosMachosSemanal;
            var ventasTotal = reporteSemanal.VentasHembrasSemanal + reporteSemanal.VentasMachosSemanal;

            worksheet.Cells[row, 1].Value = reporteSemanal.SemanaContable;
            worksheet.Cells[row, 2].Value = $"{reporteSemanal.FechaInicio:dd/MM} - {reporteSemanal.FechaFin:dd/MM}";
            worksheet.Cells[row, 3].Value = mortalidadTotal;
            worksheet.Cells[row, 4].Value = trasladosTotal;
            worksheet.Cells[row, 5].Value = ventasTotal;
            worksheet.Cells[row, 6].Value = reporteSemanal.ConsumoTotalAlimento;
            worksheet.Cells[row, 7].Value = reporteSemanal.ConsumoTotalAgua;
            worksheet.Cells[row, 8].Value = reporteSemanal.ConsumoTotalMedicamento;
            worksheet.Cells[row, 9].Value = reporteSemanal.ConsumoTotalVacuna;
            worksheet.Cells[row, 10].Value = reporteSemanal.OtrosConsumos;
            worksheet.Cells[row, 11].Value = reporteSemanal.TotalGeneral;

            // Formato de números (aves como enteros, consumos como decimales)
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, 6].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 7].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 8].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 9].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 10].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 11].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, 11].Style.Font.Bold = true;

            // Bordes
            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            totalMortalidad += mortalidadTotal;
            totalTraslados += trasladosTotal;
            totalVentas += ventasTotal;
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
        worksheet.Cells[row, 3].Value = totalMortalidad;
        worksheet.Cells[row, 4].Value = totalTraslados;
        worksheet.Cells[row, 5].Value = totalVentas;
        worksheet.Cells[row, 6].Value = totalAlimento;
        worksheet.Cells[row, 7].Value = totalAgua;
        worksheet.Cells[row, 8].Value = totalMedicamento;
        worksheet.Cells[row, 9].Value = totalVacuna;
        worksheet.Cells[row, 10].Value = totalOtros;
        worksheet.Cells[row, 11].Value = totalGeneral;

        // Formato de totales
        worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
        worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";
        worksheet.Cells[row, 5].Style.Numberformat.Format = "#,##0";
        for (int col = 6; col <= 11; col++)
        {
            worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
        }
        
        for (int col = 3; col <= 11; col++)
        {
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thick);
        }

        return row + 2;
    }

    /// <summary>
    /// Configura el encabezado para una hoja de semana específica
    /// </summary>
    private void ConfigurarEncabezadoSemana(ExcelWorksheet worksheet, ReporteContableCompletoDto reporte, ReporteContableSemanalDto reporteSemanal)
    {
        // Título principal
        worksheet.Cells[1, 1].Value = $"INFORME CONTABLE - SEMANA {reporteSemanal.SemanaContable}";
        worksheet.Cells[1, 1].Style.Font.Size = 18;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1, 1, 10].Merge = true;
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

        worksheet.Cells[5, 1].Value = "Período:";
        worksheet.Cells[5, 2].Value = $"{reporteSemanal.FechaInicio:dd/MM/yyyy} - {reporteSemanal.FechaFin:dd/MM/yyyy}";
        worksheet.Cells[5, 2].Style.Font.Bold = true;

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
        if (reporteSemanal.Sublotes.Any())
        {
            worksheet.Cells[6, 1].Value = "Sublotes:";
            worksheet.Cells[6, 2].Value = string.Join(", ", reporteSemanal.Sublotes);
        }
    }

    /// <summary>
    /// Escribe todos los datos de una semana en una hoja
    /// </summary>
    private void EscribirDatosSemana(ExcelWorksheet worksheet, ReporteContableSemanalDto reporteSemanal, int rowInicio)
    {
        var row = rowInicio;

        // Sección AVES
        row = EscribirSeccionAves(worksheet, reporteSemanal, row);

        // Sección BULTO
        row = EscribirSeccionBultos(worksheet, reporteSemanal, row + 2);

        // Sección INICIO
        if (reporteSemanal.SeccionInicio != null)
        {
            row = EscribirSeccionBultosInicioLevante(worksheet, reporteSemanal.SeccionInicio, "INICIO", row + 2);
        }

        // Sección LEVANTE
        if (reporteSemanal.SeccionLevante != null)
        {
            row = EscribirSeccionBultosInicioLevante(worksheet, reporteSemanal.SeccionLevante, "LEVANTE", row + 2);
        }

        // Sección Consumos Diarios
        row = EscribirConsumosDiariosSemana(worksheet, reporteSemanal, row + 2);
    }

    /// <summary>
    /// Escribe la sección de AVES
    /// </summary>
    private int EscribirSeccionAves(ExcelWorksheet worksheet, ReporteContableSemanalDto reporteSemanal, int rowInicio)
    {
        var row = rowInicio;

        // Título
        worksheet.Cells[row, 1].Value = "AVES";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 10].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        row++;

        // Encabezados
        var headers = new[] { "Concepto", "Hembras", "Machos", "Total" };
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

        // Datos
        var datosAves = new[]
        {
            ("Saldo Anterior", reporteSemanal.SaldoAnteriorHembras, reporteSemanal.SaldoAnteriorMachos),
            ("Entradas", reporteSemanal.EntradasHembras, reporteSemanal.EntradasMachos),
            ("Mortalidad", reporteSemanal.MortalidadHembrasSemanal, reporteSemanal.MortalidadMachosSemanal),
            ("Selección", reporteSemanal.SeleccionHembrasSemanal, reporteSemanal.SeleccionMachosSemanal),
            ("Ventas", reporteSemanal.VentasHembrasSemanal, reporteSemanal.VentasMachosSemanal),
            ("Traslados", reporteSemanal.TrasladosHembrasSemanal, reporteSemanal.TrasladosMachosSemanal),
            ("Saldo Final", reporteSemanal.SaldoFinHembras, reporteSemanal.SaldoFinMachos)
        };

        foreach (var (concepto, hembras, machos) in datosAves)
        {
            worksheet.Cells[row, 1].Value = concepto;
            worksheet.Cells[row, 2].Value = hembras;
            worksheet.Cells[row, 3].Value = machos;
            worksheet.Cells[row, 4].Value = hembras + machos;

            worksheet.Cells[row, 2].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, 3].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, 4].Style.Numberformat.Format = "#,##0";

            if (concepto == "Saldo Final")
            {
                worksheet.Cells[row, 1].Style.Font.Bold = true;
                worksheet.Cells[row, 2].Style.Font.Bold = true;
                worksheet.Cells[row, 3].Style.Font.Bold = true;
                worksheet.Cells[row, 4].Style.Font.Bold = true;
            }

            for (int col = 1; col <= headers.Length; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
            row++;
        }

        return row;
    }

    /// <summary>
    /// Escribe la sección de BULTO
    /// </summary>
    private int EscribirSeccionBultos(ExcelWorksheet worksheet, ReporteContableSemanalDto reporteSemanal, int rowInicio)
    {
        var row = rowInicio;

        // Título
        worksheet.Cells[row, 1].Value = "BULTO";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 8].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
        row++;

        // Encabezados
        var headers = new[] { "Concepto", "Saldo Ant.", "Traslados", "Entradas", "Retiros", "Consumo H", "Consumo M", "Saldo Final" };
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

        // Datos
        worksheet.Cells[row, 1].Value = "Totales";
        worksheet.Cells[row, 2].Value = reporteSemanal.SaldoBultosAnterior;
        worksheet.Cells[row, 3].Value = reporteSemanal.TrasladosBultosSemanal;
        worksheet.Cells[row, 4].Value = reporteSemanal.EntradasBultosSemanal;
        worksheet.Cells[row, 5].Value = reporteSemanal.RetirosBultosSemanal;
        worksheet.Cells[row, 6].Value = reporteSemanal.ConsumoBultosHembrasSemanal;
        worksheet.Cells[row, 7].Value = reporteSemanal.ConsumoBultosMachosSemanal;
        worksheet.Cells[row, 8].Value = reporteSemanal.SaldoBultosFinal;

        for (int col = 2; col <= 8; col++)
        {
            worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, col].Style.Font.Bold = true;
        }

        for (int col = 1; col <= headers.Length; col++)
        {
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        return row + 1;
    }

    /// <summary>
    /// Escribe la sección de BULTO para INICIO o LEVANTE
    /// </summary>
    private int EscribirSeccionBultosInicioLevante(ExcelWorksheet worksheet, SeccionReporteContableDto seccion, string tipoSeccion, int rowInicio)
    {
        var row = rowInicio;

        // Título
        worksheet.Cells[row, 1].Value = $"BULTO / {tipoSeccion}";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 8].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightYellow);
        row++;

        // Período
        worksheet.Cells[row, 1].Value = $"Período: {seccion.FechaInicio:dd/MM/yyyy} - {seccion.FechaFin:dd/MM/yyyy}";
        worksheet.Cells[row, 1, row, 8].Merge = true;
        worksheet.Cells[row, 1].Style.Font.Italic = true;
        row++;

        // Encabezados
        var headers = new[] { "Concepto", "Saldo Ant.", "Traslados", "Entradas", "Producto - H", "Producto - M", "Saldo Final" };
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

        // Datos
        worksheet.Cells[row, 1].Value = "Totales";
        worksheet.Cells[row, 2].Value = seccion.SaldoBultosAnterior;
        worksheet.Cells[row, 3].Value = seccion.TrasladosBultos;
        worksheet.Cells[row, 4].Value = seccion.EntradasBultos;
        worksheet.Cells[row, 5].Value = seccion.ConsumoBultosHembras;
        worksheet.Cells[row, 6].Value = seccion.ConsumoBultosMachos;
        worksheet.Cells[row, 7].Value = seccion.SaldoBultosFinal;

        for (int col = 2; col <= 7; col++)
        {
            worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0.00";
            worksheet.Cells[row, col].Style.Font.Bold = true;
        }

        for (int col = 1; col <= headers.Length; col++)
        {
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        return row + 1;
    }

    /// <summary>
    /// Escribe los consumos diarios de una semana
    /// </summary>
    private int EscribirConsumosDiariosSemana(ExcelWorksheet worksheet, ReporteContableSemanalDto reporteSemanal, int rowInicio)
    {
        var row = rowInicio;

        // Título
        worksheet.Cells[row, 1].Value = "CONSUMOS DIARIOS (Kg)";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, 8].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightCyan);
        row++;

        // Encabezados
        var headers = new[] { "Fecha", "Lote", "Alimento (kg)", "Agua (L)", "Medicamento", "Vacuna", "Otros", "Total" };
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

        // Subtotal
        worksheet.Cells[row, 1].Value = "Subtotal";
        worksheet.Cells[row, 1, row, 2].Merge = true;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
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
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
        }

        return row + 1;
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

