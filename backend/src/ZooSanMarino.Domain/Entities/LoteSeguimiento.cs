// src/ZooSanMarino.Domain/Entities/LoteSeguimiento.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

public class LoteSeguimiento : AuditableEntity
{
public int Id { get; set; } // PK identity
public DateTime Fecha { get; set; }


public string LoteId { get; set; } = null!; // parte de FK → LoteReproductora (string para coincidir con character varying)
public string ReproductoraId { get; set; } = null!; // parte de FK → LoteReproductora


// Métricas (decimales con precisión)
public decimal? PesoInicial { get; set; }
public decimal? PesoFinal { get; set; }
public int? MortalidadM { get; set; }
public int? MortalidadH { get; set; }
public int? SelM { get; set; }
public int? SelH { get; set; }
public int? ErrorM { get; set; }
public int? ErrorH { get; set; }
public string? TipoAlimento { get; set; }
public decimal? ConsumoAlimento { get; set; } // Consumo para hembras (en kg)
public decimal? ConsumoKgMachos { get; set; } // Consumo para machos (en kg)
public string? Observaciones { get; set; }
public string Ciclo { get; set; } = "Normal"; // "Normal" | "Reforzado"

// Campos de peso y uniformidad (double precision en PG → double?)
public double? PesoPromH { get; set; }
public double? PesoPromM { get; set; }
public double? UniformidadH { get; set; }
public double? UniformidadM { get; set; }
public double? CvH { get; set; }
public double? CvM { get; set; }

// Metadata JSONB para campos adicionales/extras (consumo original con unidad, etc.)
public JsonDocument? Metadata { get; set; }

// Items adicionales JSONB para almacenar otros tipos de ítems (vacunas, medicamentos, etc.)
// que NO son alimentos. Los alimentos se mantienen en los campos tradicionales.
public JsonDocument? ItemsAdicionales { get; set; }

// Campos de agua (solo para Ecuador y Panamá)
// NOTA: Usar double? para coincidir con double precision en PostgreSQL
public double? ConsumoAguaDiario { get; set; } // Consumo diario de agua en litros
public double? ConsumoAguaPh { get; set; } // Nivel de PH del agua
public double? ConsumoAguaOrp { get; set; } // Nivel de ORP (Oxidación-Reducción Potencial) del agua en mV
public double? ConsumoAguaTemperatura { get; set; } // Temperatura del agua en °C


// Navegación
public LoteReproductora LoteReproductora { get; set; } = null!;
// Nota: La relación con Lote está comentada debido al desajuste de tipos
// public Lote Lote { get; set; } = null!;
}