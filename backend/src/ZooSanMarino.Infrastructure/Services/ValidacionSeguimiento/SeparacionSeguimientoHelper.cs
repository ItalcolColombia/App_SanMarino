// src/ZooSanMarino.Infrastructure/Services/ValidacionSeguimiento/SeparacionSeguimientoHelper.cs
using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.Interfaces;

namespace ZooSanMarino.Infrastructure.Services;

/// <summary>
/// Lo que los cinco Crud de seguimiento necesitan para engancharse a la doble validación, en un solo
/// lugar.
///
/// <para>
/// La alternativa era repetir el mismo bloque de diez líneas en levante, producción, los dos engordes
/// y reproductora. Cinco copias del mismo criterio son cinco oportunidades de que una se olvide de
/// algo — que es exactamente cómo el descuento de aves terminó divergiendo entre módulos antes de A7.
/// </para>
/// </summary>
public static class SeparacionSeguimientoHelper
{
    /// <summary>
    /// Exige que el registro traiga alimento en el bloque que corresponde, y lanza con el motivo
    /// redactado si no.
    ///
    /// <para>
    /// Se valida en el backend además del formulario porque la carga masiva por Excel y el push de la
    /// PWA entran por este mismo service sin pasar por la pantalla.
    /// </para>
    /// </summary>
    public static void ValidarAlimentoObligatorio(
        string modulo, bool loteEsMixto, JsonDocument? metadata, DateTime fechaRegistro)
    {
        var (h, m, g) = metadata is null
            ? (0m, 0m, 0m)
            : MetadataEngordeCalculos.ParseKgPorBloque(metadata.RootElement);

        var motivo = AlimentoObligatorioCalculos.Motivo(
            modulo, loteEsMixto, new AlimentoCapturado(h, m, g), DateOnly.FromDateTime(fechaRegistro));

        if (motivo is not null)
            throw new InvalidOperationException(motivo);
    }

    /// <summary>
    /// Arma el contexto de separación desde lo que ya tiene a mano cada Crud.
    /// </summary>
    /// <param name="modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
    /// <param name="seguimientoId">Registro recién persistido.</param>
    /// <param name="paisId">País del LOTE — decide el modelo de inventario al aplicar.</param>
    /// <param name="loteRefInt">Clave del lote contra la que se descuentan las aves.</param>
    /// <param name="loteEsMixto">Si el saldo del lote no está sexado.</param>
    public static SeparacionSeguimientoContexto Contexto(
        string modulo, long seguimientoId, int? paisId,
        int farmId, string? nucleoId, string? galponId,
        int loteRefInt, string? loteRef, DateTime fechaRegistro,
        JsonDocument? metadata,
        int mortalidadHembras, int selHembras, int errorSexajeHembras,
        int mortalidadMachos, int selMachos, int errorSexajeMachos,
        bool loteEsMixto)
    {
        var consumo = metadata is null
            ? new Dictionary<ItemConsumoKey, decimal>()
            : MetadataEngordeCalculos.ParseMetadataItemsToKgPorOrigen(metadata.RootElement);

        var aves = ReservaSeguimientoCalculos.LineasDeAves(
            mortalidadHembras, selHembras, errorSexajeHembras,
            mortalidadMachos, selMachos, errorSexajeMachos,
            loteEsMixto);

        return new SeparacionSeguimientoContexto(
            Modulo: modulo,
            SeguimientoId: seguimientoId,
            PaisId: paisId ?? 0,
            FarmId: farmId,
            NucleoId: nucleoId,
            GalponId: galponId,
            LoteRefInt: loteRefInt,
            LoteRef: loteRef,
            FechaSeguimiento: DateOnly.FromDateTime(fechaRegistro),
            ConsumoPorItem: consumo,
            Aves: aves);
    }
}
