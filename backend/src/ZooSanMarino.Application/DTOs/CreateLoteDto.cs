// src/ZooSanMarino.Application/DTOs/CreateLoteDto.cs
namespace ZooSanMarino.Application.DTOs;

public class CreateLoteDto
{
    public int? LoteId { get; set; } // Opcional - auto-incremento numérico
    public string LoteNombre { get; set; } = null!;
    public int?   LotePosturaBaseId { get; set; } // ← NUEVO: base lot opcional
    public int    GranjaId { get; set; }
    public string? NucleoId { get; set; }      // ← string?
    public string? GalponId { get; set; }      // ← string?
    public string? Regional { get; set; }
    public DateTime? FechaEncaset { get; set; }
    public int?    HembrasL { get; set; }
    public int?    MachosL { get; set; }
    public double? PesoInicialH { get; set; }
    public double? PesoInicialM { get; set; }
    public double? PesoMixto   { get; set; }
    public double? UnifH { get; set; }
    public double? UnifM { get; set; }
    public int?    MortCajaH { get; set; }
    public int?    MortCajaM { get; set; }
    public string? Raza { get; set; }
    public int?    AnoTablaGenetica { get; set; }
    public string? Linea { get; set; }
    public string? TipoLinea { get; set; }
    public string? CodigoGuiaGenetica { get; set; }
    public int?    LineaGeneticaId { get; set; }  // ← NUEVO: ID de la línea genética
    public string? Tecnico { get; set; }
    public int?    Mixtas { get; set; }
    public int?    AvesEncasetadas { get; set; }
    public string? LoteErp { get; set; }
    public string? LineaGenetica { get; set; }
    public int?    EdadInicial { get; set; }
    public int?    LotePadreId { get; set; } // ← NUEVO: ID del lote padre

    /// <summary>
    /// Fase con la que nace el lote: <c>Levante</c> o <c>Produccion</c>. OPCIONAL — si va vacía se
    /// deriva de las semanas desde el encasetamiento (≥ 26 ⇒ Producción), que es el comportamiento
    /// histórico. Se indica al cargar un lote HISTÓRICO: encasetado hace más de 26 semanas nacería
    /// en Producción y los reportes de levante lo filtrarían, dejando el seguimiento invisible.
    /// </summary>
    public string? Fase { get; set; }

    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    public string? CodigoCentroCosto { get; set; }
    public string? DescripcionCentroCosto { get; set; }
}
