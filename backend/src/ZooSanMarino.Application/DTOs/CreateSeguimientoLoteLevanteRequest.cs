using System.Text.Json;

// file: backend/src/ZooSanMarino.Application/DTOs/CreateSeguimientoLoteLevanteRequest.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Request DTO para crear/actualizar seguimiento de lote levante.
/// Permite enviar consumo en kg o gramos, el backend hace la conversión.
/// </summary>
public class CreateSeguimientoLoteLevanteRequest
{
    public int LoteId { get; set; }
    public DateTime FechaRegistro { get; set; }
    
    public int MortalidadHembras { get; set; }
    public int MortalidadMachos { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
    public int ErrorSexajeHembras { get; set; }
    public int ErrorSexajeMachos { get; set; }
    
    public string TipoAlimento { get; set; } = string.Empty;
    
    // Consumo con unidad opcional (el backend convierte a kg)
    public double? ConsumoHembras { get; set; }
    public string? UnidadConsumoHembras { get; set; } // "kg" o "g" - default "kg"
    public double? ConsumoMachos { get; set; }
    public string? UnidadConsumoMachos { get; set; } // "kg" o "g" - default "kg"
    
    // IDs de alimentos (opcionales, para validación de inventario)
    public int? TipoAlimentoHembras { get; set; }
    public int? TipoAlimentoMachos { get; set; }
    
    // Tipo de ítem (alimento, medicamento, etc.) - se guarda en Metadata
    public string? TipoItemHembras { get; set; }
    public string? TipoItemMachos { get; set; }
    
    public string? Observaciones { get; set; }
    public double? KcalAlH { get; set; }
    public double? ProtAlH { get; set; }
    public double? KcalAveH { get; set; }
    public double? ProtAveH { get; set; }
    public string Ciclo { get; set; } = "Normal";
    
    public double? PesoPromH { get; set; }
    public double? PesoPromM { get; set; }
    public double? UniformidadH { get; set; }
    public double? UniformidadM { get; set; }
    public double? CvH { get; set; }
    public double? CvM { get; set; }
    
    /// <summary>
    /// Convierte este request a SeguimientoLoteLevanteDto, haciendo la conversión de unidades si es necesario.
    /// </summary>
    public SeguimientoLoteLevanteDto ToDto(int? id = null)
    {
        // Convertir consumo a kg si viene en gramos
        double consumoKgHembras = 0;
        if (ConsumoHembras.HasValue && ConsumoHembras.Value > 0)
        {
            var unidadH = (UnidadConsumoHembras ?? "kg").ToLower().Trim();
            if (unidadH == "g" || unidadH == "gramos" || unidadH == "gramo")
            {
                consumoKgHembras = ConsumoHembras.Value / 1000.0; // Convertir gramos a kg
            }
            else
            {
                consumoKgHembras = ConsumoHembras.Value; // Ya está en kg
            }
        }
        
        double? consumoKgMachos = null;
        if (ConsumoMachos.HasValue && ConsumoMachos.Value > 0)
        {
            var unidadM = (UnidadConsumoMachos ?? "kg").ToLower().Trim();
            if (unidadM == "g" || unidadM == "gramos" || unidadM == "gramo")
            {
                consumoKgMachos = ConsumoMachos.Value / 1000.0; // Convertir gramos a kg
            }
            else
            {
                consumoKgMachos = ConsumoMachos.Value; // Ya está en kg
            }
        }
        
        return new SeguimientoLoteLevanteDto(
            Id: id ?? 0,
            LoteId: LoteId,
            FechaRegistro: FechaRegistro,
            MortalidadHembras: MortalidadHembras,
            MortalidadMachos: MortalidadMachos,
            SelH: SelH,
            SelM: SelM,
            ErrorSexajeHembras: ErrorSexajeHembras,
            ErrorSexajeMachos: ErrorSexajeMachos,
            ConsumoKgHembras: consumoKgHembras,
            TipoAlimento: TipoAlimento,
            Observaciones: Observaciones,
            KcalAlH: KcalAlH,
            ProtAlH: ProtAlH,
            KcalAveH: KcalAveH,
            ProtAveH: ProtAveH,
            Ciclo: Ciclo,
            ConsumoKgMachos: consumoKgMachos,
            PesoPromH: PesoPromH,
            PesoPromM: PesoPromM,
            UniformidadH: UniformidadH,
            UniformidadM: UniformidadM,
            CvH: CvH,
            CvM: CvM,
            // Metadata JSONB con consumo original, tipo de ítem y otros campos adicionales
            Metadata: BuildMetadata(ConsumoHembras, UnidadConsumoHembras, ConsumoMachos, UnidadConsumoMachos, 
                                   TipoItemHembras, TipoItemMachos, TipoAlimentoHembras, TipoAlimentoMachos)
        );
    }
    
    /// <summary>
    /// Construye el objeto Metadata JSONB con los campos adicionales.
    /// </summary>
    private static JsonDocument? BuildMetadata(double? consumoHembras, string? unidadHembras, 
                                               double? consumoMachos, string? unidadMachos,
                                               string? tipoItemHembras, string? tipoItemMachos,
                                               int? tipoAlimentoHembras, int? tipoAlimentoMachos)
    {
        var metadata = new Dictionary<string, object?>();
        
        // Consumo original con unidad
        if (consumoHembras.HasValue)
        {
            metadata["consumoOriginalHembras"] = consumoHembras.Value;
            metadata["unidadConsumoOriginalHembras"] = unidadHembras ?? "kg";
        }
        
        if (consumoMachos.HasValue)
        {
            metadata["consumoOriginalMachos"] = consumoMachos.Value;
            metadata["unidadConsumoOriginalMachos"] = unidadMachos ?? "kg";
        }
        
        // Tipo de ítem (alimento, medicamento, etc.)
        if (!string.IsNullOrWhiteSpace(tipoItemHembras))
        {
            metadata["tipoItemHembras"] = tipoItemHembras;
        }
        
        if (!string.IsNullOrWhiteSpace(tipoItemMachos))
        {
            metadata["tipoItemMachos"] = tipoItemMachos;
        }
        
        // IDs de alimentos seleccionados
        if (tipoAlimentoHembras.HasValue)
        {
            metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        }
        
        if (tipoAlimentoMachos.HasValue)
        {
            metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        }
        
        if (metadata.Count == 0) return null;
        
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }
}

