namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para obtener datos de guía genética por edad/semana
/// Mapeo 1:1 con columnas de produccion_avicola_raw.csv
/// </summary>
public record GuiaGeneticaDto(
    int Edad,
    double ConsumoHembras,      // GrAveDiaH - Gramos por ave por día hembras
    double ConsumoMachos,       // GrAveDiaM - Gramos por ave por día machos
    double PesoHembras,         // PesoH - Peso esperado hembras (gramos)
    double PesoMachos,          // PesoM - Peso esperado machos (gramos)
    double MortalidadHembras,   // MortSemH - Mortalidad semanal hembras (%)
    double MortalidadMachos,    // MortSemM - Mortalidad semanal machos (%)
    double RetiroAcumuladoHembras,  // RetiroAcH - Retiro acumulado hembras (%)
    double RetiroAcumuladoMachos,   // RetiroAcM - Retiro acumulado machos (%)
    double Uniformidad,         // Uniformidad - Único valor no separado por sexo (%)
    bool PisoTermicoRequerido,  // Si requiere piso térmico
    string? Observaciones       // Observaciones adicionales
);

/// <summary>
/// Request para obtener guía genética
/// </summary>
public record GuiaGeneticaRequest(
    string Raza,
    int AnoTabla,
    int Edad
);

/// <summary>
/// Response con datos de guía genética
/// </summary>
public record GuiaGeneticaResponse(
    bool Existe,
    GuiaGeneticaDto? Datos,
    string? Mensaje
);
