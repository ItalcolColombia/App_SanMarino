// src/ZooSanMarino.Infrastructure/Services/ExcelImportService.cs
using Microsoft.AspNetCore.Http;
using OfficeOpenXml;
using System.Reflection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

public class ExcelImportService : IExcelImportService
{
    private readonly IProduccionAvicolaRawService _produccionService;

    static ExcelImportService()
    {
        // Configurar EPPlus para uso no comercial (EPPlus 8+)
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    public ExcelImportService(IProduccionAvicolaRawService produccionService)
    {
        _produccionService = produccionService;
    }

    public async Task<ExcelImportResultDto> ImportProduccionAvicolaFromExcelAsync(IFormFile file)
    {
        var errors = new List<string>();
        var importedData = new List<ProduccionAvicolaRawDto>();
        var totalRows = 0;
        var processedRows = 0;
        var errorRows = 0;

        try
        {
            // Validar archivo
            var validationResult = ValidateFile(file);
            if (!validationResult.IsValid)
            {
                return new ExcelImportResultDto(
                    false, 0, 0, 0, 
                    validationResult.Errors, 
                    new List<ProduccionAvicolaRawDto>()
                );
            }

            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
            {
                errors.Add("El archivo Excel no contiene hojas de trabajo.");
                return new ExcelImportResultDto(false, 0, 0, 1, errors, new List<ProduccionAvicolaRawDto>());
            }

            // Obtener mapeo de columnas desde la primera fila (encabezados)
            var columnMapping = GetColumnMapping(worksheet);
            if (columnMapping.Count == 0)
            {
                errors.Add("No se encontraron columnas válidas en el archivo Excel.");
                return new ExcelImportResultDto(false, 0, 0, 1, errors, new List<ProduccionAvicolaRawDto>());
            }

            // Procesar filas de datos (desde la fila 2)
            totalRows = worksheet.Dimension?.Rows ?? 0;
            
            for (int row = 2; row <= totalRows; row++)
            {
                try
                {
                    var createDto = ProcessRow(worksheet, row, columnMapping);
                    if (createDto != null)
                    {
                        var result = await _produccionService.CreateAsync(createDto);
                        importedData.Add(result);
                        processedRows++;
                    }
                    else
                    {
                        errors.Add($"Fila {row}: No se pudo procesar la fila (datos insuficientes).");
                        errorRows++;
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"Fila {row}: {ex.Message}");
                    errorRows++;
                }
            }

            return new ExcelImportResultDto(
                processedRows > 0,
                totalRows - 1, // Excluir fila de encabezados
                processedRows,
                errorRows,
                errors,
                importedData
            );
        }
        catch (Exception ex)
        {
            errors.Add($"Error general: {ex.Message}");
            return new ExcelImportResultDto(false, totalRows, processedRows, errorRows + 1, errors, importedData);
        }
    }

    public Task<List<ProduccionAvicolaRawDto>> ValidateExcelDataAsync(IFormFile file)
    {
        var validData = new List<ProduccionAvicolaRawDto>();

        try
        {
            using var stream = file.OpenReadStream();
            using var package = new ExcelPackage(stream);
            
            var worksheet = package.Workbook.Worksheets.FirstOrDefault();
            if (worksheet == null) return Task.FromResult(validData);

            var columnMapping = GetColumnMapping(worksheet);
            var totalRows = worksheet.Dimension?.Rows ?? 0;

            for (int row = 2; row <= totalRows; row++)
            {
                var createDto = ProcessRow(worksheet, row, columnMapping);
                if (createDto != null)
                {
                    // Simular la creación para validar los datos
                    var simulatedDto = new ProduccionAvicolaRawDto(
                        0, // ID temporal
                        1, // CompanyId temporal
                        createDto.AnioGuia,
                        createDto.Raza,
                        createDto.Edad,
                        createDto.MortSemH,
                        createDto.RetiroAcH,
                        createDto.MortSemM,
                        createDto.RetiroAcM,
                        createDto.ConsAcH,
                        createDto.ConsAcM,
                        createDto.GrAveDiaH,
                        createDto.GrAveDiaM,
                        createDto.PesoH,
                        createDto.PesoM,
                        createDto.Uniformidad,
                        createDto.HTotalAa,
                        createDto.ProdPorcentaje,
                        createDto.HIncAa,
                        createDto.AprovSem,
                        createDto.PesoHuevo,
                        createDto.MasaHuevo,
                        createDto.GrasaPorcentaje,
                        createDto.NacimPorcentaje,
                        createDto.PollitoAa,
                        createDto.KcalAveDiaH,
                        createDto.KcalAveDiaM,
                        createDto.AprovAc,
                        createDto.GrHuevoT,
                        createDto.GrHuevoInc,
                        createDto.GrPollito,
                        createDto.Valor1000,
                        createDto.Valor150,
                        createDto.Apareo,
                        createDto.PesoMh,
                        DateTime.UtcNow,
                        null
                    );
                    
                    validData.Add(simulatedDto);
                }
            }
        }
        catch (Exception)
        {
            // En caso de error, retornar lista vacía
            return Task.FromResult(new List<ProduccionAvicolaRawDto>());
        }

        return Task.FromResult(validData);
    }

    private (bool IsValid, List<string> Errors) ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        if (file == null)
        {
            errors.Add("No se ha proporcionado ningún archivo.");
            return (false, errors);
        }

        if (file.Length == 0)
        {
            errors.Add("El archivo está vacío.");
            return (false, errors);
        }

        var allowedExtensions = new[] { ".xlsx", ".xls" };
        var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(fileExtension))
        {
            errors.Add($"Formato de archivo no válido. Se permiten: {string.Join(", ", allowedExtensions)}");
            return (false, errors);
        }

