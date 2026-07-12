// Mapeos DTO Levante ↔ unificado (seguimiento_diario) y construcción de metadata sintético para
// registros legacy sin JSON en BD. Partial de SeguimientoLoteLevanteService (namespace plano).
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SeguimientoLoteLevanteService
{
    private static SeguimientoLoteLevanteDto MapToLevanteDto(SeguimientoDiarioDto u)
    {
        return new SeguimientoLoteLevanteDto(
            Id: (int)u.Id,
            LoteId: int.Parse(u.LoteId),
            LotePosturaLevanteId: u.LotePosturaLevanteId,
            FechaRegistro: u.Fecha,
            MortalidadHembras: u.MortalidadHembras ?? 0,
            MortalidadMachos: u.MortalidadMachos ?? 0,
            SelH: u.SelH ?? 0,
            SelM: u.SelM ?? 0,
            ErrorSexajeHembras: u.ErrorSexajeHembras ?? 0,
            ErrorSexajeMachos: u.ErrorSexajeMachos ?? 0,
            ConsumoKgHembras: (double)(u.ConsumoKgHembras ?? 0),
            TipoAlimento: u.TipoAlimento ?? "",
            TipoAlimentoHembrasNombre: u.TipoAlimentoHembrasNombre,
            TipoAlimentoMachosNombre: u.TipoAlimentoMachosNombre,
            Observaciones: u.Observaciones,
            KcalAlH: u.KcalAlH,
            ProtAlH: u.ProtAlH,
            KcalAveH: u.KcalAveH,
            ProtAveH: u.ProtAveH,
            Ciclo: u.Ciclo ?? "Normal",
            ConsumoKgMachos: u.ConsumoKgMachos.HasValue ? (double)u.ConsumoKgMachos.Value : null,
            PesoPromH: u.PesoPromHembras,
            PesoPromM: u.PesoPromMachos,
            UniformidadH: u.UniformidadHembras,
            UniformidadM: u.UniformidadMachos,
            CvH: u.CvHembras,
            CvM: u.CvMachos,
            Metadata: u.Metadata,
            ItemsAdicionales: u.ItemsAdicionales,
            ConsumoAguaDiario: u.ConsumoAguaDiario,
            ConsumoAguaPh: u.ConsumoAguaPh,
            ConsumoAguaOrp: u.ConsumoAguaOrp,
            ConsumoAguaTemperatura: u.ConsumoAguaTemperatura,
            SaldoAlimentoKg: null,
            // Feature 13 — propagar marcado de traslado al frontend
            EsTraslado: u.EsTraslado,
            TrasladoDireccion: u.TrasladoDireccion,
            TrasladoLoteContraparteId: u.TrasladoLoteContraparteId,
            TrasladoGranjaContraparteId: u.TrasladoGranjaContraparteId,
            // Splits H/M dedicados (separados de mortalidad)
            TrasladoIngresoHembras: u.TrasladoIngresoHembras,
            TrasladoIngresoMachos: u.TrasladoIngresoMachos,
            TrasladoSalidaHembras: u.TrasladoSalidaHembras,
            TrasladoSalidaMachos: u.TrasladoSalidaMachos,
            // Auditoría
            UpdatedByUserId: u.UpdatedByUserId,
            CreatedAt: u.CreatedAt,
            UpdatedAt: u.UpdatedAt
        );
    }

    private static CreateSeguimientoDiarioDto MapToCreateUnificado(SeguimientoLoteLevanteDto dto,
        double consumoKgHembras, double? kcalAlH, double? protAlH, double? kcalAveH, double? protAveH)
    {
        return new CreateSeguimientoDiarioDto(
            TipoSeguimiento: TipoLevante,
            LoteId: dto.LoteId.ToString(),
            LotePosturaLevanteId: dto.LotePosturaLevanteId,
            LotePosturaProduccionId: null,
            ReproductoraId: null,
            Fecha: dto.FechaRegistro,
            MortalidadHembras: dto.MortalidadHembras,
            MortalidadMachos: dto.MortalidadMachos,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorSexajeHembras,
            ErrorSexajeMachos: dto.ErrorSexajeMachos,
            ConsumoKgHembras: (decimal)consumoKgHembras,
            ConsumoKgMachos: dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo,
            PesoPromHembras: dto.PesoPromH,
            PesoPromMachos: dto.PesoPromM,
            UniformidadHembras: dto.UniformidadH,
            UniformidadMachos: dto.UniformidadM,
            CvHembras: dto.CvH,
            CvMachos: dto.CvM,
            ConsumoAguaDiario: dto.ConsumoAguaDiario,
            ConsumoAguaPh: dto.ConsumoAguaPh,
            ConsumoAguaOrp: dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura: dto.ConsumoAguaTemperatura,
            Metadata: dto.Metadata,
            ItemsAdicionales: dto.ItemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: kcalAlH,
            ProtAlH: protAlH,
            KcalAveH: kcalAveH,
            ProtAveH: protAveH,
            HuevoTot: null,
            HuevoInc: null,
            HuevoLimpio: null,
            HuevoTratado: null,
            HuevoSucio: null,
            HuevoDeforme: null,
            HuevoBlanco: null,
            HuevoDobleYema: null,
            HuevoPiso: null,
            HuevoPequeno: null,
            HuevoRoto: null,
            HuevoDesecho: null,
            HuevoOtro: null,
            PesoHuevo: null,
            Etapa: null,
            PesoH: null,
            PesoM: null,
            Uniformidad: null,
            CoeficienteVariacion: null,
            ObservacionesPesaje: null,
            TrasladoAvesEntrante: null,
            TrasladoAvesSalida: null,
            VentaAvesCantidad: null,
            VentaAvesMotivo: null,
            CreatedByUserId: dto.CreatedByUserId,
            TipoAlimentoHembrasNombre: dto.TipoAlimentoHembrasNombre,
            TipoAlimentoMachosNombre: dto.TipoAlimentoMachosNombre
        );
    }

    private static UpdateSeguimientoDiarioDto MapToUpdateUnificado(SeguimientoLoteLevanteDto dto,
        double consumoKgHembras, double? kcalAlH, double? protAlH, double? kcalAveH, double? protAveH)
    {
        return new UpdateSeguimientoDiarioDto(
            Id: (long)dto.Id,
            TipoSeguimiento: TipoLevante,
            LoteId: dto.LoteId.ToString(),
            LotePosturaLevanteId: dto.LotePosturaLevanteId,
            LotePosturaProduccionId: null,
            ReproductoraId: null,
            Fecha: dto.FechaRegistro,
            MortalidadHembras: dto.MortalidadHembras,
            MortalidadMachos: dto.MortalidadMachos,
            SelH: dto.SelH,
            SelM: dto.SelM,
            ErrorSexajeHembras: dto.ErrorSexajeHembras,
            ErrorSexajeMachos: dto.ErrorSexajeMachos,
            ConsumoKgHembras: (decimal)consumoKgHembras,
            ConsumoKgMachos: dto.ConsumoKgMachos.HasValue ? (decimal)dto.ConsumoKgMachos.Value : null,
            TipoAlimento: dto.TipoAlimento,
            Observaciones: dto.Observaciones,
            Ciclo: dto.Ciclo,
            PesoPromHembras: dto.PesoPromH,
            PesoPromMachos: dto.PesoPromM,
            UniformidadHembras: dto.UniformidadH,
            UniformidadMachos: dto.UniformidadM,
            CvHembras: dto.CvH,
            CvMachos: dto.CvM,
            ConsumoAguaDiario: dto.ConsumoAguaDiario,
            ConsumoAguaPh: dto.ConsumoAguaPh,
            ConsumoAguaOrp: dto.ConsumoAguaOrp,
            ConsumoAguaTemperatura: dto.ConsumoAguaTemperatura,
            Metadata: dto.Metadata,
            ItemsAdicionales: dto.ItemsAdicionales,
            PesoInicial: null,
            PesoFinal: null,
            KcalAlH: kcalAlH,
            ProtAlH: protAlH,
            KcalAveH: kcalAveH,
            ProtAveH: protAveH,
            HuevoTot: null,
            HuevoInc: null,
            HuevoLimpio: null,
            HuevoTratado: null,
            HuevoSucio: null,
            HuevoDeforme: null,
            HuevoBlanco: null,
            HuevoDobleYema: null,
            HuevoPiso: null,
            HuevoPequeno: null,
            HuevoRoto: null,
            HuevoDesecho: null,
            HuevoOtro: null,
            PesoHuevo: null,
            Etapa: null,
            PesoH: null,
            PesoM: null,
            Uniformidad: null,
            CoeficienteVariacion: null,
            ObservacionesPesaje: null,
            TrasladoAvesEntrante: null,
            TrasladoAvesSalida: null,
            VentaAvesCantidad: null,
            VentaAvesMotivo: null,
            TipoAlimentoHembrasNombre: dto.TipoAlimentoHembrasNombre,
            TipoAlimentoMachosNombre: dto.TipoAlimentoMachosNombre
        );
    }

    /// <summary>
    /// Registros antiguos solo tenían tipo_alimento + consumo_kg_* en columnas; metadata en BD NULL.
    /// Construye un JSON compatible con el modal (itemsHembras/itemsMachos + consumo original) resolviendo
    /// el ítem de catálogo por código igual a <see cref="SeguimientoLoteLevanteDto.TipoAlimento"/>.
    /// </summary>
    private async Task<JsonDocument?> BuildSyntheticMetadataForLegacyRowAsync(SeguimientoLoteLevanteDto dto, CancellationToken ct)
    {
        var hasConsumo = dto.ConsumoKgHembras > 0 || (dto.ConsumoKgMachos ?? 0) > 0;
        var hasTipo = !string.IsNullOrWhiteSpace(dto.TipoAlimento);
        if (!hasConsumo && !hasTipo)
            return null;

        int? catalogId = null;
        string itemType = "alimento";
        if (hasTipo)
        {
            var code = dto.TipoAlimento!.Trim();
            var cat = await _ctx.CatalogItems.AsNoTracking()
                .Where(c => c.CompanyId == _current.CompanyId && c.Activo && c.Codigo == code)
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            cat ??= await _ctx.CatalogItems.AsNoTracking()
                .Where(c => c.CompanyId == _current.CompanyId && c.Activo && EF.Functions.ILike(c.Codigo, code))
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);
            if (cat != null)
            {
                catalogId = cat.Id;
                if (!string.IsNullOrWhiteSpace(cat.ItemType))
                    itemType = cat.ItemType.Trim();
            }
        }

        var root = new Dictionary<string, object?>();
        root["consumoOriginalHembras"] = dto.ConsumoKgHembras;
        root["unidadConsumoOriginalHembras"] = "kg";
        if (dto.ConsumoKgMachos.HasValue)
        {
            root["consumoOriginalMachos"] = dto.ConsumoKgMachos.Value;
            root["unidadConsumoOriginalMachos"] = "kg";
        }
        if (hasTipo)
            root["tipoAlimentoCodigo"] = dto.TipoAlimento!.Trim();
        root["syntheticLegacyMetadata"] = true;

        if (catalogId.HasValue)
        {
            root["tipoAlimentoHembras"] = catalogId.Value;
            root["tipoAlimentoMachos"] = catalogId.Value;
            root["tipoItemHembras"] = itemType;
            root["tipoItemMachos"] = itemType;

            if (dto.ConsumoKgHembras > 0)
            {
                root["itemsHembras"] = new[]
                {
                    new { tipoItem = itemType, catalogItemId = catalogId.Value, cantidad = dto.ConsumoKgHembras, unidad = "kg" }
                };
            }
            if (dto.ConsumoKgMachos is > 0)
            {
                root["itemsMachos"] = new[]
                {
                    new { tipoItem = itemType, catalogItemId = catalogId.Value, cantidad = dto.ConsumoKgMachos!.Value, unidad = "kg" }
                };
            }
        }

        return JsonSerializer.SerializeToDocument(root, SyntheticMetadataJsonOptions);
    }
}
