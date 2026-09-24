namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Bitácora append-only de firmas y decisiones sobre una instancia. Nunca se actualiza ni se
/// borra; es el registro de auditoría que sustenta quién aprobó/devolvió/corrigió cada etapa.
/// </summary>
public class ValidacionAccion
{
    public long Id { get; set; }
    public Guid InstanciaId { get; set; }
    public long InstanciaPasoId { get; set; }

    /// <summary>Ver <see cref="TipoAccionValidacion"/>.</summary>
    public string Accion { get; set; } = null!;

    /// <summary>Usuario real que ejecutó la acción (nunca el hash numérico de ICurrentUser.UserId).</summary>
    public Guid UserId { get; set; }

    /// <summary>Candidato rol/usuario de <c>validacion_flujo_asignados</c> que habilitó la firma; null en acciones que no firman (EDITAR/CANCELAR/OVERRIDE sin asignación directa).</summary>
    public int? AsignadoId { get; set; }

    /// <summary>Obligatorio al devolver/hacer override; opcional al aprobar/editar.</summary>
    public string? Comentario { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ValidacionInstancia Instancia { get; set; } = null!;
    public ValidacionInstanciaPaso InstanciaPaso { get; set; } = null!;
}

public static class TipoAccionValidacion
{
    public const string Aprobar = "APROBAR";
    public const string Devolver = "DEVOLVER";
    public const string Editar = "EDITAR";
    public const string Reenviar = "REENVIAR";
    public const string Eliminar = "ELIMINAR";
    public const string Cancelar = "CANCELAR";
    public const string Override = "OVERRIDE";

    public static readonly IReadOnlySet<string> Todos = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        { Aprobar, Devolver, Editar, Reenviar, Eliminar, Cancelar, Override };

    public static bool EsValido(string? accion) =>
        !string.IsNullOrWhiteSpace(accion) && Todos.Contains(accion);
}
