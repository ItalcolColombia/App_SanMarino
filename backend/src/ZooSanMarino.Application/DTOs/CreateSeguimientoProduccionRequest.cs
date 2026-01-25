using System.Text.Json;

// file: backend/src/ZooSanMarino.Application/DTOs/CreateSeguimientoProduccionRequest.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Request DTO para crear/actualizar seguimiento de producción.
/// Permite enviar consumo en kg o gramos, el backend hace la conversión.
/// </summary>
public class CreateSeguimientoProduccionRequest
{
    public int LoteId { get; set; }
    public DateTime Fecha { get; set; }
    
    public int MortalidadH { get; set; }
    public int MortalidadM { get; set; }
    public int SelH { get; set; }
    public int SelM { get; set; }
    
    // Consumo con unidad opcional (el backend convierte a kg)
    public double? ConsumoH { get; set; }
    public string? UnidadConsumoH { get; set; } // "kg" o "g" - default "kg"
    public double? ConsumoM { get; set; }
    public string? UnidadConsumoM { get; set; } // "kg" o "g" - default "kg"
    
    public int HuevoTot { get; set; }
    public int HuevoInc { get; set; }
    
    // Campos de Clasificadora de Huevos - (Limpio, Tratado) = HuevoInc +
    public int? HuevoLimpio { get; set; }
    public int? HuevoTratado { get; set; }
    
    // Campos de Clasificadora de Huevos - (Sucio, Deforme, Blanco, Doble Yema, Piso, Pequeño, Roto, Desecho, Otro) = Huevo Total
    public int? HuevoSucio { get; set; }
    public int? HuevoDeforme { get; set; }
    public int? HuevoBlanco { get; set; }
    public int? HuevoDobleYema { get; set; }
    public int? HuevoPiso { get; set; }
    public int? HuevoPequeno { get; set; }
    public int? HuevoRoto { get; set; }
    public int? HuevoDesecho { get; set; }
    public int? HuevoOtro { get; set; }
    
    // IDs de alimentos (opcionales, para validación de inventario)
    public int? TipoAlimentoHembras { get; set; }
    public int? TipoAlimentoMachos { get; set; }
    
    // Tipo de ítem (alimento, medicamento, etc.) - se guarda en Metadata
    public string? TipoItemHembras { get; set; }
    public string? TipoItemMachos { get; set; }
    
    public string TipoAlimento { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public decimal PesoHuevo { get; set; }
    public int Etapa { get; set; }
    
    // Campos de Pesaje Semanal (registro una vez por semana)
    public decimal? PesoH { get; set; }
    public decimal? PesoM { get; set; }
    public decimal? Uniformidad { get; set; }
    public decimal? CoeficienteVariacion { get; set; }
    public string? ObservacionesPesaje { get; set; }
    
    /// <summary>
    /// Convierte este request a CreateSeguimientoProduccionDto, haciendo la conversión de unidades si es necesario.
    /// </summary>
    public CreateSeguimientoProduccionDto ToDto()
    {
        // Convertir consumo a kg si viene en gramos
        decimal consumoKgH = 0;
        if (ConsumoH.HasValue && ConsumoH.Value > 0)
        {
            var unidadH = (UnidadConsumoH ?? "kg").ToLower().Trim();
            if (unidadH == "g" || unidadH == "gramos" || unidadH == "gramo")
            {
                consumoKgH = (decimal)(ConsumoH.Value / 1000.0); // Convertir gramos a kg
            }
            else
            {
                consumoKgH = (decimal)ConsumoH.Value; // Ya está en kg
            }
        }
        
        decimal consumoKgM = 0;
        if (ConsumoM.HasValue && ConsumoM.Value > 0)
        {
            var unidadM = (UnidadConsumoM ?? "kg").ToLower().Trim();
            if (unidadM == "g" || unidadM == "gramos" || unidadM == "gramo")
            {
                consumoKgM = (decimal)(ConsumoM.Value / 1000.0); // Convertir gramos a kg
            }
            else
            {
                consumoKgM = (decimal)ConsumoM.Value; // Ya está en kg
            }
        }
        
        return new CreateSeguimientoProduccionDto(
            Fecha: Fecha,
            LoteId: LoteId,
            MortalidadH: MortalidadH,
            MortalidadM: MortalidadM,
            SelH: SelH,
            SelM: SelM,
            ConsKgH: consumoKgH,
            ConsKgM: consumoKgM,
            HuevoTot: HuevoTot,
            HuevoInc: HuevoInc,
            TipoAlimento: TipoAlimento,
            Observaciones: Observaciones,
            PesoHuevo: PesoHuevo,
            Etapa: Etapa,
            // Metadata JSONB con consumo original, tipo de ítem y otros campos adicionales
            Metadata: BuildMetadata(ConsumoH, UnidadConsumoH, ConsumoM, UnidadConsumoM, 
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





