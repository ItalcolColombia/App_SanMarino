using System.Text.Json;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;

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
    private async Task<(int?, string?)> CrearSeguimientoEngordeAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        var request = LeerCuerpo<CreateSeguimientoLoteLevanteRequest>(op);

        // B5: el autor lo estampa el servidor, no el cuerpo.
        var dto = request.ToDto() with
        {
            CreatedByUserId = _current.UserGuid?.ToString() ?? _current.UserId.ToString()
        };

        var creado = await _engorde.CreateAsync(dto);
        ct.ThrowIfCancellationRequested();

        return (creado.Id, JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson));
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
    private async Task<(int?, string?)> CrearSeguimientoReproductoraEngordeAsync(
        SyncOperacionRequest op, CancellationToken ct)
    {
        var request = LeerCuerpo<CreateSeguimientoDiarioLoteReproductoraRequest>(op);

        var dto = request.ToDto() with
        {
            CreatedByUserId = _current.UserGuid?.ToString() ?? _current.UserId.ToString()
        };

        var creado = await _reproductoraEngorde.CreateAsync(dto);
        ct.ThrowIfCancellationRequested();

        return (creado.Id, JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson));
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
