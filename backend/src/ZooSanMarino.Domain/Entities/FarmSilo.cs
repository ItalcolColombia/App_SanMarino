// src/ZooSanMarino.Domain/Entities/FarmSilo.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Silo de alimento o bodega de insumos de una granja (catálogo). No son galpones: no deben
/// aparecer al crear lotes ni participan del inventario de aves. Se registran aparte porque el
/// ERP les asigna su propia ubicación/bodega (empresas con
/// <see cref="Company.ManejaCodigosErpAvicola"/> = true). El consumo por silo es fase futura;
/// por ahora es solo catálogo (sin endpoints).
/// </summary>
public class FarmSilo
{
    public int Id { get; set; }

    /// <summary>Empresa dueña del registro (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Granja a la que pertenece el silo/bodega (FK a <c>farms.id</c>).</summary>
    public int GranjaId { get; set; }

    /// <summary>Nombre del silo o bodega (único dentro de la granja).</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Tipo de la ubicación: <c>Silo</c> (alimento) o <c>Insumos</c> (bodega).</summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Código ERP de ubicación (ej. "BS60101" silo, "BUG60100" bodega de insumos).</summary>
    public string? CodigoErpUbicacion { get; set; }

    /// <summary>Descripción libre del silo/bodega.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Centro de operación ERP (ej. "830").</summary>
    public string? CentroOperacion { get; set; }

    /// <summary>Código de bodega ERP asociado (ej. "B0601").</summary>
    public string? CodigoBodega { get; set; }

    /// <summary>Activo/inactivo (baja lógica del catálogo).</summary>
    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
