using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;
using ZooSanMarino.Application.Exceptions;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// H4 — alta de un GASTO DE INVENTARIO capturado sin red (consumo de un ítem que no es alimento).
    ///
    /// Es la primera operación offline que no es una captura diaria. Como las otras, llama al mismo
    /// service que usa el controller: una captura offline pasa exactamente por donde pasa una con red.
    ///
    /// ## Por qué no alcanzaba con copiar el patrón de los seguimientos
    ///
    /// En un seguimiento, cuando falta stock el reintento guarda **el día sin los ítems de
    /// inventario**: el hecho de campo —mortalidad, huevos, peso— existe con independencia del
    /// alimento. **En un gasto no hay tal separación: el gasto ES el consumo.** Quitarle las líneas
    /// dejaría un gasto vacío, que no es el dato que nadie capturó.
    ///
    /// Por eso acá el reintento es el mismo alta con <c>sinDescontarStock</c>: el consumo **ocurrió
    /// físicamente** en la granja y lo que está atrasado es el número del sistema. La fila queda en
    /// la bandeja de cuadre (<c>GET /api/Sync/cuadres</c>) para que una persona cargue el ingreso que
    /// falta. Perder un dato de campo es peor que un saldo pendiente de revisar, y ⛔ nunca se
    /// descuenta "hasta donde alcance": un consumo parcial inventa un número.
    ///
    /// ## 🔴 El savepoint no es adorno
    ///
    /// A diferencia de los seguimientos —que validan el stock **antes** de escribir—,
    /// <c>InventarioGastoService.CreateAsync</c> hace <c>SaveChangesAsync</c> de la cabecera
    /// **antes** de recorrer las líneas. Cuando una línea lanza, esa cabecera ya está persistida
    /// dentro de la transacción del push, y <c>ChangeTracker.Clear()</c> —que es lo que alcanzó en
    /// engorde— **no la borra de la base**: un reintento crearía un SEGUNDO gasto. El savepoint
    /// deshace el intento fallido y deja la transacción del push intacta, que es lo que permite
    /// reintentar sin inventar una segunda validación de stock del lado de acá.
    ///
    /// ## La ventana de fecha NO se aplica, a propósito
    ///
    /// <c>InventarioGastosController</c> valida la ventana de fechas de captura antes de llamar al
    /// service; esta rama llama al service, así que no pasa por ahí. Es correcto: una captura offline
    /// **es legítimamente retroactiva** —esa es toda su razón de ser— y su antigüedad ya está acotada
    /// por la jornada offline y por <c>capturadoAtDispositivo</c>. Someterla a la ventana del
    /// formulario haría que una captura del turno de la noche se rechace por la mañana.
    /// </summary>
    private async Task<DespachoResultado> CrearGastoInventarioAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        var request = payload.Deserialize<CreateInventarioGastoRequest>(OpcionesJson)
                      ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");

        var tx = _ctx.Database.CurrentTransaction;

        // Sin transacción ambiente no hay savepoint posible. No debería pasar —`AplicarUnaAsync`
        // siempre abre una— pero si pasara, intentar el camino con reintento dejaría el gasto
        // duplicado: se hace un solo intento y que el rechazo hable.
        if (tx is null)
        {
            var soloIntento = await _gastos.CreateAsync(request, ct);
            return DespachoResultado.Aplicado(soloIntento.Id, JsonSerializer.Serialize(new { id = soloIntento.Id }, OpcionesJson));
        }

        const string savepoint = "sp_gasto_offline";
        await tx.CreateSavepointAsync(savepoint, ct);

        try
        {
            var gasto = await _gastos.CreateAsync(request, ct);
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.Aplicado(gasto.Id, JsonSerializer.Serialize(new { id = gasto.Id }, OpcionesJson));
        }
        catch (StockInsuficienteException ex)
        {
            // Deshace la cabecera y las líneas del intento fallido; la transacción del push sigue
            // viva, así que el registro de idempotencia va a commitear con el reintento.
            await tx.RollbackToSavepointAsync(savepoint, ct);
            _ctx.ChangeTracker.Clear();

            var gasto = await _gastos.CreateAsync(request, ct, sinDescontarStock: true);
            ct.ThrowIfCancellationRequested();
            return DespachoResultado.RequiereCuadre(
                gasto.Id,
                JsonSerializer.Serialize(new { id = gasto.Id }, OpcionesJson),
                ex.Message);
        }
    }
}
