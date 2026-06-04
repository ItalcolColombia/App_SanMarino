namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Ticket de soporte / requerimiento. Centraliza errores, dudas, soportes y nuevos
/// requerimientos con trazabilidad por país y empresa (multi-tenant vía
/// <see cref="AuditableEntity"/>). El país y el autor se infieren del contexto del
/// request (<c>ICurrentUser</c>), nunca del body.
/// </summary>
public class Ticket : AuditableEntity
{
    public long Id { get; set; }

    /// <summary>Código legible para soporte (ej: <c>TK-2026-000123</c>). Se genera en backend.</summary>
    public string? Codigo { get; set; }

    /// <summary>País de origen (de <c>ICurrentUser.PaisId</c>).</summary>
    public int PaisId { get; set; }

    /// <summary>SOPORTE | DESARROLLO | REQUERIMIENTO | DUDAS — ver <see cref="TicketTipos"/>.</summary>
    public string Tipo { get; set; } = default!;

    /// <summary>
    /// ABIERTO | EN_ANALISIS | EN_IMPLEMENTACION | SOLUCIONADO | TRANSFERIDO | SUSPENDIDO —
    /// ver <see cref="TicketEstados"/>.
    /// </summary>
    public string Estado { get; set; } = TicketEstados.Abierto;

    public string Titulo { get; set; } = default!;
    public string Descripcion { get; set; } = default!;

    /// <summary>Resolutor que tomó el ticket (de <c>ICurrentUser.UserId</c> al hacer "tomar").</summary>
    public int? AssignedToUserId { get; set; }

    /// <summary>Cuándo un resolutor abrió/tomó el ticket por primera vez.</summary>
    public DateTime? FechaPrimeraApertura { get; set; }

    /// <summary>Cuándo pasó a SOLUCIONADO.</summary>
    public DateTime? FechaSolucion { get; set; }

    /// <summary>Estado del registro (A=activo). Patrón del proyecto.</summary>
    public string Status { get; set; } = "A";

    // Navegación
    public ICollection<TicketImagen> Imagenes { get; set; } = new List<TicketImagen>();
    public ICollection<TicketNota> Notas { get; set; } = new List<TicketNota>();
}