        const long maxFileSize = 10 * 1024 * 1024; // 10 MB
        if (file.Length > maxFileSize)
        {
            errors.Add($"El archivo es demasiado grande. Tamaño máximo permitido: {maxFileSize / (1024 * 1024)} MB");
            return (false, errors);
        }

        return (true, errors);
    }

    private Dictionary<int, string> GetColumnMapping(ExcelWorksheet worksheet)
    {
        var mapping = new Dictionary<int, string>();
        
        if (worksheet.Dimension == null) return mapping;

        var totalColumns = worksheet.Dimension.Columns;

        for (int col = 1; col <= totalColumns; col++)
        {
            var headerValue = worksheet.Cells[1, col].Value?.ToString()?.Trim();
            if (!string.IsNullOrEmpty(headerValue))
            {
                var propertyName = ExcelColumnMappings.GetPropertyName(headerValue);
                if (!string.IsNullOrEmpty(propertyName))
                {
                    mapping[col] = propertyName;
                }
            }
        }

        return mapping;
    }

    private CreateProduccionAvicolaRawDto? ProcessRow(ExcelWorksheet worksheet, int row, Dictionary<int, string> columnMapping)
    {
        var values = new Dictionary<string, string?>();
        var hasData = false;

        // Recopilar valores de las celdas
        foreach (var kvp in columnMapping)
        {
            var columnIndex = kvp.Key;
            var propertyName = kvp.Value;

            var cellValue = worksheet.Cells[row, columnIndex].Value?.ToString()?.Trim();
            
            if (!string.IsNullOrEmpty(cellValue))
            {
                hasData = true;
                values[propertyName] = cellValue;
            }
            else
            {
                values[propertyName] = null;
            }
        }

        if (!hasData) return null;

        // Crear el DTO usando los valores recopilados
        return new CreateProduccionAvicolaRawDto(
            values.GetValueOrDefault("AnioGuia"),
            values.GetValueOrDefault("Raza"),
            values.GetValueOrDefault("Edad"),
            values.GetValueOrDefault("MortSemH"),
            values.GetValueOrDefault("RetiroAcH"),
            values.GetValueOrDefault("MortSemM"),
            values.GetValueOrDefault("RetiroAcM"),
            values.GetValueOrDefault("ConsAcH"),
            values.GetValueOrDefault("ConsAcM"),
            values.GetValueOrDefault("GrAveDiaH"),
            values.GetValueOrDefault("GrAveDiaM"),
            values.GetValueOrDefault("PesoH"),
            values.GetValueOrDefault("PesoM"),
            values.GetValueOrDefault("Uniformidad"),
            values.GetValueOrDefault("HTotalAa"),
            values.GetValueOrDefault("ProdPorcentaje"),
            values.GetValueOrDefault("HIncAa"),
            values.GetValueOrDefault("AprovSem"),
            values.GetValueOrDefault("PesoHuevo"),
            values.GetValueOrDefault("MasaHuevo"),
            values.GetValueOrDefault("GrasaPorcentaje"),
            values.GetValueOrDefault("NacimPorcentaje"),
            values.GetValueOrDefault("PollitoAa"),
            values.GetValueOrDefault("KcalAveDiaH"),
            values.GetValueOrDefault("KcalAveDiaM"),
            values.GetValueOrDefault("AprovAc"),
            values.GetValueOrDefault("GrHuevoT"),
            values.GetValueOrDefault("GrHuevoInc"),
            values.GetValueOrDefault("GrPollito"),
            values.GetValueOrDefault("Valor1000"),
            values.GetValueOrDefault("Valor150"),
            values.GetValueOrDefault("Apareo"),
            values.GetValueOrDefault("PesoMh")
        );
    }
}

