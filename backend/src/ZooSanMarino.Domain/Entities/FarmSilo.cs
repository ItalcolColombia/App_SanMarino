// src/ZooSanMarino.Domain/Entities/FarmSilo.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Silo de alimento o bodega de una granja. En empresas con
/// <see cref="Company.ManejaInventarioPorSilo"/> = true es la <b>ubicación real del inventario</b>:
/// el stock vive en <c>(farm_id, silo_id)</c> y el galpón queda como filtro de navegación.
///
/// <para>
/// No son galpones: no aparecen al crear lotes ni participan del inventario de aves. Se registran
/// aparte porque el ERP les asigna su propia ubicación/bodega (<c>codigo_erp_ubicacion</c>,
/// <c>centro_operacion</c>, <c>codigo_bodega</c>), que es <b>por granja</b> — el «Silo 1» de una
/// granja tiene una ubicación distinta al de otra, y por eso esos códigos viven acá y no en
/// <see cref="SiloCatalogo"/>.
/// </para>
///
/// <para>
/// Se relaciona con los galpones que alimenta (<see cref="GalponSilo"/>, N:M) y con los lotes que
/// consumen de él (<see cref="LoteSilo"/>, N:M).
/// </para>
/// </summary>
public class FarmSilo
{
    public int Id { get; set; }

    /// <summary>Empresa dueña del registro (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Granja a la que pertenece el silo/bodega (FK a <c>farms.id</c>).</summary>
    public int GranjaId { get; set; }

    /// <summary>
    /// Entrada de la lista maestra de la que sale este silo (FK a <c>silo_catalogo.id</c>).
    /// <c>null</c> para las bodegas y para las filas creadas antes del catálogo.
    /// </summary>
    public int? SiloCatalogoId { get; set; }

    /// <summary>Nombre del silo o bodega (único dentro de la granja).</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>
    /// Tipo de la ubicación: <c>Silo</c> (numerado, sale del catálogo) o <c>Bodega</c> (la «granja
    /// global»: guarda alimento e insumos y admite traslado interno bodega→silo).
    /// <para>
    /// El valor legacy <c>Insumos</c> de la carga inicial equivale a <c>Bodega</c> y se normaliza por
    /// migración.
    /// </para>
    /// </summary>
    public string Tipo { get; set; } = null!;

    /// <summary>Código ERP de ubicación (ej. "BS60101" silo, "BUG60100" bodega de insumos).</summary>
    public string? CodigoErpUbicacion { get; set; }

    /// <summary>Descripción libre del silo/bodega.</summary>
    public string? Descripcion { get; set; }

    /// <summary>Centro de operación ERP (ej. "830").</summary>
    public string? CentroOperacion { get; set; }

    /// <summary>Código de bodega ERP asociado (ej. "B0601").</summary>
    public string? CodigoBodega { get; set; }

    /// <summary>Activo/inactivo: un silo inactivo no se ofrece para movimientos nuevos.</summary>
    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Baja lógica. Un silo con movimientos NUNCA se borra físicamente (rompería la trazabilidad del
    /// stock y del histórico); se marca acá y deja de ofrecerse.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Valores admitidos de <see cref="Tipo"/>.</summary>
    public const string TipoSilo = "Silo";
    public const string TipoBodega = "Bodega";
}
