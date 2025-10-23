// src/ZooSanMarino.Application/DTOs/ExcelImportDto.cs
namespace ZooSanMarino.Application.DTOs;

public record ExcelImportResultDto(
    bool Success,
    int TotalRows,
    int ProcessedRows,
    int ErrorRows,
    List<string> Errors,
    List<ProduccionAvicolaRawDto> ImportedData
);

public record ExcelValidationErrorDto(
    int Row,
    string Column,
    string Value,
    string Error
);

public record ExcelColumnMappingDto(
    string ExcelColumn,
    string DatabaseColumn,
    bool IsRequired
);

public static class ExcelColumnMappings
{
    public static readonly Dictionary<string, string> ProduccionAvicolaColumns = new()
    {
        // Mapeo de nombres de columnas Excel a propiedades de la entidad
        { "anio_guia", "AnioGuia" },
        { "año_guia", "AnioGuia" },
        { "año guia", "AnioGuia" },
        { "raza", "Raza" },
        { "edad", "Edad" },
        { "mort_sem_h", "MortSemH" },
        { "mortalidad_semanal_h", "MortSemH" },
        { "mortalidad semanal h", "MortSemH" },
        { "retiro_ac_h", "RetiroAcH" },
        { "retiro_acumulado_h", "RetiroAcH" },
        { "retiro acumulado h", "RetiroAcH" },
        { "mort_sem_m", "MortSemM" },
        { "mortalidad_semanal_m", "MortSemM" },
        { "mortalidad semanal m", "MortSemM" },
        { "retiro_ac_m", "RetiroAcM" },
        { "retiro_acumulado_m", "RetiroAcM" },
        { "retiro acumulado m", "RetiroAcM" },
        { "cons_ac_h", "ConsAcH" },
        { "consumo_acumulado_h", "ConsAcH" },
        { "consumo acumulado h", "ConsAcH" },
        { "cons_ac_m", "ConsAcM" },
        { "consumo_acumulado_m", "ConsAcM" },
        { "consumo acumulado m", "ConsAcM" },
        { "gr_ave_dia_h", "GrAveDiaH" },
        { "gramos_ave_dia_h", "GrAveDiaH" },
        { "gramos ave dia h", "GrAveDiaH" },
        { "gr_ave_dia_m", "GrAveDiaM" },
        { "gramos_ave_dia_m", "GrAveDiaM" },
        { "gramos ave dia m", "GrAveDiaM" },
        { "peso_h", "PesoH" },
        { "peso_hembra", "PesoH" },
        { "peso hembra", "PesoH" },
        { "peso_m", "PesoM" },
        { "peso_macho", "PesoM" },
        { "peso macho", "PesoM" },
        { "uniformidad", "Uniformidad" },
        { "h_total_aa", "HTotalAa" },
        { "hembras_total_aa", "HTotalAa" },
        { "hembras total aa", "HTotalAa" },
        { "prod_porcentaje", "ProdPorcentaje" },
        { "produccion_porcentaje", "ProdPorcentaje" },
        { "produccion porcentaje", "ProdPorcentaje" },
        { "h_inc_aa", "HIncAa" },
        { "hembras_incubacion_aa", "HIncAa" },
        { "hembras incubacion aa", "HIncAa" },
        { "aprov_sem", "AprovSem" },
        { "aprovechamiento_semanal", "AprovSem" },
        { "aprovechamiento semanal", "AprovSem" },
        { "peso_huevo", "PesoHuevo" },
        { "peso huevo", "PesoHuevo" },
        { "masa_huevo", "MasaHuevo" },
        { "masa huevo", "MasaHuevo" },
        { "grasa_porcentaje", "GrasaPorcentaje" },
        { "grasa porcentaje", "GrasaPorcentaje" },
        { "nacim_porcentaje", "NacimPorcentaje" },
        { "nacimiento_porcentaje", "NacimPorcentaje" },
        { "nacimiento porcentaje", "NacimPorcentaje" },
        { "pollito_aa", "PollitoAa" },
        { "pollito aa", "PollitoAa" },
        { "kcal_ave_dia_h", "KcalAveDiaH" },
        { "kcal_ave_dia_m", "KcalAveDiaM" },
        { "aprov_ac", "AprovAc" },
        { "aprovechamiento_acumulado", "AprovAc" },
        { "aprovechamiento acumulado", "AprovAc" },
        { "gr_huevo_t", "GrHuevoT" },
        { "gramos_huevo_total", "GrHuevoT" },
        { "gramos huevo total", "GrHuevoT" },
        { "gr_huevo_inc", "GrHuevoInc" },
        { "gramos_huevo_incubacion", "GrHuevoInc" },
        { "gramos huevo incubacion", "GrHuevoInc" },
        { "gr_pollito", "GrPollito" },
        { "gramos_pollito", "GrPollito" },
        { "gramos pollito", "GrPollito" },
        { "valor_1000", "Valor1000" },
        { "valor 1000", "Valor1000" },
        { "valor_150", "Valor150" },
        { "valor 150", "Valor150" },
        { "apareo", "Apareo" },
        { "peso_mh", "PesoMh" },
        { "peso_macho_hembra", "PesoMh" },
        { "peso macho hembra", "PesoMh" }
    };

    public static string? GetPropertyName(string excelColumnName)
    {
        var normalizedKey = excelColumnName.ToLowerInvariant().Trim();
        return ProduccionAvicolaColumns.TryGetValue(normalizedKey, out var propertyName) 
            ? propertyName 
            : null;
    }
}
