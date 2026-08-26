// src/ZooSanMarino.Infrastructure/Services/ReporteContableExcelService.cs
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Servicio para exportar reportes contables a Excel
/// </summary>
public class ReporteContableExcelService
{
    static ReporteContableExcelService()
    {
        ExcelPackage.License.SetNonCommercialPersonal("ItalGranja");
    }

    /// <summary>
    /// Genera archivo Excel para reporte contable con una hoja por semana.
    /// <paramref name="movimientosHuevos"/> es opcional: cuando llega con filas se agrega la hoja
    /// MOVIMIENTOS HUEVOS después del RESUMEN. En Levante no hay postura, así que el libro sale
    /// igual que sin el parámetro.
    /// </summary>
    public byte[] GenerarExcel(
        ReporteContableCompletoDto reporte,
        ReporteMovimientosHuevosDto? movimientosHuevos = null)
    {
        using var package = new ExcelPackage();

        // Crear hoja de resumen
        var hojaResumen = package.Workbook.Worksheets.Add("RESUMEN");
        ConfigurarEncabezado(hojaResumen, reporte);
        var rowInicio = EscribirResumenSemanal(hojaResumen, reporte, 10);
        hojaResumen.Cells.AutoFitColumns();

        // Movimientos de huevo (solo si el lote tiene postura en el período)
        if (movimientosHuevos is not null && movimientosHuevos.MovimientosDiarios.Count > 0)
        {
            var hojaHuevos = package.Workbook.Worksheets.Add("MOVIMIENTOS HUEVOS");
            EscribirMovimientosHuevos(hojaHuevos, reporte, movimientosHuevos);
            hojaHuevos.Cells.AutoFitColumns();
        }

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

    /// <summary>
    /// Columnas de la hoja RESUMEN, en orden. Declararlas acá (en vez de indexar a mano) mantiene
    /// alineados el encabezado, la fila de datos, los formatos y la fila de totales: agregar una
    /// columna es agregar una entrada, no reindexar cuatro bloques.
    /// </summary>
    private static readonly (string Titulo, string Formato,
        Func<ReporteContableResumenCalculos.FilaResumen, object?> Valor)[] ColumnasResumen =
    {
        ("Mortalidad",    "#,##0",    f => f.Mortalidad),
        ("Selección",     "#,##0",    f => f.Seleccion),
        ("Traslados",     "#,##0",    f => f.Traslados),
        ("Ventas",        "#,##0",    f => f.Ventas),
        ("Alimento (kg)", "#,##0.00", f => f.Alimento),
        ("Agua (L)",      "#,##0.00", f => f.Agua),
        ("Medicamento",   "#,##0.00", f => f.Medicamento),
        ("Vacuna",        "#,##0.00", f => f.Vacuna),
        ("Otros",         "#,##0.00", f => f.Otros),
        ("Total General", "#,##0.00", f => f.TotalGeneral)
    };

    private int EscribirResumenSemanal(ExcelWorksheet worksheet, ReporteContableCompletoDto reporte, int rowInicio)
    {
        var row = rowInicio;

        // "Semana" y "Período" van fijas al frente; el resto sale de ColumnasResumen
        const int colSemana = 1;
        const int colPeriodo = 2;
        const int primeraColumnaDato = 3;
        var ultimaColumna = primeraColumnaDato + ColumnasResumen.Length - 1;
        var colTotalGeneral = ultimaColumna;

        // Título de sección
        worksheet.Cells[row, 1].Value = "RESUMEN SEMANAL";
        worksheet.Cells[row, 1].Style.Font.Size = 14;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1, row, ultimaColumna].Merge = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
        row++;

        // Encabezados de columnas
        worksheet.Cells[row, colSemana].Value = "Semana";
        worksheet.Cells[row, colPeriodo].Value = "Período";
        for (int i = 0; i < ColumnasResumen.Length; i++)
        {
            worksheet.Cells[row, primeraColumnaDato + i].Value = ColumnasResumen[i].Titulo;
        }
        for (int col = 1; col <= ultimaColumna; col++)
        {
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        // Escribir datos semanales
        var filas = ReporteContableResumenCalculos.Filas(reporte.ReportesSemanales);

        foreach (var fila in filas)
        {
            worksheet.Cells[row, colSemana].Value = fila.Semana;
            worksheet.Cells[row, colPeriodo].Value = $"{fila.FechaInicio:dd/MM} - {fila.FechaFin:dd/MM}";

            for (int i = 0; i < ColumnasResumen.Length; i++)
            {
                var col = primeraColumnaDato + i;
                worksheet.Cells[row, col].Value = ColumnasResumen[i].Valor(fila);
                worksheet.Cells[row, col].Style.Numberformat.Format = ColumnasResumen[i].Formato;
            }
            worksheet.Cells[row, colTotalGeneral].Style.Font.Bold = true;

            // Bordes
            for (int col = 1; col <= ultimaColumna; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }

            row++;
        }

        // Fila de totales
        var total = ReporteContableResumenCalculos.Total(filas);

        worksheet.Cells[row, 1].Value = "TOTAL GENERAL";
        worksheet.Cells[row, colSemana, row, colPeriodo].Merge = true;
        worksheet.Cells[row, 1].Style.Font.Bold = true;
        worksheet.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

        for (int i = 0; i < ColumnasResumen.Length; i++)
        {
            var col = primeraColumnaDato + i;
            worksheet.Cells[row, col].Value = ColumnasResumen[i].Valor(total);
            worksheet.Cells[row, col].Style.Numberformat.Format = ColumnasResumen[i].Formato;
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thick);
        }

        return row + 2;
    }

