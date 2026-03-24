using System.Text.Json;
using System.Text.Json.Serialization;

// file: backend/src/ZooSanMarino.Application/DTOs/CreateSeguimientoLoteLevanteRequest.cs
namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Representa un ítem individual en el seguimiento (alimento, vacuna, medicamento, etc.)
/// </summary>
public class ItemSeguimientoDto
{
    [JsonPropertyName("tipoItem")]
    public string TipoItem { get; set; } = string.Empty; // "alimento", "vacuna", "medicamento", etc.
    [JsonPropertyName("catalogItemId")]
    public int CatalogItemId { get; set; } // ID del ítem del inventario (catálogo legacy)
    /// <summary>ID de item_inventario_ecuador (Ecuador/Panamá). Cuando está presente, se aplica consumo en inventario-gestion.</summary>
    [JsonPropertyName("itemInventarioEcuadorId")]
    public int? ItemInventarioEcuadorId { get; set; }
    [JsonPropertyName("cantidad")]
    public double Cantidad { get; set; } // Cantidad utilizada
    [JsonPropertyName("unidad")]
    public string Unidad { get; set; } = "kg"; // "kg", "g", "unidades", etc.
}

/// <summary>
/// Request DTO para crear/actualizar seguimiento diario de lote LEVANTE únicamente.
/// No usar en Seguimiento Diario Reproductora Aves de Engorde (ese módulo usa CreateSeguimientoDiarioLoteReproductoraRequest).
/// Permite consumo en kg o gramos; soporta múltiples ítems y campos de agua (Ecuador/Panamá).
/// </summary>
public class CreateSeguimientoLoteLevanteRequest
{
    public int LoteId { get; set; }
    /// <summary>ID de lote_postura_levante. Solo aplica para seguimiento tipo levante.</summary>
    public int? LotePosturaLevanteId { get; set; }
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
    // Compatibilidad payload frontend actual (envía consumoKgHembras/consumoKgMachos)
    [JsonPropertyName("consumoKgHembras")]
    public double? ConsumoKgHembrasDirecto { get; set; }
    [JsonPropertyName("consumoKgMachos")]
    public double? ConsumoKgMachosDirecto { get; set; }
    
    // IDs de alimentos (opcionales, para validación de inventario)
    public int? TipoAlimentoHembras { get; set; }
    public int? TipoAlimentoMachos { get; set; }
    
    // Tipo de ítem (alimento, medicamento, etc.) - se guarda en Metadata
    // DEPRECATED: Usar ItemsHembras e ItemsMachos en su lugar
    public string? TipoItemHembras { get; set; }
    public string? TipoItemMachos { get; set; }
    
    // NUEVO: Arrays de ítems para permitir múltiples ítems por género
    [JsonPropertyName("itemsHembras")]
    public List<ItemSeguimientoDto>? ItemsHembras { get; set; }
    [JsonPropertyName("itemsMachos")]
    public List<ItemSeguimientoDto>? ItemsMachos { get; set; }
    
    // Cantidad de unidades (para tipos de ítem que no sean alimento) - DEPRECATED
    public double? CantidadUnidadesHembras { get; set; }
    public double? CantidadUnidadesMachos { get; set; }
    
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
    
    // Campos de agua (solo para Ecuador y Panamá) — explícito para binding JSON camelCase
    [JsonPropertyName("consumoAguaDiario")]
    public double? ConsumoAguaDiario { get; set; } // Consumo diario de agua en litros
    [JsonPropertyName("consumoAguaPh")]
    public double? ConsumoAguaPh { get; set; } // Nivel de PH del agua (0-14)
    [JsonPropertyName("consumoAguaOrp")]
    public double? ConsumoAguaOrp { get; set; } // Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV
    [JsonPropertyName("consumoAguaTemperatura")]
    public double? ConsumoAguaTemperatura { get; set; } // Temperatura del agua en °C