public static class ExcelColumnMappings
{
    private static readonly Dictionary<string, string> ColumnMappings = new()
    {
        // Encabezados reales del Excel -> Propiedades del DTO
        { "AÑOGUÍA", "AnioGuia" },
        { "RAZA", "Raza" },
        { "Edad", "Edad" },
        { "%MortSemH", "MortSemH" },
        { "RetiroAcH", "RetiroAcH" },
        { "%MortSemM", "MortSemM" },
        { "RetiroAcM", "RetiroAcM" },
        { "ConsAcH", "ConsAcH" },
        { "ConsAcM", "ConsAcM" },
        { "GrAveDiaH", "GrAveDiaH" },
        { "GrAveDiaM", "GrAveDiaM" },
        { "PesoH", "PesoH" },
        { "PesoM", "PesoM" },
        { "%Uniform", "Uniformidad" },
        { "HTotalAA", "HTotalAa" },
        { "%Prod", "ProdPorcentaje" },
        { "HIncAA", "HIncAa" },
        { "%AprovSem", "AprovSem" },
        { "PesoHuevo", "PesoHuevo" },
        { "MasaHuevo", "MasaHuevo" },
        { "%Grasa", "GrasaPorcentaje" },
        { "%Nac im", "NacimPorcentaje" },
        { "PollitoAA", "PollitoAa" },
        { "KcalAveDiaH", "KcalAveDiaH" },
        { "KcalAveDiaM", "KcalAveDiaM" },
        { "%AprovAc", "AprovAc" },
        { "GR/HuevoT", "GrHuevoT" },
        { "GR/HuevoInc", "GrHuevoInc" },
        { "GR/Pollito", "GrPollito" },
        { "1000", "Valor1000" },
        { "150", "Valor150" },
        { "%Apareo", "Apareo" },
        { "PesoM/H", "PesoMh" }
    };

    public static string? GetPropertyName(string excelHeader)
    {
        if (string.IsNullOrWhiteSpace(excelHeader))
            return null;

        // Limpiar el encabezado (quitar espacios extra)
        var cleanHeader = excelHeader.Trim();

        // Buscar coincidencia exacta
        if (ColumnMappings.TryGetValue(cleanHeader, out var propertyName))
            return propertyName;

        // Buscar coincidencia sin distinguir mayúsculas/minúsculas
        var match = ColumnMappings.FirstOrDefault(kvp => 
            string.Equals(kvp.Key, cleanHeader, StringComparison.OrdinalIgnoreCase));
        
        return match.Key != null ? match.Value : null;
    }

    public static List<string> GetAllSupportedHeaders()
    {
        return ColumnMappings.Keys.ToList();
    }
}
