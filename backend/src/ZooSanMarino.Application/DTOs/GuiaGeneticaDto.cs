namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// DTO para obtener datos de guía genética por edad/semana
/// </summary>
public record GuiaGeneticaDto(
    int Edad,
    double ConsumoHembras,      // Gramos por ave por día
    double ConsumoMachos,       // Gramos por ave por día
    double PesoHembras,         // Peso esperado hembras
    double PesoMachos,          // Peso esperado machos
    double MortalidadHembras,   // Mortalidad esperada hembras
    double MortalidadMachos,    // Mortalidad esperada machos
    double Uniformidad,         // Uniformidad esperada
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