    /// <summary>
    /// ID del usuario que crea el registro (desde storage/token en el frontend). Opcional; si no se envía, el backend usa el usuario actual.
    /// </summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Convierte este request a SeguimientoLoteLevanteDto, haciendo la conversión de unidades si es necesario.
    /// Separa los alimentos (que van a campos tradicionales) de otros ítems (que van a ItemsAdicionales).
    /// </summary>
    public SeguimientoLoteLevanteDto ToDto(int? id = null)
    {
        // Separar alimentos de otros ítems
        var (alimentosHembras, otrosItemsHembras) = SepararAlimentosYOtrosItems(ItemsHembras);
        var (alimentosMachos, otrosItemsMachos) = SepararAlimentosYOtrosItems(ItemsMachos);
        
        // Calcular consumo total de alimentos desde los arrays de ítems
        double consumoKgHembras = CalcularConsumoTotalAlimentos(alimentosHembras);
        double? consumoKgMachos = CalcularConsumoTotalAlimentos(alimentosMachos);
        
        // Si no hay ítems pero hay consumo directo (compatibilidad hacia atrás)
        if (consumoKgHembras == 0 && ConsumoHembras.HasValue && ConsumoHembras.Value > 0)
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
        
        if (!consumoKgMachos.HasValue && ConsumoMachos.HasValue && ConsumoMachos.Value > 0)
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

        // Fallback directo para frontend que envía consumoKg* en lugar de consumo* con unidad.
        if (consumoKgHembras == 0 && ConsumoKgHembrasDirecto.HasValue && ConsumoKgHembrasDirecto.Value > 0)
            consumoKgHembras = ConsumoKgHembrasDirecto.Value;
        if ((!consumoKgMachos.HasValue || consumoKgMachos.Value <= 0) && ConsumoKgMachosDirecto.HasValue && ConsumoKgMachosDirecto.Value > 0)
            consumoKgMachos = ConsumoKgMachosDirecto.Value;
        
        // Construir TipoAlimento concatenando nombres de alimentos
        string tipoAlimentoStr = TipoAlimento ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tipoAlimentoStr))
        {
            tipoAlimentoStr = ConstruirTipoAlimentoString(alimentosHembras, alimentosMachos);
        }
        
        // Construir ItemsAdicionales JSONB solo con ítems que NO son alimentos
        JsonDocument? itemsAdicionales = BuildItemsAdicionales(otrosItemsHembras, otrosItemsMachos);
        
        return new SeguimientoLoteLevanteDto(
            Id: id ?? 0,
            LoteId: LoteId,
            LotePosturaLevanteId: LotePosturaLevanteId,
            FechaRegistro: FechaRegistro,
            MortalidadHembras: MortalidadHembras,
            MortalidadMachos: MortalidadMachos,
            SelH: SelH,
            SelM: SelM,
            ErrorSexajeHembras: ErrorSexajeHembras,
            ErrorSexajeMachos: ErrorSexajeMachos,
            ConsumoKgHembras: consumoKgHembras,
            TipoAlimento: tipoAlimentoStr,
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
            Metadata: BuildMetadata(ItemsHembras, ItemsMachos, ConsumoHembras, UnidadConsumoHembras, 
                                   ConsumoMachos, UnidadConsumoMachos, TipoItemHembras, TipoItemMachos, 
                                   TipoAlimentoHembras, TipoAlimentoMachos, CantidadUnidadesHembras, CantidadUnidadesMachos),
            // Items adicionales JSONB solo para ítems que NO son alimentos
            ItemsAdicionales: itemsAdicionales,
            // Campos de agua (solo para Ecuador y Panamá)
            // NOTA: Ya son double?, no necesitan conversión
            ConsumoAguaDiario: ConsumoAguaDiario,
            ConsumoAguaPh: ConsumoAguaPh,
            ConsumoAguaOrp: ConsumoAguaOrp,
            ConsumoAguaTemperatura: ConsumoAguaTemperatura,
            CreatedByUserId: CreatedByUserId
        );
    }
    
    /// <summary>
    /// Separa los ítems en dos listas: alimentos y otros ítems.
    /// </summary>
    private static (List<ItemSeguimientoDto> alimentos, List<ItemSeguimientoDto> otrosItems) SepararAlimentosYOtrosItems(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0)
        {
            return (new List<ItemSeguimientoDto>(), new List<ItemSeguimientoDto>());
        }
        
        var alimentos = new List<ItemSeguimientoDto>();
        var otrosItems = new List<ItemSeguimientoDto>();
        
        foreach (var item in items)
        {
            if (item.TipoItem?.ToLower().Trim() == "alimento")
            {
                alimentos.Add(item);
            }
            else
            {
                otrosItems.Add(item);
            }
        }
        
        return (alimentos, otrosItems);
    }
    
    /// <summary>
    /// Construye el JSONB de ItemsAdicionales solo con ítems que NO son alimentos.
    /// </summary>
    private static JsonDocument? BuildItemsAdicionales(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var itemsAdicionales = new Dictionary<string, object?>();
        
        if (itemsHembras != null && itemsHembras.Count > 0)
        {
            itemsAdicionales["itemsHembras"] = itemsHembras.Select(i => new
            {
                tipoItem = i.TipoItem,
                catalogItemId = i.CatalogItemId,
                cantidad = i.Cantidad,
                unidad = i.Unidad
            }).ToList();
        }
        
        if (itemsMachos != null && itemsMachos.Count > 0)
        {
            itemsAdicionales["itemsMachos"] = itemsMachos.Select(i => new
            {
                tipoItem = i.TipoItem,
                catalogItemId = i.CatalogItemId,
                cantidad = i.Cantidad,
                unidad = i.Unidad
            }).ToList();
        }
        
        if (itemsAdicionales.Count == 0) return null;
        
        return JsonDocument.Parse(JsonSerializer.Serialize(itemsAdicionales));
    }
    
    /// <summary>
    /// Calcula el consumo total de alimentos (en kg) desde un array de ítems.
    /// Solo suma los ítems de tipo "alimento".
    /// </summary>
    private static double CalcularConsumoTotalAlimentos(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0) return 0;
        
        double total = 0;
        foreach (var item in items)
        {
            if (item.TipoItem?.ToLower().Trim() == "alimento")
            {
                var unidad = item.Unidad?.ToLower().Trim() ?? "kg";
                double cantidadKg = item.Cantidad;
                
                if (unidad == "g" || unidad == "gramos" || unidad == "gramo")
                {
                    cantidadKg = item.Cantidad / 1000.0; // Convertir gramos a kg
                }
                
                total += cantidadKg;
            }
        }
        
        return total;
    }
    
    /// <summary>
    /// Construye el string de TipoAlimento concatenando los nombres de los alimentos.
    /// </summary>
    private static string ConstruirTipoAlimentoString(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var nombres = new List<string>();
        
        if (itemsHembras != null)
        {
            foreach (var item in itemsHembras)
            {
                if (item.TipoItem?.ToLower().Trim() == "alimento")
                {
                    nombres.Add($"H:{item.CatalogItemId}");
                }
            }
        }
        
        if (itemsMachos != null)
        {
            foreach (var item in itemsMachos)
            {
                if (item.TipoItem?.ToLower().Trim() == "alimento")
                {
                    nombres.Add($"M:{item.CatalogItemId}");
                }
            }
        }
        
        return nombres.Count > 0 ? string.Join(" / ", nombres) : string.Empty;
    }
    
    /// <summary>
    /// Construye el objeto Metadata JSONB con los campos adicionales.
    /// AHORA: Guarda TODOS los items (incluyendo alimentos) en metadata para poder cargarlos al editar.
    /// </summary>
    private static JsonDocument? BuildMetadata(
        List<ItemSeguimientoDto>? itemsHembras,
        List<ItemSeguimientoDto>? itemsMachos,
        double? consumoHembras, string? unidadHembras, 
        double? consumoMachos, string? unidadMachos,
        string? tipoItemHembras, string? tipoItemMachos,
        int? tipoAlimentoHembras, int? tipoAlimentoMachos,
        double? cantidadUnidadesHembras, double? cantidadUnidadesMachos)
    {
        var metadata = new Dictionary<string, object?>();
        
        // NUEVO: Guardar TODOS los items en metadata (incluyendo alimentos) para poder cargarlos al editar
        if (itemsHembras != null && itemsHembras.Count > 0)
        {
            metadata["itemsHembras"] = itemsHembras.Select(i => new
            {
                tipoItem = i.TipoItem,
                catalogItemId = i.CatalogItemId,
                itemInventarioEcuadorId = i.ItemInventarioEcuadorId,
                cantidad = i.Cantidad,
                unidad = i.Unidad
            }).ToList();
        }
        
        if (itemsMachos != null && itemsMachos.Count > 0)
        {
            metadata["itemsMachos"] = itemsMachos.Select(i => new
            {
                tipoItem = i.TipoItem,
                catalogItemId = i.CatalogItemId,
                itemInventarioEcuadorId = i.ItemInventarioEcuadorId,
                cantidad = i.Cantidad,
                unidad = i.Unidad
            }).ToList();
        }
        
        // COMPATIBILIDAD HACIA ATRÁS: Mantener campos antiguos si no hay arrays
        if ((itemsHembras == null || itemsHembras.Count == 0) && consumoHembras.HasValue)
        {
            metadata["consumoOriginalHembras"] = consumoHembras.Value;
            metadata["unidadConsumoOriginalHembras"] = unidadHembras ?? "kg";
        }
        
        if ((itemsMachos == null || itemsMachos.Count == 0) && consumoMachos.HasValue)
        {
            metadata["consumoOriginalMachos"] = consumoMachos.Value;
            metadata["unidadConsumoOriginalMachos"] = unidadMachos ?? "kg";
        }
        
        // Tipo de ítem (compatibilidad hacia atrás)
        if (!string.IsNullOrWhiteSpace(tipoItemHembras))
        {
            metadata["tipoItemHembras"] = tipoItemHembras;
        }
        
        if (!string.IsNullOrWhiteSpace(tipoItemMachos))
        {
            metadata["tipoItemMachos"] = tipoItemMachos;
        }
        
        // IDs de alimentos seleccionados (compatibilidad hacia atrás)
        if (tipoAlimentoHembras.HasValue)
        {
            metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        }
        
        if (tipoAlimentoMachos.HasValue)
        {
            metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        }
        
        // Cantidad de unidades (compatibilidad hacia atrás)
        if (cantidadUnidadesHembras.HasValue)
        {
            metadata["cantidadUnidadesHembras"] = cantidadUnidadesHembras.Value;
        }
        
        if (cantidadUnidadesMachos.HasValue)
        {
            metadata["cantidadUnidadesMachos"] = cantidadUnidadesMachos.Value;
        }
        
        if (metadata.Count == 0) return null;
        
        return JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }
}

