// src/ZooSanMarino.Domain/Entities/SiloCatalogo.cs
namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Lista MAESTRA de silos de una empresa (típicamente 1..100). Es el catálogo del que cada granja
/// elige los silos que realmente tiene: la fila por granja vive en <see cref="FarmSilo"/>, que es la
/// que lleva los códigos ERP (el «Silo 1» de una granja y el de otra tienen ubicaciones distintas).
///
/// <para>
/// Solo silos NUMERADOS. La <b>bodega</b> no va acá: es una ubicación propia de cada granja (la
/// «granja global» del negocio) y se crea directamente como <see cref="FarmSilo"/> con
/// <c>Tipo = 'Bodega'</c>.
/// </para>
///
/// <para>
/// No se modeló sobre las listas maestras genéricas (<c>master_lists</c>) porque esas son clave/valor
/// de texto: el silo necesita <see cref="Numero"/> tipado y ser destino de FK desde
/// <see cref="FarmSilo.SiloCatalogoId"/>, y contra una lista de strings no hay integridad posible.
/// </para>
///
/// Solo aplica a empresas con <see cref="Company.ManejaInventarioPorSilo"/> = true.
/// </summary>
public class SiloCatalogo
{
    public int Id { get; set; }

    /// <summary>Empresa dueña de la lista (scoping multi-empresa).</summary>
    public int CompanyId { get; set; }

    /// <summary>Número del silo dentro de la empresa (1..100). Único entre los no borrados.</summary>
    public int Numero { get; set; }

    /// <summary>Nombre visible ("Silo 1"). Único dentro de la empresa entre los no borrados.</summary>
    public string Nombre { get; set; } = null!;

    /// <summary>Descripción libre (opcional).</summary>
    public string? Descripcion { get; set; }

    /// <summary>Activo/inactivo: un silo inactivo no se ofrece para asignar a granjas nuevas.</summary>
    public bool Activo { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Baja lógica. Los índices únicos solo consideran las filas con <c>null</c>.</summary>
    public DateTime? DeletedAt { get; set; }
}
