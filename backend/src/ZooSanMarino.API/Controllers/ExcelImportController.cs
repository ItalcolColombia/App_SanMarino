// src/ZooSanMarino.API/Controllers/ExcelImportController.cs
using Microsoft.AspNetCore.Mvc;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Tags("ExcelImport")]
public class ExcelImportController : ControllerBase
{
    private readonly IExcelImportService _excelImportService;
    private readonly ILogger<ExcelImportController> _logger;

    public ExcelImportController(
        IExcelImportService excelImportService,
        ILogger<ExcelImportController> logger)
    {
        _excelImportService = excelImportService;
        _logger = logger;
    }

    /// <summary>
    /// Importa datos de producción avícola desde un archivo Excel
    /// </summary>
    /// <param name="file">Archivo Excel (.xlsx o .xls) con los datos de producción</param>
    /// <returns>Resultado de la importación con estadísticas y errores</returns>
    [HttpPost("produccion-avicola")]
    [ProducesResponseType(typeof(ExcelImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<ActionResult<ExcelImportResultDto>> ImportProduccionAvicola(IFormFile file)
    {
        try
        {
            if (file == null)
            {
                return BadRequest(new { message = "No se ha proporcionado ningún archivo." });
            }

            _logger.LogInformation("Iniciando importación de archivo Excel: {FileName}, Tamaño: {FileSize} bytes", 
                file.FileName, file.Length);

            var result = await _excelImportService.ImportProduccionAvicolaFromExcelAsync(file);

            if (result.Success)
            {
                _logger.LogInformation("Importación completada exitosamente. Procesadas: {ProcessedRows}, Errores: {ErrorRows}", 
                    result.ProcessedRows, result.ErrorRows);
            }
            else
            {
                _logger.LogWarning("Importación completada con errores. Procesadas: {ProcessedRows}, Errores: {ErrorRows}", 
                    result.ProcessedRows, result.ErrorRows);
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la importación del archivo Excel: {FileName}", file?.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno durante la importación del archivo." });
        }
    }

    /// <summary>
    /// Valida un archivo Excel sin importar los datos
    /// </summary>
    /// <param name="file">Archivo Excel para validar</param>
    /// <returns>Lista de datos válidos encontrados en el archivo</returns>
    [HttpPost("validate-produccion-avicola")]
    [ProducesResponseType(typeof(List<ProduccionAvicolaRawDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [RequestSizeLimit(10 * 1024 * 1024)] // 10 MB
    public async Task<ActionResult<List<ProduccionAvicolaRawDto>>> ValidateProduccionAvicola(IFormFile file)
    {
        try
        {
            if (file == null)
            {
                return BadRequest(new { message = "No se ha proporcionado ningún archivo." });
            }

            _logger.LogInformation("Validando archivo Excel: {FileName}", file.FileName);

            var validData = await _excelImportService.ValidateExcelDataAsync(file);

            _logger.LogInformation("Validación completada. Registros válidos encontrados: {ValidCount}", validData.Count);

            return Ok(validData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error durante la validación del archivo Excel: {FileName}", file?.FileName);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error interno durante la validación del archivo." });
        }
    }

    /// <summary>
    /// Obtiene información sobre el formato esperado del archivo Excel
    /// </summary>
    /// <returns>Información sobre las columnas esperadas y su formato</returns>
    [HttpGet("template-info")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetTemplateInfo()
    {
        var templateInfo = new
        {
            description = "Información sobre el formato esperado del archivo Excel para importación de datos de producción avícola",
            fileFormats = new[] { ".xlsx", ".xls" },
            maxFileSize = "10 MB",
            requiredStructure = new
            {
                firstRow = "Debe contener los encabezados de las columnas",
                dataRows = "Filas 2 en adelante deben contener los datos"
            },
            supportedColumns = new[]
            {
                new { excelName = "anio_guia", description = "Año de la guía de producción", example = "2024" },
                new { excelName = "raza", description = "Raza de las aves", example = "Cobb 500" },
                new { excelName = "edad", description = "Edad de las aves", example = "42" },
                new { excelName = "mort_sem_h", description = "Mortalidad semanal hembras", example = "0.5" },
                new { excelName = "mort_sem_m", description = "Mortalidad semanal machos", example = "0.3" },
                new { excelName = "peso_h", description = "Peso hembras", example = "2.1" },
                new { excelName = "peso_m", description = "Peso machos", example = "2.8" },
                new { excelName = "uniformidad", description = "Uniformidad del lote", example = "85" },
                new { excelName = "prod_porcentaje", description = "Porcentaje de producción", example = "92.5" },
                new { excelName = "peso_huevo", description = "Peso del huevo", example = "62.3" }
                // Agregar más columnas según sea necesario
            },
            alternativeColumnNames = new
            {
                note = "El sistema acepta múltiples variaciones de nombres de columnas",
                examples = new[]
                {
                    "anio_guia, año_guia, año guia",
                    "mort_sem_h, mortalidad_semanal_h, mortalidad semanal h",
                    "peso_h, peso_hembra, peso hembra"
                }
            },
            tips = new[]
            {
                "Asegúrate de que la primera fila contenga los nombres de las columnas",
                "Los nombres de columnas no son sensibles a mayúsculas/minúsculas",
                "Se aceptan espacios y guiones bajos en los nombres de columnas",
                "Las celdas vacías se ignoran durante la importación",
                "Revisa los errores en la respuesta para corregir problemas específicos"
            }
        };

        return Ok(templateInfo);
    }

    /// <summary>
    /// Descarga un archivo Excel de plantilla con las columnas esperadas
    /// </summary>
    /// <returns>Archivo Excel de plantilla</returns>
    [HttpGet("download-template")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    public IActionResult DownloadTemplate()
    {
        try
        {
            // Crear un archivo Excel de plantilla
            using var package = new OfficeOpenXml.ExcelPackage();
            var worksheet = package.Workbook.Worksheets.Add("ProduccionAvicola");

            // Agregar encabezados
            var headers = new[]
            {
                "anio_guia", "raza", "edad", "mort_sem_h", "retiro_ac_h", "mort_sem_m", "retiro_ac_m",
                "cons_ac_h", "cons_ac_m", "gr_ave_dia_h", "gr_ave_dia_m", "peso_h", "peso_m",
                "uniformidad", "h_total_aa", "prod_porcentaje", "h_inc_aa", "aprov_sem",
                "peso_huevo", "masa_huevo", "grasa_porcentaje", "nacim_porcentaje", "pollito_aa",
                "kcal_ave_dia_h", "kcal_ave_dia_m", "aprov_ac", "gr_huevo_t", "gr_huevo_inc",
                "gr_pollito", "valor_1000", "valor_150", "apareo", "peso_mh"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                worksheet.Cells[1, i + 1].Value = headers[i];
                worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            }

            // Agregar fila de ejemplo
            var exampleData = new[]
            {
                "2024", "Cobb 500", "42", "0.5", "0.2", "0.3", "0.1",
                "120.5", "135.2", "85.3", "92.1", "2.1", "2.8",
                "85", "150", "92.5", "140", "88.2",
                "62.3", "58.7", "15.2", "82.5", "125",
                "280.5", "310.2", "85.7", "45.2", "42.8",
                "45.5", "1250", "187", "1:8", "2.45"
            };

            for (int i = 0; i < exampleData.Length; i++)
            {
                worksheet.Cells[2, i + 1].Value = exampleData[i];
            }

            // Autoajustar columnas
            worksheet.Cells.AutoFitColumns();

            var fileBytes = package.GetAsByteArray();
            var fileName = $"plantilla_produccion_avicola_{DateTime.Now:yyyyMMdd}.xlsx";

            return File(fileBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al generar la plantilla Excel");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { message = "Error al generar la plantilla Excel." });
        }
    }
}
