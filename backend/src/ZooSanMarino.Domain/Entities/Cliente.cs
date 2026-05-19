namespace ZooSanMarino.Domain.Entities;

public class Cliente : AuditableEntity
{
    public int Id { get; set; }

    public string TipoDocumento { get; set; } = default!;
    public string NumeroIdentificacion { get; set; } = default!;
    public string Nombre { get; set; } = default!;
    public string? Correo { get; set; }
    public string? Telefono { get; set; }
    public string? TipoCliente { get; set; }
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? Distrito { get; set; }
    public string? Planta { get; set; }
    public string? Zona { get; set; }

    public string Status { get; set; } = "A";
}
