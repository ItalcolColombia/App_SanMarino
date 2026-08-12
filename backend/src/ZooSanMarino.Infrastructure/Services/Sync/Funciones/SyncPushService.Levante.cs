using System.Text.Json;
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;
using ZooSanMarino.Application.DTOs.Sync;

namespace ZooSanMarino.Infrastructure.Services;

public partial class SyncPushService
{
    /// <summary>
    /// Despacha una operación a su efecto. Devuelve el id de la entidad creada y la respuesta
    /// serializada que se guarda para el replay.
    ///
    /// **No reimplementa reglas de negocio**: llama al mismo service que usa el controller, así que
    /// una captura offline pasa exactamente por donde pasa una con red. Cualquier otra cosa sería una
    /// segunda fórmula para el mismo número.
    /// </summary>
    private async Task<(int? EntidadId, string? RespuestaJson)> DespacharAsync(
        SyncOperacionRequest op, CancellationToken ct)
    {
        return op.Tipo switch
        {
            SyncPushCalculos.Tipos.SeguimientoLevanteCrear => await CrearSeguimientoLevanteAsync(op, ct),
            SyncPushCalculos.Tipos.SeguimientoProduccionCrear => await CrearSeguimientoProduccionAsync(op, ct),
            // `EvaluarOperacion` ya rechazó los tipos desconocidos; esto es la red de seguridad por si
            // alguien agrega un tipo al catálogo y olvida su rama.
            _ => throw new InvalidOperationException($"Tipo sin despacho: '{op.Tipo}'.")
        };
    }

    private async Task<(int?, string?)> CrearSeguimientoLevanteAsync(SyncOperacionRequest op, CancellationToken ct)
    {
        if (op.Payload is not { } payload || payload.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("La operación no trae cuerpo.");
        }

        var request = payload.Deserialize<CreateSeguimientoLoteLevanteRequest>(OpcionesJson)
                      ?? throw new InvalidOperationException("El cuerpo de la captura no se pudo interpretar.");

        // B5 — el autor lo estampa el SERVIDOR. Lo que venga en el cuerpo se descarta: la autoría es
        // falsificable si se acepta del cliente, y el diseño offline la iba a usar como característica.
        var dto = request.ToDto() with
        {
            CreatedByUserId = _current.UserGuid?.ToString() ?? _current.UserId.ToString()
        };

        var creado = await _levante.CreateAsync(dto);
        ct.ThrowIfCancellationRequested();

        return (creado.Id, JsonSerializer.Serialize(new { id = creado.Id, loteId = creado.LoteId }, OpcionesJson));
    }
}
