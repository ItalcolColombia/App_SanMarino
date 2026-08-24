// src/ZooSanMarino.Application/DTOs/Sync/SyncPushDtos.cs
// Contrato del push offline (PWA F3).

using System.Text.Json;

namespace ZooSanMarino.Application.DTOs.Sync;

/// <summary>Una operación capturada sin red.</summary>
public class SyncOperacionRequest
{
    /// <summary>
    /// Texto, no <c>Guid</c>, a propósito: un uuid mal formado tiene que rechazarse como error de
    /// captura de ESA operación, no romper el binding del lote entero y tirar abajo las sanas.
    /// </summary>
    public string? ClientOpId { get; set; }

    public string? Tipo { get; set; }

    /// <summary>Empresa en la que se capturó. El servidor la valida contra el JWT y NO la reasigna.</summary>
    public int CompanyId { get; set; }

    public int? PaisId { get; set; }

    /// <summary>Informativo. Nunca decide autorización.</summary>
    public string? DeviceId { get; set; }

    public DateTime? CapturadoAtDispositivo { get; set; }

    /// <summary>Cuerpo original de la mutación, tal como lo habría mandado la pantalla con red.</summary>
    public JsonElement? Payload { get; set; }
}

public class SyncPushRequest
{
    public List<SyncOperacionRequest> Operaciones { get; set; } = new();
}

/// <summary>Resultado de UNA operación. El lote nunca es todo-o-nada.</summary>
public class SyncOperacionResultado
{
    public string ClientOpId { get; set; } = null!;

    /// <summary><c>aplicada</c> | <c>rechazada</c> | <c>requiere_cuadre</c>.</summary>
    public string Estado { get; set; } = null!;

    /// <summary>Código tipado y estable. El cliente decide con esto, no con el detalle.</summary>
    public string? ErrorCodigo { get; set; }

    /// <summary>Texto para el humano. Puede cambiar sin romper dispositivos.</summary>
    public string? Detalle { get; set; }

    public int? EntidadId { get; set; }

    /// <summary>
    /// <c>true</c> cuando la operación ya estaba registrada y esta respuesta salió del registro,
    /// sin volver a aplicar el efecto. Es la prueba visible de que la idempotencia actuó.
    /// </summary>
    public bool Replay { get; set; }
}

public class SyncPushResponse
{
    public List<SyncOperacionResultado> Resultados { get; set; } = new();
}

/// <summary>
/// F7 — una fila de la bandeja de cuadre: un push que se aplicó SIN descontar inventario porque no
/// había stock. El día de campo está guardado (<see cref="EntidadId"/> lo identifica); lo que falta
/// es que alguien revise el faltante y, si corresponde, cargue el ingreso de alimento a mano.
/// </summary>
public class CuadrePendienteDto
{
    public long Id { get; set; }

    /// <summary>Uno de <c>SyncPushCalculos.Tipos</c> — de qué módulo es el registro.</summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Id del seguimiento ya guardado (levante/engorde/producción/reproductora según <see cref="Tipo"/>).</summary>
    public int? EntidadId { get; set; }

    /// <summary>Qué ítem faltó, cuánto había y cuánto se pedía — texto para el humano.</summary>
    public string? Detalle { get; set; }

    /// <summary>Informativo: qué dispositivo lo capturó.</summary>
    public string? DeviceId { get; set; }

    public DateTime RecibidoAt { get; set; }
}