    /// <summary>
    /// Columnas de la hoja MOVIMIENTOS HUEVOS, espejo de la pestaña «Movimientos de Huevos» de la
    /// pantalla: el Excel y la tabla tienen que decir lo mismo. <c>Total</c> es el acumulado que ya
    /// trae el DTO — el Excel no recalcula.
    /// </summary>
    /// <summary>
    /// <c>OcultaSiClasificaPorItems</c>: columnas que salen de <c>huevo_inc</c>/las 11 legacy fijas
    /// de <c>seguimiento_diario_produccion</c> (HVTO FÉRTIL/HVO COMERCIAL/HUEVO DESECHO) o de
    /// <c>cantidad_desecho</c> de <c>traslado_huevos</c> (DESCARTE) -- ambas quedan siempre en 0
    /// para empresas con <c>clasificacion_huevo_por_items</c>, así que se ocultan.
    /// </summary>
    private static readonly (string Grupo, string Titulo,
        Func<MovimientoHuevoDiarioDto, int> Valor,
        Func<ReporteMovimientosHuevosDto, int> Total,
        bool OcultaSiClasificaPorItems)[] ColumnasHuevos =
    {
        ("PRODUCCIÓN", "POSTURA",           d => d.Postura,          r => r.TotalPostura,          false),
        ("PRODUCCIÓN", "HVTO FÉRTIL",       d => d.HvtoFertil,       r => r.TotalHvtoFertil,        true),
        ("PRODUCCIÓN", "HVO COMERCIAL",     d => d.HvoComercial,     r => r.TotalHvoComercial,      true),
        ("PRODUCCIÓN", "HUEVO DESECHO",     d => d.HuevoDesecho,     r => r.TotalHuevoDesecho,      true),
        ("MOVIMIENTOS", "ENTRADA",          d => d.Entrada,          r => r.TotalEntrada,           false),
        ("MOVIMIENTOS", "CAPTURA INFO",     d => d.CapturaInfo,      r => r.MovimientosDiarios.Sum(x => x.CapturaInfo), false),
        ("MOVIMIENTOS", "VENTA",            d => d.Venta,            r => r.TotalVenta,             false),
        ("MOVIMIENTOS", "SALIDA",           d => d.Salida,           r => r.TotalSalida,            false),
        ("MOVIMIENTOS", "TRASLADO A PLANTA", d => d.TrasladoAPlanta, r => r.TotalTrasladoAPlanta,   false),
        ("MOVIMIENTOS", "DESCARTE",         d => d.Descarte,         r => r.TotalDescarte,          true)
    };

