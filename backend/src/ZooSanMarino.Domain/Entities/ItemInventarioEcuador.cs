// src/ZooSanMarino.Domain/Entities/ItemInventarioEcuador.cs
// Catálogo de ítems de inventario para el módulo Gestión de Inventario (Ecuador/Panama).

namespace ZooSanMarino.Domain.Entities;

public class ItemInventarioEcuador
{
    public int Id { get; set; }
    public string Codigo { get; set; } = null!;
    public string Nombre { get; set; } = null!;
    /// <summary>Tipo: alimento, medicamento, insumo, otro.</summary>
    public string TipoItem { get; set; } = "alimento";
    /// <summary>Unidad de medida (kg, und, l, etc.). Se selecciona para ítems que tienen unidad.</summary>
    public string Unidad { get; set; } = "kg";
    public string? Descripcion { get; set; }
    public bool Activo { get; set; } = true;

    // Campos alineados con planilla de carga (GRUPO, TIPO DE INVENTARIO, Desc. tipo inventario, Referencia, Desc. item, Concepto)
    public string? Grupo { get; set; }
    public string? TipoInventarioCodigo { get; set; }
    public string? DescripcionTipoInventario { get; set; }
    public string? Referencia { get; set; }
    public string? DescripcionItem { get; set; }
    public string? Concepto { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
}
