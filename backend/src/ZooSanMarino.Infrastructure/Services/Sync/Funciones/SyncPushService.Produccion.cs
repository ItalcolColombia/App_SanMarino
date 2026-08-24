using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs.Produccion;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Exceptions;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// Alta de seguimiento diario de PRODUCCIÓN (la segunda etapa de postura; la primera es levante).
    ///
    /// Igual que en levante, llama al mismo service que usa el controller: una captura offline pasa
    /// exactamente por donde pasa una con red.
    ///
    /// ## El autor y la empresa ya los pone el servidor
    ///
    /// `ProduccionService` estampa `CompanyId` y `CreatedByUserId` desde el token, y busca el lote
    /// filtrando por la empresa de la sesión. Por eso una operación de otra empresa no puede escribir
    /// acá aunque se cuele: no encontraría el lote. La comprobación explícita de
    /// `SyncPushCalculos` existe para que el motivo del rechazo sea el real y no un "Lote no existe",
    /// que en campo se lee como un dato corrupto.
    /// </summary>
    private async Task<DespachoResultado> CrearSeguimientoProduccionAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        var request = payload.Deserialize<CrearSeguimientoRequest>(OpcionesJson)
                      ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");

        try
        {
            var id = await _produccion.CrearSeguimientoAsync(request);
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.Aplicado(id, JsonSerializer.Serialize(new { id }, OpcionesJson));
        }
        catch (StockInsuficienteException ex) when (TraeItems(request.ItemsHembras, request.ItemsMachos))
        {
            // Defensivo, igual que en levante/engorde: `ProduccionService.CrearSeguimientoAsync`
            // valida el stock antes de trackear la entidad en ESTE camino, pero limpiar acá no
            // rompe nada y evita que un cambio futuro en el orden reintroduzca el mismo bug.
            _ctx.ChangeTracker.Clear();

            // F7 — `CrearSeguimientoRequest` es un record posicional: `with` en vez de mutar.
            var kgH = ItemConsumoCalculos.KgDeAlimento(request.ItemsHembras);
            var kgM = ItemConsumoCalculos.KgDeAlimento(request.ItemsMachos);
            var nombres = ItemConsumoCalculos.NombresDeAlimento(request.ItemsHembras)
                .Concat(ItemConsumoCalculos.NombresDeAlimento(request.ItemsMachos))
                .Distinct()
                .ToArray();

            var sinItems = request with
            {
                ItemsHembras = null,
                ItemsMachos = null,
                ConsumoH = kgH > 0 ? kgH : request.ConsumoH,
                UnidadConsumoH = kgH > 0 ? "kg" : request.UnidadConsumoH,
                ConsumoM = kgM > 0 ? kgM : request.ConsumoM,
                UnidadConsumoM = kgM > 0 ? "kg" : request.UnidadConsumoM,
                TipoAlimento = string.IsNullOrWhiteSpace(request.TipoAlimento) && nombres.Length > 0
                    ? string.Join(", ", nombres)
                    : request.TipoAlimento,
            };

            var id = await _produccion.CrearSeguimientoAsync(sinItems);
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.RequiereCuadre(id, JsonSerializer.Serialize(new { id }, OpcionesJson), ex.Message);
        }
    }
}
