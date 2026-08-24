using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Exceptions;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// Despacha una operación a su efecto. Devuelve el id de la entidad creada, la respuesta
    /// serializada que se guarda para el replay, y (F7) si tuvo que aplicarse SIN los ítems de
    /// inventario por falta de stock — en ese caso <see cref="AplicarUnaAsync"/> marca
    /// <c>requiere_cuadre</c> en vez de <c>aplicada</c>.
    ///
    /// **No reimplementa reglas de negocio**: llama al mismo service que usa el controller, así que
    /// una captura offline pasa exactamente por donde pasa una con red. Cualquier otra cosa sería una
    /// segunda fórmula para el mismo número.
    /// </summary>
    private async Task<DespachoResultado> DespacharAsync(
        SyncOperacionRequest op, CancellationToken ct)
    {
        return op.Tipo switch
        {
            SyncPushCalculos.Tipos.SeguimientoLevanteCrear => await CrearSeguimientoLevanteAsync(op, ct),
            SyncPushCalculos.Tipos.SeguimientoProduccionCrear => await CrearSeguimientoProduccionAsync(op, ct),
            SyncPushCalculos.Tipos.SeguimientoEngordeCrear => await CrearSeguimientoEngordeAsync(op, ct),
            SyncPushCalculos.Tipos.SeguimientoReproductoraEngordeCrear
                => await CrearSeguimientoReproductoraEngordeAsync(op, ct),
            // `EvaluarOperacion` ya rechazó los tipos desconocidos; esto es la red de seguridad por si
            // alguien agrega un tipo al catálogo y olvida su rama.
            _ => throw new InvalidOperationException($"Tipo sin despacho: '{op.Tipo}'.")
        };
    }

    private async Task<DespachoResultado> CrearSeguimientoLevanteAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        var request = payload.Deserialize<CreateSeguimientoLoteLevanteRequest>(OpcionesJson)
                      ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");

        // B5 — el autor lo estampa el SERVIDOR. Lo que venga en el cuerpo se descarta: la autoría es
        // falsificable si se acepta del cliente, y el diseño offline la iba a usar como característica.
        var autor = _current.UserGuid?.ToString() ?? _current.UserId.ToString();

        try
        {
            var creado = await _levante.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.Aplicado(creado.Id,
                JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson));
        }
        catch (StockInsuficienteException ex) when (TraeItems(request.ItemsHembras, request.ItemsMachos, request.ItemsGenerales))
        {
            // El primer intento agregó su entidad al ChangeTracker ANTES de validar el stock (F3:
            // el registro se arma y se hace Add() antes del chequeo previo a SaveChanges). Sin
            // limpiar, el reintento agrega una SEGUNDA entidad para el mismo lote+fecha y las dos
            // quedan pendientes de guardar — el índice único (lote, fecha) las rechaza a las dos.
            _ctx.ChangeTracker.Clear();

            // F7: se reintenta el MISMO alta, sin ítems y con el kg total en el escalar — así el
            // guard de "alimento obligatorio" ve alimento igual, sólo que sin descuento de stock.
            QuitarItemsYRecalcularEscalar(request);
            var creado = await _levante.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.RequiereCuadre(creado.Id,
                JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson), ex.Message);
        }
    }

    /// <summary>
    /// F7 — deja el request de levante/engorde SIN ítems de inventario (para que el reintento no
    /// vuelva a intentar el descuento) pero con el kg total volcado al escalar
    /// <c>consumoKgHembras</c>/<c>consumoKgMachos</c> y <c>tipoAlimento</c> si venía vacío: sin esto
    /// el guard de "alimento obligatorio" rechazaría el reintento por falta de alimento, perdiendo
    /// igual el día que F7 existe para salvar.
    /// </summary>
    private static void QuitarItemsYRecalcularEscalar(CreateSeguimientoLoteLevanteRequest request)
    {
        var kgH = ItemConsumoCalculos.KgDeAlimento(request.ItemsHembras);
        var kgM = ItemConsumoCalculos.KgDeAlimento(request.ItemsMachos);
        var nombres = ItemConsumoCalculos.NombresDeAlimento(request.ItemsHembras)
            .Concat(ItemConsumoCalculos.NombresDeAlimento(request.ItemsMachos))
            .Distinct()
            .ToArray();

        request.ItemsHembras = null;
        request.ItemsMachos = null;
        request.ItemsGenerales = null;

        if (kgH > 0) request.ConsumoKgHembrasDirecto = kgH;
        if (kgM > 0) request.ConsumoKgMachosDirecto = kgM;
        if (string.IsNullOrWhiteSpace(request.TipoAlimento) && nombres.Length > 0)
            request.TipoAlimento = string.Join(", ", nombres);
    }
}
