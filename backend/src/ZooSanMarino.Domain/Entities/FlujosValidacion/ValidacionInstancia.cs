namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Ejecución concreta de un <see cref="ValidacionFlujo"/> sobre un registro de negocio
/// (<see cref="RecursoTipo"/>/<see cref="RecursoId"/>, resuelto por el adaptador del proceso).
/// Solo puede existir una instancia ACTIVA (PENDIENTE_VALIDACION o DEVUELTA_CORRECCION) por
/// empresa/proceso/recurso (índice único parcial).
/// </summary>
public class ValidacionInstancia
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public int CompanyId { get; set; }
    public int ProcesoId { get; set; }
    public int FlujoId { get; set; }

    /// <summary>Tipo estable declarado por el adaptador (coincide con <c>validacion_procesos.key</c>).</summary>
    public string RecursoTipo { get; set; } = null!;

    /// <summary>Id del registro de negocio como string; admite bigint o GUID futuros.</summary>
    public string RecursoId { get; set; } = null!;

    /// <summary>1, 2, 3... se incrementa tras una cancelación/reapertura administrativa (desvalidar).</summary>
    public int Intento { get; set; } = 1;

    /// <summary>Ver <see cref="EstadoValidacionInstancia"/>.</summary>
    public string Estado { get; set; } = EstadoValidacionInstancia.PendienteValidacion;

    public int PasoActualOrden { get; set; }

    /// <summary>Etapa que devolvió y a la que debe regresar tras corregir; null si nunca hubo devolución.</summary>
    public int? PasoRetornoOrden { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ReturnedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Última devolución/cancelación/error en texto legible.</summary>
    public string? MotivoEstado { get; set; }

    public Company Company { get; set; } = null!;
    public ValidacionProceso Proceso { get; set; } = null!;
    public ValidacionFlujo Flujo { get; set; } = null!;
    public ICollection<ValidacionInstanciaPaso> Pasos { get; set; } = new List<ValidacionInstanciaPaso>();
    public ICollection<ValidacionAccion> Acciones { get; set; } = new List<ValidacionAccion>();
    public ICollection<ValidacionNovedadUsuario> Novedades { get; set; } = new List<ValidacionNovedadUsuario>();
}

public static class EstadoValidacionInstancia
{
    public const string PendienteValidacion = "PENDIENTE_VALIDACION";
    public const string DevueltaCorreccion = "DEVUELTA_CORRECCION";
    public const string Aprobada = "APROBADA";
    public const string Cancelada = "CANCELADA";
    public const string ErrorFinalizacion = "ERROR_FINALIZACION";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { PendienteValidacion, DevueltaCorreccion, Aprobada, Cancelada, ErrorFinalizacion };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);

    /// <summary>Estados en los que la instancia cuenta para el índice único de "una activa por recurso".</summary>
    public static readonly IReadOnlySet<string> Activos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PendienteValidacion, DevueltaCorreccion };
}
