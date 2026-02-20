using System.Text.Json;
using System.Text.Json.Serialization;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Request DTO exclusivo para crear/actualizar seguimiento diario de lote reproductora aves de engorde.
/// Persiste en tabla seguimiento_diario_lote_reproductora_aves_engorde.
/// LoteId = lote_reproductora_ave_engorde_id.
/// No comparte tipo con Seguimiento diario levante (CreateSeguimientoLoteLevanteRequest).
/// Incluye campos de agua (Ecuador/Panamá) y múltiples ítems (alimento, vacuna, medicamento).
/// </summary>
public class CreateSeguimientoDiarioLoteReproductoraRequest
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

    public double? ConsumoHembras { get; set; }
    public string? UnidadConsumoHembras { get; set; }
    public double? ConsumoMachos { get; set; }
    public string? UnidadConsumoMachos { get; set; }

    public int? TipoAlimentoHembras { get; set; }
    public int? TipoAlimentoMachos { get; set; }

    public string? TipoItemHembras { get; set; }
    public string? TipoItemMachos { get; set; }

    public List<ItemSeguimientoDto>? ItemsHembras { get; set; }
    public List<ItemSeguimientoDto>? ItemsMachos { get; set; }

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

    [JsonPropertyName("consumoAguaDiario")]
    public double? ConsumoAguaDiario { get; set; }

    [JsonPropertyName("consumoAguaPh")]
    public double? ConsumoAguaPh { get; set; }

    [JsonPropertyName("consumoAguaOrp")]
    public double? ConsumoAguaOrp { get; set; }

    [JsonPropertyName("consumoAguaTemperatura")]
    public double? ConsumoAguaTemperatura { get; set; }

    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Convierte este request a SeguimientoLoteLevanteDto para el servicio (que persiste en seguimiento_diario_lote_reproductora_aves_engorde).
    /// </summary>
    public SeguimientoLoteLevanteDto ToDto(int? id = null)
    {
        var (alimentosHembras, otrosItemsHembras) = SepararAlimentosYOtrosItems(ItemsHembras);
        var (alimentosMachos, otrosItemsMachos) = SepararAlimentosYOtrosItems(ItemsMachos);

        double consumoKgHembras = CalcularConsumoTotalAlimentos(alimentosHembras);
        double? consumoKgMachos = CalcularConsumoTotalAlimentos(alimentosMachos);

        if (consumoKgHembras == 0 && ConsumoHembras.HasValue && ConsumoHembras.Value > 0)
        {
            var unidadH = (UnidadConsumoHembras ?? "kg").ToLower().Trim();
            consumoKgHembras = unidadH == "g" || unidadH == "gramos" || unidadH == "gramo"
                ? ConsumoHembras.Value / 1000.0
                : ConsumoHembras.Value;
        }

        if (!consumoKgMachos.HasValue && ConsumoMachos.HasValue && ConsumoMachos.Value > 0)
        {
            var unidadM = (UnidadConsumoMachos ?? "kg").ToLower().Trim();
            consumoKgMachos = unidadM == "g" || unidadM == "gramos" || unidadM == "gramo"
                ? ConsumoMachos.Value / 1000.0
                : ConsumoMachos.Value;
        }

        string tipoAlimentoStr = TipoAlimento ?? string.Empty;
        if (string.IsNullOrWhiteSpace(tipoAlimentoStr))
            tipoAlimentoStr = ConstruirTipoAlimentoString(alimentosHembras, alimentosMachos);

        JsonDocument? itemsAdicionales = BuildItemsAdicionales(otrosItemsHembras, otrosItemsMachos);

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
            Metadata: BuildMetadata(ItemsHembras, ItemsMachos, ConsumoHembras, UnidadConsumoHembras,
                ConsumoMachos, UnidadConsumoMachos, TipoItemHembras, TipoItemMachos,
                TipoAlimentoHembras, TipoAlimentoMachos, CantidadUnidadesHembras, CantidadUnidadesMachos),
            ItemsAdicionales: itemsAdicionales,
            ConsumoAguaDiario: ConsumoAguaDiario,
            ConsumoAguaPh: ConsumoAguaPh,
            ConsumoAguaOrp: ConsumoAguaOrp,
            ConsumoAguaTemperatura: ConsumoAguaTemperatura,
            CreatedByUserId: CreatedByUserId
        );
    }

    private static (List<ItemSeguimientoDto> alimentos, List<ItemSeguimientoDto> otrosItems) SepararAlimentosYOtrosItems(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0)
            return (new List<ItemSeguimientoDto>(), new List<ItemSeguimientoDto>());

        var alimentos = new List<ItemSeguimientoDto>();
        var otrosItems = new List<ItemSeguimientoDto>();
        foreach (var item in items)
        {
            if (item.TipoItem?.ToLower().Trim() == "alimento")
                alimentos.Add(item);
            else
                otrosItems.Add(item);
        }
        return (alimentos, otrosItems);
    }

    private static JsonDocument? BuildItemsAdicionales(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var itemsAdicionales = new Dictionary<string, object?>();
        if (itemsHembras != null && itemsHembras.Count > 0)
            itemsAdicionales["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            itemsAdicionales["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        return itemsAdicionales.Count == 0 ? null : JsonDocument.Parse(JsonSerializer.Serialize(itemsAdicionales));
    }

    private static double CalcularConsumoTotalAlimentos(List<ItemSeguimientoDto>? items)
    {
        if (items == null || items.Count == 0) return 0;
        double total = 0;
        foreach (var item in items)
        {
            if (item.TipoItem?.ToLower().Trim() != "alimento") continue;
            var unidad = item.Unidad?.ToLower().Trim() ?? "kg";
            double cantidadKg = unidad == "g" || unidad == "gramos" || unidad == "gramo" ? item.Cantidad / 1000.0 : item.Cantidad;
            total += cantidadKg;
        }
        return total;
    }

    private static string ConstruirTipoAlimentoString(List<ItemSeguimientoDto>? itemsHembras, List<ItemSeguimientoDto>? itemsMachos)
    {
        var nombres = new List<string>();
        if (itemsHembras != null)
            foreach (var item in itemsHembras)
                if (item.TipoItem?.ToLower().Trim() == "alimento")
                    nombres.Add($"H:{item.CatalogItemId}");
        if (itemsMachos != null)
            foreach (var item in itemsMachos)
                if (item.TipoItem?.ToLower().Trim() == "alimento")
                    nombres.Add($"M:{item.CatalogItemId}");
        return nombres.Count > 0 ? string.Join(" / ", nombres) : string.Empty;
    }

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
        if (itemsHembras != null && itemsHembras.Count > 0)
            metadata["itemsHembras"] = itemsHembras.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
        if (itemsMachos != null && itemsMachos.Count > 0)
            metadata["itemsMachos"] = itemsMachos.Select(i => new { tipoItem = i.TipoItem, catalogItemId = i.CatalogItemId, cantidad = i.Cantidad, unidad = i.Unidad }).ToList();
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
        if (!string.IsNullOrWhiteSpace(tipoItemHembras)) metadata["tipoItemHembras"] = tipoItemHembras;
        if (!string.IsNullOrWhiteSpace(tipoItemMachos)) metadata["tipoItemMachos"] = tipoItemMachos;
        if (tipoAlimentoHembras.HasValue) metadata["tipoAlimentoHembras"] = tipoAlimentoHembras.Value;
        if (tipoAlimentoMachos.HasValue) metadata["tipoAlimentoMachos"] = tipoAlimentoMachos.Value;
        if (cantidadUnidadesHembras.HasValue) metadata["cantidadUnidadesHembras"] = cantidadUnidadesHembras.Value;
        if (cantidadUnidadesMachos.HasValue) metadata["cantidadUnidadesMachos"] = cantidadUnidadesMachos.Value;
        return metadata.Count == 0 ? null : JsonDocument.Parse(JsonSerializer.Serialize(metadata));
    }
}