    /// <summary>
    /// Escribe la hoja de movimientos de huevo: encabezado del lote, una fila por día y lote, y la
    /// fila de totales del reporte.
    /// </summary>
    private void EscribirMovimientosHuevos(
        ExcelWorksheet worksheet,
        ReporteContableCompletoDto reporte,
        ReporteMovimientosHuevosDto huevos)
    {
        const int colDia = 1;
        const int colFecha = 2;
        const int colLote = 3;
        const int primeraColumnaDato = 4;

        // Empresas que clasifican por ITEM del catalogo (flag clasificacion_huevo_por_items): las
        // columnas marcadas OcultaSiClasificaPorItems salen de columnas legacy fijas, siempre en 0
        // -- se filtran ACA, una sola vez, y el resto del metodo usa "columnas" en vez del campo
        // estatico. Alinea automaticamente cabecera de grupo, cabecera de columna, filas de dato y
        // fila de totales: no hay 4 bloques que reindexar a mano.
        var columnas = huevos.ClasificacionHuevoPorItems
            ? ColumnasHuevos.Where(c => !c.OcultaSiClasificaPorItems).ToArray()
            : ColumnasHuevos;
        var ultimaColumna = primeraColumnaDato + columnas.Length - 1;

        // Encabezado
        worksheet.Cells[1, 1].Value = "MOVIMIENTOS DE HUEVOS";
        worksheet.Cells[1, 1].Style.Font.Size = 18;
        worksheet.Cells[1, 1].Style.Font.Bold = true;
        worksheet.Cells[1, 1, 1, ultimaColumna].Merge = true;
        worksheet.Cells[1, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

        worksheet.Cells[2, 1].Value = "Lote Padre:";
        worksheet.Cells[2, 2].Value = huevos.LotePadreNombre;
        worksheet.Cells[2, 2].Style.Font.Bold = true;

        worksheet.Cells[3, 1].Value = "Granja:";
        worksheet.Cells[3, 2].Value = reporte.GranjaNombre;

        if (huevos.FechaInicio.HasValue && huevos.FechaFin.HasValue)
        {
            worksheet.Cells[4, 1].Value = "Período:";
            worksheet.Cells[4, 2].Value = $"{huevos.FechaInicio:dd/MM/yyyy} - {huevos.FechaFin:dd/MM/yyyy}";
        }

        var row = 6;

        // Fila de grupos (PRODUCCIÓN / MOVIMIENTOS) sobre sus columnas
        var inicioGrupo = primeraColumnaDato;
        for (int i = 0; i < columnas.Length; i++)
        {
            var esUltima = i == columnas.Length - 1;
            var cambiaGrupo = esUltima || columnas[i + 1].Grupo != columnas[i].Grupo;
            if (!cambiaGrupo) continue;

            var finGrupo = primeraColumnaDato + i;
            worksheet.Cells[row, inicioGrupo].Value = columnas[i].Grupo;
            worksheet.Cells[row, inicioGrupo, row, finGrupo].Merge = true;
            worksheet.Cells[row, inicioGrupo].Style.Font.Bold = true;
            worksheet.Cells[row, inicioGrupo].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            worksheet.Cells[row, inicioGrupo].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, inicioGrupo].Style.Fill.BackgroundColor.SetColor(Color.LightBlue);
            worksheet.Cells[row, inicioGrupo].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            inicioGrupo = finGrupo + 1;
        }
        row++;

        // Encabezados de columnas
        worksheet.Cells[row, colDia].Value = "Día";
        worksheet.Cells[row, colFecha].Value = "Fecha";
        worksheet.Cells[row, colLote].Value = "Lote";
        for (int i = 0; i < columnas.Length; i++)
        {
            worksheet.Cells[row, primeraColumnaDato + i].Value = columnas[i].Titulo;
        }
        for (int col = 1; col <= ultimaColumna; col++)
        {
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            worksheet.Cells[row, col].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        row++;

        // Detalle diario
        var dia = 1;
        foreach (var mov in huevos.MovimientosDiarios)
        {
            worksheet.Cells[row, colDia].Value = dia++;
            worksheet.Cells[row, colFecha].Value = mov.Fecha.ToString("dd/MM/yyyy");
            worksheet.Cells[row, colLote].Value = mov.LoteNombre;

            for (int i = 0; i < columnas.Length; i++)
            {
                var col = primeraColumnaDato + i;
                worksheet.Cells[row, col].Value = columnas[i].Valor(mov);
                worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0";
            }

            for (int col = 1; col <= ultimaColumna; col++)
            {
                worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thin);
            }
            row++;
        }

        // Totales
        worksheet.Cells[row, colDia].Value = "TOTALES";
        worksheet.Cells[row, colDia, row, colLote].Merge = true;
        worksheet.Cells[row, colDia].Style.Font.Bold = true;
        worksheet.Cells[row, colDia].Style.Fill.PatternType = ExcelFillStyle.Solid;
        worksheet.Cells[row, colDia].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);

        for (int i = 0; i < columnas.Length; i++)
        {
            var col = primeraColumnaDato + i;
            worksheet.Cells[row, col].Value = columnas[i].Total(huevos);
            worksheet.Cells[row, col].Style.Numberformat.Format = "#,##0";
            worksheet.Cells[row, col].Style.Font.Bold = true;
            worksheet.Cells[row, col].Style.Fill.PatternType = ExcelFillStyle.Solid;
            worksheet.Cells[row, col].Style.Fill.BackgroundColor.SetColor(Color.LightGreen);
            worksheet.Cells[row, col].Style.Border.BorderAround(ExcelBorderStyle.Thick);
        }
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

