// Mapeos DTO Levante ↔ unificado (seguimiento_diario) y construcción de metadata sintético para
// registros legacy sin JSON en BD. Partial de SeguimientoLoteLevanteService (namespace plano).
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZooSanMarino.Application.Calculos;
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
            UpdatedAt: u.UpdatedAt,
            // Huevos en levante: se devuelven tal cual están en la tabla unificada para que el
            // modal pueda rehidratar el tab «Huevos» al editar.
            HuevoLimpio: u.HuevoLimpio,
            HuevoTratado: u.HuevoTratado,
            HuevoSucio: u.HuevoSucio,
            HuevoDeforme: u.HuevoDeforme,
            HuevoBlanco: u.HuevoBlanco,
            HuevoDobleYema: u.HuevoDobleYema,
            HuevoPiso: u.HuevoPiso,
            HuevoPequeno: u.HuevoPequeno,
            HuevoRoto: u.HuevoRoto,
            HuevoDesecho: u.HuevoDesecho,
            HuevoOtro: u.HuevoOtro,
            PesoHuevo: u.PesoHuevo,
            HuevoTot: u.HuevoTot,
            HuevoInc: u.HuevoInc,
            // Doble validación: el front necesita el estado para pintar la fila y ofrecer el ✓.
            // `EstadoValidacion` va nulo a propósito: el literal lo deriva el front con la función
            // compartida, que es la única que conoce el día del usuario.
            Validado: u.Validado,
            ValidadoAt: u.ValidadoAt,
            ValidadoPor: u.ValidadoPor
        );
    }

    /// <summary>
    /// Proyecta las 11 categorías del DTO de levante a <see cref="HuevosClasificacion"/>.
    /// Devuelve <c>null</c> si el cliente NO mandó ninguna categoría (todas null) — es la señal de
    /// «no tocar los huevos», distinta de «mandó ceros» (que sí los pone en 0).
    /// </summary>
    private static HuevosClasificacion? HuevosDeDto(SeguimientoLoteLevanteDto dto)
    {
        if (dto.HuevoLimpio is null && dto.HuevoTratado is null && dto.HuevoSucio is null &&
            dto.HuevoDeforme is null && dto.HuevoBlanco is null && dto.HuevoDobleYema is null &&
            dto.HuevoPiso is null && dto.HuevoPequeno is null && dto.HuevoRoto is null &&
            dto.HuevoDesecho is null && dto.HuevoOtro is null)
            return null;

        return new HuevosClasificacion(
            Limpio: dto.HuevoLimpio ?? 0,
            Tratado: dto.HuevoTratado ?? 0,
            Sucio: dto.HuevoSucio ?? 0,
            Deforme: dto.HuevoDeforme ?? 0,
            Blanco: dto.HuevoBlanco ?? 0,
            DobleYema: dto.HuevoDobleYema ?? 0,
            Piso: dto.HuevoPiso ?? 0,
            Pequeno: dto.HuevoPequeno ?? 0,
            Roto: dto.HuevoRoto ?? 0,
            Desecho: dto.HuevoDesecho ?? 0,
            Otro: dto.HuevoOtro ?? 0);
    }

    /// <summary>
    /// Si el request de edición NO trae ninguna categoría de huevos (todas null) pero el registro
    /// persistido SÍ tiene huevos, los arrastra al DTO para que el update no los borre.
    /// Si el request trae aunque sea una categoría, manda el request (incluidos los ceros: así se
    /// corrige/limpia un día cargado por error).
    /// </summary>
    private static SeguimientoLoteLevanteDto ConservarHuevosPrevios(
        SeguimientoLoteLevanteDto dto, SeguimientoDiarioDto? previo)
    {
        if (previo is null) return dto;
        if (HuevosDeDto(dto) is not null) return dto;

        var teniaHuevos =
            previo.HuevoLimpio.HasValue || previo.HuevoTratado.HasValue || previo.HuevoSucio.HasValue ||
            previo.HuevoDeforme.HasValue || previo.HuevoBlanco.HasValue || previo.HuevoDobleYema.HasValue ||
            previo.HuevoPiso.HasValue || previo.HuevoPequeno.HasValue || previo.HuevoRoto.HasValue ||
            previo.HuevoDesecho.HasValue || previo.HuevoOtro.HasValue;
        if (!teniaHuevos) return dto;

        return dto with
        {
            HuevoLimpio = previo.HuevoLimpio,
            HuevoTratado = previo.HuevoTratado,
            HuevoSucio = previo.HuevoSucio,
            HuevoDeforme = previo.HuevoDeforme,
            HuevoBlanco = previo.HuevoBlanco,
            HuevoDobleYema = previo.HuevoDobleYema,
            HuevoPiso = previo.HuevoPiso,
            HuevoPequeno = previo.HuevoPequeno,
            HuevoRoto = previo.HuevoRoto,
            HuevoDesecho = previo.HuevoDesecho,
            HuevoOtro = previo.HuevoOtro,
            PesoHuevo = dto.PesoHuevo ?? previo.PesoHuevo
        };
    }

    private static CreateSeguimientoDiarioDto MapToCreateUnificado(SeguimientoLoteLevanteDto dto,
        double consumoKgHembras, double? kcalAlH, double? protAlH, double? kcalAveH, double? protAveH)
    {
        var huevos = HuevosDeDto(dto);
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
            // El recorte defensivo vive en SeguimientoDiarioService, el único escritor de la tabla
            // unificada, para que cubra también a los otros llamadores. Ver TipoAlimentoCalculos.
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
            // Huevos en LEVANTE (semana 14+). El gate de Crud.cs ya neutralizó los valores si la
            // empresa no tiene el flag o el registro no llega a la semana 14, así que acá se mapean
            // tal cual. HuevoTot/HuevoInc SIEMPRE se derivan de las 11 categorías (nunca se confía
            // en lo que mandó el cliente) ⇒ imposible que queden descuadrados.
            HuevoTot: huevos?.Totales,
            HuevoInc: huevos?.Incubables,
            HuevoLimpio: dto.HuevoLimpio,
            HuevoTratado: dto.HuevoTratado,
            HuevoSucio: dto.HuevoSucio,
            HuevoDeforme: dto.HuevoDeforme,
            HuevoBlanco: dto.HuevoBlanco,
            HuevoDobleYema: dto.HuevoDobleYema,
            HuevoPiso: dto.HuevoPiso,
            HuevoPequeno: dto.HuevoPequeno,
            HuevoRoto: dto.HuevoRoto,
            HuevoDesecho: dto.HuevoDesecho,
            HuevoOtro: dto.HuevoOtro,
            PesoHuevo: dto.PesoHuevo,
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
        var huevos = HuevosDeDto(dto);
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
            // El recorte defensivo vive en SeguimientoDiarioService, el único escritor de la tabla
            // unificada, para que cubra también a los otros llamadores. Ver TipoAlimentoCalculos.
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
            // Huevos en LEVANTE (semana 14+). El gate de Crud.cs ya neutralizó los valores si la
            // empresa no tiene el flag o el registro no llega a la semana 14, así que acá se mapean
            // tal cual. HuevoTot/HuevoInc SIEMPRE se derivan de las 11 categorías (nunca se confía
            // en lo que mandó el cliente) ⇒ imposible que queden descuadrados.
            HuevoTot: huevos?.Totales,
            HuevoInc: huevos?.Incubables,
            HuevoLimpio: dto.HuevoLimpio,
            HuevoTratado: dto.HuevoTratado,
            HuevoSucio: dto.HuevoSucio,
            HuevoDeforme: dto.HuevoDeforme,
            HuevoBlanco: dto.HuevoBlanco,
            HuevoDobleYema: dto.HuevoDobleYema,
            HuevoPiso: dto.HuevoPiso,
            HuevoPequeno: dto.HuevoPequeno,
            HuevoRoto: dto.HuevoRoto,
            HuevoDesecho: dto.HuevoDesecho,
            HuevoOtro: dto.HuevoOtro,
            PesoHuevo: dto.PesoHuevo,
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
                .Where(c => c.CompanyId == _current.CompanyId && c.Activo && c.Codigo != null && EF.Functions.ILike(c.Codigo, code))
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
