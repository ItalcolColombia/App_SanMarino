// src/ZooSanMarino.Domain/Entities/InventarioGestionStock.cs
// Stock del módulo Gestión de Inventario (Panama/Ecuador).
// Para item tipo "alimento": ubicación Granja -> Núcleo -> Galpón.
// Para otros tipos: solo Granja (NucleoId y GalponId null).

namespace ZooSanMarino.Domain.Entities;

public class InventarioGestionStock
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    public int FarmId { get; set; }
    /// <summary>Requerido para alimento; null para otros tipos (stock a nivel granja).</summary>
    public string? NucleoId { get; set; }
    /// <summary>Requerido para alimento; null para otros tipos.</summary>
    public string? GalponId { get; set; }
    public int ItemInventarioEcuadorId { get; set; }

    /// <summary>
    /// Silo o bodega donde está físicamente el ítem (FK → <c>farm_silos</c>).
    /// <para>
    /// Solo lo usan las empresas con <c>maneja_inventario_por_silo</c>; con el flag apagado es
    /// SIEMPRE <c>null</c> y la clave natural se comporta igual que antes de la Fase B
    /// (<c>COALESCE(silo_id,0)</c> queda constante en 0). Cuando tiene valor, <see cref="NucleoId"/>
    /// y <see cref="GalponId"/> van en <c>null</c>: la ubicación del alimento es el silo, no el galpón
    /// —un silo puede alimentar a varios galpones y su saldo es UNO solo—.
    /// </para>
    /// </summary>
    public int? SiloId { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
    public Farm Farm { get; set; } = null!;
    public ItemInventario ItemInventario { get; set; } = null!;
}
