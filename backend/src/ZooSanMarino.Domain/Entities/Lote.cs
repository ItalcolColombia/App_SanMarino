// src/ZooSanMarino.Domain/Entities/Lote.cs
namespace ZooSanMarino.Domain.Entities;

// src/ZooSanMarino.Domain/Entities/Lote.cs
public class Lote : AuditableEntity
{
    public int? LoteId      { get; set; } // Auto-incremento numérico simple
    public string LoteNombre  { get; set; } = null!;
    public int    GranjaId    { get; set; }
    public string? NucleoId   { get; set; }
    public string? GalponId   { get; set; }

    public string?   Regional           { get; set; }
    public DateTime? FechaEncaset       { get; set; }
    public int?      HembrasL           { get; set; }
    public int?      MachosL            { get; set; }

    // ← OJO: todos como double? (coincide con columnas double precision)
    public double?   PesoInicialH       { get; set; }
    public double?   PesoInicialM       { get; set; }
    public double?   UnifH              { get; set; }
    public double?   UnifM              { get; set; }

    public int?      MortCajaH          { get; set; }
    public int?      MortCajaM          { get; set; }
    public string?   Raza               { get; set; }
    public int?      AnoTablaGenetica   { get; set; }
    public string?   Linea              { get; set; }
    public string?   TipoLinea          { get; set; }
    public string?   CodigoGuiaGenetica { get; set; }
    public int?      LineaGeneticaId    { get; set; }  // ← NUEVO: ID de la línea genética
    public string?   Tecnico            { get; set; }

    public int?      Mixtas             { get; set; }
    public double?   PesoMixto          { get; set; } // ← double?
    public int?      AvesEncasetadas    { get; set; }
    public int?      EdadInicial        { get; set; }
    public string?   LoteErp            { get; set; } // ← NUEVO: Código ERP del lote

    public Farm    Farm   { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }

    public List<LoteReproductora> Reproductoras { get; set; } = new();
}
