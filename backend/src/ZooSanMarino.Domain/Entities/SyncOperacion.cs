// src/ZooSanMarino.Domain/Entities/SyncOperacion.cs
// Registro de idempotencia del push offline (PWA F3).
//
// Cada operación que un dispositivo capturó sin red deja acá su huella ANTES de que la respuesta
// viaje de vuelta. Es lo único que hace seguro el par "504 + reintento": sin este registro, una
// respuesta perdida en la red hace que el dispositivo reenvíe y el servidor aplique dos veces.
// En capturas de campo eso es mortalidad contada doble.

namespace ZooSanMarino.Domain.Entities;

public class SyncOperacion
{
    public long Id { get; set; }

    /// <summary>
    /// Identificador que genera el DISPOSITIVO al encolar, y que no cambia entre reintentos.
    /// Tiene un índice ÚNICO en la base: es la garantía real de idempotencia, no el chequeo previo
    /// del service (dos dispositivos pueden empujar el mismo lote a la vez).
    /// </summary>
    public Guid ClientOpId { get; set; }

    /// <summary>Tipo de operación. Un tipo desconocido se rechaza como contrato obsoleto.</summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Autor real, estampado por el servidor desde el token. Nunca sale del cuerpo.</summary>
    public int UserId { get; set; }

    /// <summary>Empresa que traía la operación, ya validada contra el JWT.</summary>
    public int CompanyId { get; set; }

    /// <summary>Trazabilidad offline: informativa, NO autoritativa.</summary>
    public string? DeviceId { get; set; }

    /// <summary>Momento que declara el dispositivo. Su reloj puede estar corrido; no se usa para nada crítico.</summary>
    public DateTime? CapturadoAtDispositivo { get; set; }

    /// <summary>`aplicada` | `rechazada` | `requiere_cuadre`.</summary>
    public string Estado { get; set; } = null!;

    /// <summary>Código tipado y estable. Nunca un mensaje libre: el cliente decide con esto.</summary>
    public string? ErrorCodigo { get; set; }

    /// <summary>
    /// Respuesta original, serializada. El replay la devuelve TAL CUAL: un reintento tiene que ver
    /// exactamente lo mismo que vio el envío que sí llegó.
    /// </summary>
    public string? RespuestaJson { get; set; }

    /// <summary>Id de la entidad creada, cuando la hubo.</summary>
    public int? EntidadId { get; set; }

    public DateTime RecibidoAt { get; set; }

    /// <summary>
    /// F7 — texto para el humano, sólo cuando <see cref="Estado"/> es <c>requiere_cuadre</c>: qué
    /// ítem faltó, cuánto había y cuánto se pedía. Es lo que muestra la bandeja del supervisor; el
    /// cliente decide con <see cref="ErrorCodigo"/>, no con esto.
    /// </summary>
    public string? Detalle { get; set; }

    /// <summary>
    /// F7 — cuándo alguien marcó vista la fila en la bandeja de cuadre. "Resolver" un cuadre SOLO
    /// marca visto (decisión del negocio): no repone kilos, porque eso abriría una segunda fórmula
    /// para el mismo número. Null = sigue pendiente.
    /// </summary>
    public DateTime? CuadreResueltoAt { get; set; }

    /// <summary>Quién lo marcó visto.</summary>
    public int? CuadreResueltoPor { get; set; }
}
