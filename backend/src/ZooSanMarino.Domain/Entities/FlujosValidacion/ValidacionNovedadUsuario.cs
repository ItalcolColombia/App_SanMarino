namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Aviso persistente y accionable para el Home (tarjeta roja). No se deriva solo de
/// <see cref="ValidacionAccion"/> porque necesita destinatario, estado de resolución y contexto
/// legible aun si el seguimiento origen se elimina. Leer (<see cref="ReadAt"/>) NO resuelve la
/// novedad: solo corregir/reenviar, devolver otra etapa o eliminar el registro la resuelve.
/// </summary>
public class ValidacionNovedadUsuario
{
    public long Id { get; set; }
    public Guid InstanciaId { get; set; }

    /// <summary>Acción DEVOLVER que originó esta novedad.</summary>
    public long AccionDevolucionId { get; set; }

    public int CompanyId { get; set; }

    /// <summary>Firmante de la etapa anterior, o el creador si se devolvió la etapa 1.</summary>
    public Guid DestinatarioUserId { get; set; }

    public Guid GeneradaPorUserId { get; set; }

    /// <summary>ACTIVA | RESUELTA | CANCELADA — ver <see cref="EstadoNovedadValidacion"/>.</summary>
    public string Estado { get; set; } = EstadoNovedadValidacion.Activa;

    /// <summary>Descripción obligatoria escrita al devolver.</summary>
    public string Motivo { get; set; } = null!;

    /// <summary>Snapshot JSONB: empresa/proceso, granja, núcleo, galpón, lote, fecha, ruta.</summary>
    public string ContextoResumen { get; set; } = "{}";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    /// <summary>CORREGIDO_REENVIADO | DEVUELTO_ATRAS | ELIMINADO — ver <see cref="ResolucionNovedadValidacion"/>.</summary>
    public string? Resolucion { get; set; }

    public ValidacionInstancia Instancia { get; set; } = null!;
    public ValidacionAccion AccionDevolucion { get; set; } = null!;
}

public static class EstadoNovedadValidacion
{
    public const string Activa = "ACTIVA";
    public const string Resuelta = "RESUELTA";
    public const string Cancelada = "CANCELADA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Activa, Resuelta, Cancelada };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);
}

public static class ResolucionNovedadValidacion
{
    public const string CorregidoReenviado = "CORREGIDO_REENVIADO";
    public const string DevueltoAtras = "DEVUELTO_ATRAS";
    public const string Eliminado = "ELIMINADO";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { CorregidoReenviado, DevueltoAtras, Eliminado };

    public static bool EsValido(string? resolucion) =>
        !string.IsNullOrWhiteSpace(resolucion) && Todos.Contains(resolucion);
}
