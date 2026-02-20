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
    public string?   EstadoTraslado     { get; set; } // ← Estados: null/"normal", "trasladado", "en_transferencia"
    public int?      LotePadreId         { get; set; } // ← ID del lote padre (Levante); los de Producción son hijos

    /// <summary>Fase del lote: Levante (inicial) o Produccion (lote hijo al pasar a producción).</summary>
    public string Fase { get; set; } = "Levante";

    // Campos de etapa Producción (solo cuando Fase == "Produccion"; en hijos o mismo registro si se unifica)
    public DateTime? FechaInicioProduccion { get; set; }
    public int?      HembrasInicialesProd   { get; set; }
    public int?      MachosInicialesProd   { get; set; }
    public int?      HuevosIniciales       { get; set; }
    public string?   TipoNido              { get; set; }
    public string?   NucleoP               { get; set; }
    public string?   CicloProduccion       { get; set; }
    public DateTime? FechaFinProduccion    { get; set; }
    public int?      AvesFinHembrasProd    { get; set; }
    public int?      AvesFinMachosProd     { get; set; }

    /// <summary>ID del país en sesión al crear el lote (desde storage/header).</summary>
    public int? PaisId { get; set; }
    /// <summary>Nombre del país en sesión al crear el lote.</summary>
    public string? PaisNombre { get; set; }
    /// <summary>Nombre de la empresa en sesión al crear el lote.</summary>
    public string? EmpresaNombre { get; set; }

    public Farm    Farm   { get; set; } = null!;
    public Nucleo? Nucleo { get; set; }
    public Galpon? Galpon { get; set; }

    // Relación self-referencial para lote padre
    public Lote? LotePadre { get; set; }
    public List<Lote> LotesHijos { get; set; } = new();

    /// <summary>Seguimientos diarios de producción (solo cuando Fase == "Produccion").</summary>
    public List<ProduccionSeguimiento> ProduccionSeguimientos { get; set; } = new();

    // Nota: La relación con LoteReproductora está comentada debido al desajuste de tipos
    // (lote_id es string en lote_reproductoras pero integer en lotes)
    // public List<LoteReproductora> Reproductoras { get; set; } = new(); // Comentado temporalmente
}
