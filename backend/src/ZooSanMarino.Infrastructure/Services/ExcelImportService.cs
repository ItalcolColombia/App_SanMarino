// src/ZooSanMarino.Infrastructure/Services/ExcelImportService.cs
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using System.Reflection;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.Interfaces;
using ZooSanMarino.Infrastructure.Persistence;

namespace ZooSanMarino.Infrastructure.Services;

public class ExcelImportService : IExcelImportService
{
    private readonly IProduccionAvicolaRawService _produccionService;
    private readonly ZooSanMarinoContext _context;
    private readonly ICurrentUser _currentUser;
    private readonly ICompanyResolver _companyResolver;

    static ExcelImportService()
    {
        // Configurar EPPlus para uso no comercial (EPPlus 8+)
        ExcelPackage.License.SetNonCommercialPersonal("ZooSanMarino");
    }

    public ExcelImportService(IProduccionAvicolaRawService produccionService, ZooSanMarinoContext context, ICurrentUser currentUser, ICompanyResolver companyResolver)
    {
        _produccionService = produccionService;
        _context = context;
        _currentUser = currentUser;
        _companyResolver = companyResolver;
    }

    private async Task<int> GetEffectiveCompanyIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.ActiveCompanyName))
        {
            var cid = await _companyResolver.GetCompanyIdByNameAsync(_currentUser.ActiveCompanyName);
            if (cid.HasValue) return cid.Value;
        }
        return _currentUser.CompanyId;
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
            var effectiveCompanyId = await GetEffectiveCompanyIdAsync();
            
            for (int row = 2; row <= totalRows; row++)
            {
                try
                {
                    var createDto = ProcessRow(worksheet, row, columnMapping);
                    if (createDto != null)
                    {
                        // Completar código si no viene en Excel (se calcula por fórmula)
                        var codigo = ComputeCodigo(createDto);
                        if (string.IsNullOrWhiteSpace(codigo))
                        {
                            errors.Add($"Fila {row}: No se pudo determinar CodigoGuiaGenetica (requiere Raza + AnioGuia + Edad).");
                            errorRows++;
                            continue;
                        }
                        createDto = createDto with { CodigoGuiaGenetica = codigo };

                        // Validación mínima: debe existir anio_guia + raza + edad (las fórmulas se calculan con lo que exista)
                        var missingKeys = GetMissingKeyFields(createDto);
                        if (missingKeys.Count > 0)
                        {
                            errors.Add($"Fila {row}: Faltan campos clave: {string.Join(", ", missingKeys)}");
                            errorRows++;
                            continue;
                        }

                        // Upsert por codigo_guia_genetica (mismo company)
                        var existing = await _context.ProduccionAvicolaRaw
                            .AsNoTracking()
                            .Where(x => x.CompanyId == effectiveCompanyId && x.CodigoGuiaGenetica == codigo)
                            .Select(x => new
                            {
                                x.Id,
                                x.CodigoGuiaGenetica,
                                x.AnioGuia,
                                x.Raza,
                                x.Edad,
                                x.MortSemH,
                                x.RetiroAcH,
                                x.MortSemM,
                                x.RetiroAcM,
                                x.Hembras,
                                x.Machos,
                                x.ConsAcH,
                                x.ConsAcM,
                                x.GrAveDiaH,
                                x.GrAveDiaM,
                                x.PesoH,
                                x.PesoM,
                                x.Uniformidad,
                                x.HTotalAa,
                                x.ProdPorcentaje,
                                x.HIncAa,
                                x.AprovSem,
                                x.PesoHuevo,
                                x.MasaHuevo,
                                x.GrasaPorcentaje,
                                x.NacimPorcentaje,
                                x.PollitoAa,
                                x.AlimH,
                                x.KcalAveDiaH,
                                x.KcalAveDiaM,
                                x.KcalH,
                                x.ProtH,
                                x.AlimM,
                                x.KcalM,
                                x.ProtM,
                                x.KcalSemH,
                                x.ProtHSem,
                                x.KcalSemM,
                                x.ProtSemM,
                                x.AprovAc,
                                x.GrHuevoT,
                                x.GrHuevoInc,
                                x.GrPollito,
                                x.Valor1000,
                                x.Valor150,
                                x.Apareo,
                                x.PesoMh
                            })
                            .FirstOrDefaultAsync();

                        ProduccionAvicolaRawDto result;
                        if (existing != null)
                        {
                            // Si ya existe:
                            // - Si Excel trae un campo distinto al guardado (y el guardado NO está vacío) => error
                            // - Si el guardado está vacío y Excel trae valor => lo rellenamos
                            var diffs = new List<string>();

                            string? Merge(string field, string? incoming, string? stored)
                            {
                                if (IsEmpty(incoming))
                                    return stored;

                                if (IsEmpty(stored))
                                    return incoming;

                                if (!AreEquivalent(incoming, stored))
                                    diffs.Add(field);

                                return stored;
                            }

                            var merged = new
                            {
                                CodigoGuiaGenetica = existing.CodigoGuiaGenetica ?? codigo,
                                AnioGuia = Merge("anio_guia", createDto.AnioGuia, existing.AnioGuia),
                                Raza = Merge("raza", createDto.Raza, existing.Raza),
                                Edad = Merge("edad", createDto.Edad, existing.Edad),
                                MortSemH = Merge("mort_sem_h", createDto.MortSemH, existing.MortSemH),
                                RetiroAcH = Merge("retiro_ac_h", createDto.RetiroAcH, existing.RetiroAcH),
                                MortSemM = Merge("mort_sem_m", createDto.MortSemM, existing.MortSemM),
                                RetiroAcM = Merge("retiro_ac_m", createDto.RetiroAcM, existing.RetiroAcM),
                                Hembras = Merge("hembras", createDto.Hembras, existing.Hembras),
                                Machos = Merge("machos", createDto.Machos, existing.Machos),
                                ConsAcH = Merge("cons_ac_h", createDto.ConsAcH, existing.ConsAcH),
                                ConsAcM = Merge("cons_ac_m", createDto.ConsAcM, existing.ConsAcM),
                                GrAveDiaH = Merge("gr_ave_dia_h", createDto.GrAveDiaH, existing.GrAveDiaH),
                                GrAveDiaM = Merge("gr_ave_dia_m", createDto.GrAveDiaM, existing.GrAveDiaM),
                                PesoH = Merge("peso_h", createDto.PesoH, existing.PesoH),
                                PesoM = Merge("peso_m", createDto.PesoM, existing.PesoM),
                                Uniformidad = Merge("uniformidad", createDto.Uniformidad, existing.Uniformidad),
                                HTotalAa = Merge("h_total_aa", createDto.HTotalAa, existing.HTotalAa),
                                ProdPorcentaje = Merge("prod_porcentaje", createDto.ProdPorcentaje, existing.ProdPorcentaje),
                                HIncAa = Merge("h_inc_aa", createDto.HIncAa, existing.HIncAa),
                                AprovSem = Merge("aprov_sem", createDto.AprovSem, existing.AprovSem),
                                PesoHuevo = Merge("peso_huevo", createDto.PesoHuevo, existing.PesoHuevo),
                                MasaHuevo = Merge("masa_huevo", createDto.MasaHuevo, existing.MasaHuevo),
                                GrasaPorcentaje = Merge("grasa_porcentaje", createDto.GrasaPorcentaje, existing.GrasaPorcentaje),
                                NacimPorcentaje = Merge("nacim_porcentaje", createDto.NacimPorcentaje, existing.NacimPorcentaje),
                                PollitoAa = Merge("pollito_aa", createDto.PollitoAa, existing.PollitoAa),
                                AlimH = Merge("alim_h", createDto.AlimH, existing.AlimH),
                                KcalAveDiaH = Merge("kcal_ave_dia_h", createDto.KcalAveDiaH, existing.KcalAveDiaH),
                                KcalAveDiaM = Merge("kcal_ave_dia_m", createDto.KcalAveDiaM, existing.KcalAveDiaM),
                                KcalH = Merge("kcal_h", createDto.KcalH, existing.KcalH),
                                ProtH = Merge("prot_h", createDto.ProtH, existing.ProtH),
                                AlimM = Merge("alim_m", createDto.AlimM, existing.AlimM),
                                KcalM = Merge("kcal_m", createDto.KcalM, existing.KcalM),
                                ProtM = Merge("prot_m", createDto.ProtM, existing.ProtM),
                                KcalSemH = Merge("kcal_sem_h", createDto.KcalSemH, existing.KcalSemH),
                                ProtHSem = Merge("prot_h_sem", createDto.ProtHSem, existing.ProtHSem),
                                KcalSemM = Merge("kcal_sem_m", createDto.KcalSemM, existing.KcalSemM),
                                ProtSemM = Merge("prot_sem_m", createDto.ProtSemM, existing.ProtSemM),
                                AprovAc = Merge("aprov_ac", createDto.AprovAc, existing.AprovAc),
                                GrHuevoT = Merge("gr_huevo_t", createDto.GrHuevoT, existing.GrHuevoT),
                                GrHuevoInc = Merge("gr_huevo_inc", createDto.GrHuevoInc, existing.GrHuevoInc),
                                GrPollito = Merge("gr_pollito", createDto.GrPollito, existing.GrPollito),
                                Valor1000 = Merge("valor_1000", createDto.Valor1000, existing.Valor1000),
                                Valor150 = Merge("valor_150", createDto.Valor150, existing.Valor150),
                                Apareo = Merge("apareo", createDto.Apareo, existing.Apareo),
                                PesoMh = Merge("peso_mh", createDto.PesoMh, existing.PesoMh)
                            };

                            if (diffs.Count > 0)
                            {
                                errors.Add($"Fila {row}: El código '{codigo}' ya existe y los campos difieren: {string.Join(", ", diffs)}");
                                errorRows++;
                                continue;
                            }

                            // Si no hay diferencias, actualizamos (esto también recalcula campos automáticos si faltaban)
                            var updateDto = new UpdateProduccionAvicolaRawDto(
                                existing.Id,
                                merged.CodigoGuiaGenetica,
                                merged.AnioGuia,
                                merged.Raza,
                                merged.Edad,
                                merged.MortSemH,
                                merged.RetiroAcH,
                                merged.MortSemM,
                                merged.RetiroAcM,
                                merged.Hembras,
                                merged.Machos,
                                merged.ConsAcH,
                                merged.ConsAcM,
                                merged.GrAveDiaH,
                                merged.GrAveDiaM,
                                merged.PesoH,
                                merged.PesoM,
                                merged.Uniformidad,
                                merged.HTotalAa,
                                merged.ProdPorcentaje,
                                merged.HIncAa,
                                merged.AprovSem,
                                merged.PesoHuevo,
                                merged.MasaHuevo,
                                merged.GrasaPorcentaje,
                                merged.NacimPorcentaje,
                                merged.PollitoAa,
                                merged.AlimH,
                                merged.KcalAveDiaH,
                                merged.KcalAveDiaM,
                                merged.KcalH,
                                merged.ProtH,
                                merged.AlimM,
                                merged.KcalM,
                                merged.ProtM,
                                merged.KcalSemH,
                                merged.ProtHSem,
                                merged.KcalSemM,
                                merged.ProtSemM,
                                merged.AprovAc,
                                merged.GrHuevoT,
                                merged.GrHuevoInc,
                                merged.GrPollito,
                                merged.Valor1000,
                                merged.Valor150,
                                merged.Apareo,
                                merged.PesoMh
                            );

                            result = await _produccionService.UpdateAsync(updateDto);
                        }
                        else
                        {
                            // Si no existe, insertamos normal
                            result = await _produccionService.CreateAsync(createDto);
                        }

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
                    var codigo = ComputeCodigo(createDto);
                    if (string.IsNullOrWhiteSpace(codigo)) continue;
                    createDto = createDto with { CodigoGuiaGenetica = codigo };

                    var missingKeys = GetMissingKeyFields(createDto);
                    if (missingKeys.Count > 0) continue;

                    // Simular la creación para validar los datos
                    var simulatedDto = new ProduccionAvicolaRawDto(
                        0, // ID temporal
                        1, // CompanyId temporal
                        createDto.CodigoGuiaGenetica,
                        createDto.AnioGuia,
                        createDto.Raza,
                        createDto.Edad,
                        createDto.MortSemH,
                        createDto.RetiroAcH,
                        createDto.MortSemM,
                        createDto.RetiroAcM,
                        createDto.Hembras,
                        createDto.Machos,
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
                        createDto.AlimH,
                        createDto.KcalAveDiaH,
                        createDto.KcalAveDiaM,
                        createDto.KcalH,
                        createDto.ProtH,
                        createDto.AlimM,
                        createDto.KcalM,
                        createDto.ProtM,
                        createDto.KcalSemH,
                        createDto.ProtHSem,
                        createDto.KcalSemM,
                        createDto.ProtSemM,
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

    private static string? ComputeCodigo(CreateProduccionAvicolaRawDto dto)
    {
        if (!string.IsNullOrWhiteSpace(dto.CodigoGuiaGenetica)) return dto.CodigoGuiaGenetica.Trim();
        if (string.IsNullOrWhiteSpace(dto.Raza) || string.IsNullOrWhiteSpace(dto.AnioGuia) || string.IsNullOrWhiteSpace(dto.Edad))
            return null;
        return $"{dto.Raza.Trim()}{dto.AnioGuia.Trim()}{dto.Edad.Trim()}";
    }

    private static List<string> GetMissingKeyFields(CreateProduccionAvicolaRawDto dto)
    {
        // Solo campos mínimos para poder identificar el registro y calcular el código
        var required = new Dictionary<string, string?>
        {
            ["anio_guia"] = dto.AnioGuia,
            ["raza"] = dto.Raza,
            ["edad"] = dto.Edad
        };

        return required
            .Where(kvp => string.IsNullOrWhiteSpace(kvp.Value))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    private static bool IsEmpty(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool AreEquivalent(string? a, string? b)
    {
        if (IsEmpty(a) && IsEmpty(b)) return true;
        if (a == null || b == null) return false;

        var na = NormalizeForCompare(a);
        var nb = NormalizeForCompare(b);

        // Si ambos son números (después de normalizar), comparar numéricamente (evita "58.7" vs "58.70")
        if (decimal.TryParse(na, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var da) &&
            decimal.TryParse(nb, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var db))
        {
            return Math.Abs(da - db) < 0.0001m;
        }

        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeForCompare(string input)
    {
        var s = input.Trim();
        s = s.Replace(" ", "");
        s = s.Replace("%", "");
        // normalizar decimales básicos
        if (s.Contains(',') && !s.Contains('.'))
            s = s.Replace(',', '.');
        // mantener ":" para ratios (1:8)
        return s;
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
            values.GetValueOrDefault("CodigoGuiaGenetica"),
            values.GetValueOrDefault("AnioGuia"),
            values.GetValueOrDefault("Raza"),
            values.GetValueOrDefault("Edad"),
            values.GetValueOrDefault("MortSemH"),
            values.GetValueOrDefault("RetiroAcH"),
            values.GetValueOrDefault("MortSemM"),
            values.GetValueOrDefault("RetiroAcM"),
            values.GetValueOrDefault("Hembras"),
            values.GetValueOrDefault("Machos"),
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
            values.GetValueOrDefault("AlimH"),
            values.GetValueOrDefault("KcalAveDiaH"),
            values.GetValueOrDefault("KcalAveDiaM"),
            values.GetValueOrDefault("KcalH"),
            values.GetValueOrDefault("ProtH"),
            values.GetValueOrDefault("AlimM"),
            values.GetValueOrDefault("KcalM"),
            values.GetValueOrDefault("ProtM"),
            values.GetValueOrDefault("KcalSemH"),
            values.GetValueOrDefault("ProtHSem"),
            values.GetValueOrDefault("KcalSemM"),
            values.GetValueOrDefault("ProtSemM"),
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
        // === Encabezados "Excel rojo" (cliente) ===
        { "CodigoGuiaGenetica", "CodigoGuiaGenetica" },
        { "CódigoGuiaGenetica", "CodigoGuiaGenetica" },
        { "Código Guía Genética", "CodigoGuiaGenetica" },
        { "CODIGOGUIAGENETICA", "CodigoGuiaGenetica" },
        { "CODGUÍA", "CodigoGuiaGenetica" },
        { "CODGUIA", "CodigoGuiaGenetica" },
        { "AÑOGUÍA", "AnioGuia" },
        { "AÑO GUÍA", "AnioGuia" },
        { "RAZA", "Raza" },
        { "Edad", "Edad" },
        { "%MortSemH", "MortSemH" },
        { "RetiroAcH", "RetiroAcH" },
        { "%MortSemM", "MortSemM" },
        { "RetiroAcM", "RetiroAcM" },
        { "Hembras", "Hembras" },
        { "Machos", "Machos" },
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
        { "%Nac", "NacimPorcentaje" },
        { "%Nac im", "NacimPorcentaje" },
        { "%Nacim", "NacimPorcentaje" },
        { "PollitoAA", "PollitoAa" },
        { "AlimH", "AlimH" },
        { "KcalAveDiaH", "KcalAveDiaH" },
        { "KcalAveDiaM", "KcalAveDiaM" },
        { "KcalH", "KcalH" },
        { "ProtH", "ProtH" },
        { "AlimM", "AlimM" },
        { "KcalM", "KcalM" },
        { "ProtM", "ProtM" },
        { "KcalSemH", "KcalSemH" },
        { "ProtHSem", "ProtHSem" },
        { "KcalSemM", "KcalSemM" },
        { "ProtSemM", "ProtSemM" },
        { "%AprovAc", "AprovAc" },
        { "GR/HuevoT", "GrHuevoT" },
        { "GR/HuevoInc", "GrHuevoInc" },
        { "GR/Pollito", "GrPollito" },
        { "1000", "Valor1000" },
        { "150", "Valor150" },
        { "%Apareo", "Apareo" },
        { "PesoM/H", "PesoMh" },

        // === Encabezados de plantilla (download-template) / snake_case ===
        { "codigo_guia_genetica", "CodigoGuiaGenetica" },
        { "anio_guia", "AnioGuia" },
        { "raza", "Raza" },
        { "edad", "Edad" },
        { "mort_sem_h", "MortSemH" },
        { "retiro_ac_h", "RetiroAcH" },
        { "mort_sem_m", "MortSemM" },
        { "retiro_ac_m", "RetiroAcM" },
        { "hembras", "Hembras" },
        { "machos", "Machos" },
        { "cons_ac_h", "ConsAcH" },
        { "cons_ac_m", "ConsAcM" },
        { "gr_ave_dia_h", "GrAveDiaH" },
        { "gr_ave_dia_m", "GrAveDiaM" },
        { "peso_h", "PesoH" },
        { "peso_m", "PesoM" },
        { "uniformidad", "Uniformidad" },
        { "h_total_aa", "HTotalAa" },
        { "prod_porcentaje", "ProdPorcentaje" },
        { "h_inc_aa", "HIncAa" },
        { "aprov_sem", "AprovSem" },
        { "peso_huevo", "PesoHuevo" },
        { "masa_huevo", "MasaHuevo" },
        { "grasa_porcentaje", "GrasaPorcentaje" },
        { "nacim_porcentaje", "NacimPorcentaje" },
        { "pollito_aa", "PollitoAa" },
        { "alim_h", "AlimH" },
        { "kcal_ave_dia_h", "KcalAveDiaH" },
        { "kcal_ave_dia_m", "KcalAveDiaM" },
        { "kcal_h", "KcalH" },
        { "prot_h", "ProtH" },
        { "alim_m", "AlimM" },
        { "kcal_m", "KcalM" },
        { "prot_m", "ProtM" },
        { "kcal_sem_h", "KcalSemH" },
        { "prot_h_sem", "ProtHSem" },
        { "kcal_sem_m", "KcalSemM" },
        { "prot_sem_m", "ProtSemM" },
        { "aprov_ac", "AprovAc" },
        { "gr_huevo_t", "GrHuevoT" },
        { "gr_huevo_inc", "GrHuevoInc" },
        { "gr_pollito", "GrPollito" },
        { "valor_1000", "Valor1000" },
        { "valor_150", "Valor150" },
        { "apareo", "Apareo" },
        { "peso_mh", "PesoMh" }
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
