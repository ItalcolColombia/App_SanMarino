using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Exceptions;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// Alta de seguimiento diario de POLLO ENGORDE.
    ///
    /// Comparte cuerpo con levante (<see cref="CreateSeguimientoLoteLevanteRequest"/>) pero **no**
    /// service: engorde tiene el suyo, con su saldo de alimento y su cuadre. Por eso son dos tipos
    /// de operación distintos aunque el payload se vea igual — el tipo es lo que decide a qué
    /// service va, y confundirlos escribiría el seguimiento en la etapa equivocada.
    /// </summary>
    private async Task<DespachoResultado> CrearSeguimientoEngordeAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        var request = LeerCuerpo<CreateSeguimientoLoteLevanteRequest>(op);
        var autor = _current.UserGuid?.ToString() ?? _current.UserId.ToString();

        try
        {
            var creado = await _engorde.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
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
            QuitarItemsYRecalcularEscalar(request);
            var creado = await _engorde.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.RequiereCuadre(creado.Id,
                JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson), ex.Message);
        }
    }

    /// <summary>
    /// Alta de seguimiento diario de REPRODUCTORA de pollo engorde.
    ///
    /// ## Lo que NO cambia por ser offline
    ///
    /// El cruce hacia pollo engorde solo ocurre con `confirmado = true`, y esa regla vive en el
    /// service. Acá no se toca: una captura sincronizada tarde entra igual que una hecha con red, y
    /// si viene sin confirmar se queda sin cruzar — que es el comportamiento correcto.
    /// </summary>
    private async Task<DespachoResultado> CrearSeguimientoReproductoraEngordeAsync(
        SyncOperacionRequest op, CancellationToken ct)
    {
        var request = LeerCuerpo<CreateSeguimientoDiarioLoteReproductoraRequest>(op);
        var autor = _current.UserGuid?.ToString() ?? _current.UserId.ToString();

        try
        {
            var creado = await _reproductoraEngorde.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.Aplicado(creado.Id,
                JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson));
        }
        catch (StockInsuficienteException ex) when (TraeItems(request.ItemsHembras, request.ItemsMachos))
        {
            _ctx.ChangeTracker.Clear();
            QuitarItemsYRecalcularEscalarReproductora(request);
            var creado = await _reproductoraEngorde.CreateAsync(request.ToDto() with { CreatedByUserId = autor });
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.RequiereCuadre(creado.Id,
                JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson), ex.Message);
        }
    }

    /// <summary>F7 — versión de <see cref="QuitarItemsYRecalcularEscalar"/> para reproductora, cuyo request
    /// escalar se llama <c>ConsumoHembras</c>/<c>ConsumoMachos</c> (no <c>*Directo</c>) y no tiene
    /// <c>ItemsGenerales</c>.</summary>
    private static void QuitarItemsYRecalcularEscalarReproductora(CreateSeguimientoDiarioLoteReproductoraRequest request)
    {
        var kgH = ItemConsumoCalculos.KgDeAlimento(request.ItemsHembras);
        var kgM = ItemConsumoCalculos.KgDeAlimento(request.ItemsMachos);
        var nombres = ItemConsumoCalculos.NombresDeAlimento(request.ItemsHembras)
            .Concat(ItemConsumoCalculos.NombresDeAlimento(request.ItemsMachos))
            .Distinct()
            .ToArray();

        request.ItemsHembras = null;
        request.ItemsMachos = null;

        if (kgH > 0) { request.ConsumoHembras = kgH; request.UnidadConsumoHembras = "kg"; }
        if (kgM > 0) { request.ConsumoMachos = kgM; request.UnidadConsumoMachos = "kg"; }
        if (string.IsNullOrWhiteSpace(request.TipoAlimento) && nombres.Length > 0)
            request.TipoAlimento = string.Join(", ", nombres);
    }

    /// <summary>
    /// Deserializa el cuerpo de la operación. Un cuerpo ausente o ilegible es un error de captura de
    /// ESA operación (<c>regla_de_negocio</c>), no un fallo del lote entero.
    /// </summary>
    private static T LeerCuerpo<T>(SyncOperacionRequest op)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        return payload.Deserialize<T>(OpcionesJson)
               ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");
    }
}
