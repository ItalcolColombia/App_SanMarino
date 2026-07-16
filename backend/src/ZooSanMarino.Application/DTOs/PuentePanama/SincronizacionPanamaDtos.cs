namespace ZooSanMarino.Application.DTOs.PuentePanama;

/// <summary>Conexión al API origen. Si viene null en el request, se usa la config del backend (sección PuentePanama).</summary>
public sealed class PanamaConexion
{
    public string? BaseUrl { get; set; }
    public string? Email { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// Parámetros de una corrida del puente. Filtros combinables:
/// <see cref="Anio"/> = año de inicio del lote (null = todos); <see cref="ClienteIdOrigen"/> / <see cref="GranjaIdOrigen"/>
/// acotan a un cliente/granja del origen; <see cref="FechaHasta"/> trae hasta esa fecha (lotes iniciados ≤ y
/// seguimiento con fecha ≤). <see cref="DryRun"/> = previsualizar sin insertar.
/// </summary>
public sealed class SincronizarPanamaRequest
{
    // ── Filtros ──
    public int? Anio { get; set; }
    public int? ClienteIdOrigen { get; set; }
    public int? GranjaIdOrigen { get; set; }
    public DateTime? FechaHasta { get; set; }
    public bool DryRun { get; set; } = true;

    // ── Genética (la guía de Panamá no trae raza/año: se eligen aquí y se crea/asegura la guía) ──
    public string? GeneticaRaza { get; set; }
    public int? GeneticaAnio { get; set; }               // null = año de inicio de cada lote
    public bool ImportarGuiaGenetica { get; set; } = true;

    /// <summary>
    /// Red de seguridad: si para (raza, año) no hay guía en el sistema y la del origen no está disponible
    /// o viene vacía (o la importación está apagada), crea una guía de PRUEBA claramente marcada como FAKE
    /// (datos del origen si existen; si no, curva placeholder 14→264 g/día en 49 días) para que los lotes
    /// no queden pendientes. Deja rastro fuerte en mensajes/preview: hay que cargar la guía real después.
    /// </summary>
    public bool CrearGuiaFakeSiFalta { get; set; } = true;

    // ── Conexión al origen (opcional; null = config del backend) ──
    public PanamaConexion? Origen { get; set; }
}

/// <summary>Resultado de probar la conexión/login contra el origen.</summary>
public sealed class ConexionResultDto
{
    public bool Ok { get; set; }
    public string? Mensaje { get; set; }
    public DateTime? Expiracion { get; set; }
}

/// <summary>Resumen por lote para la vista informativa del front (qué trae cada lote y su estado).</summary>
public sealed class LotePreviewDto
{
    public int IdOrigen { get; set; }
    public string Lote { get; set; } = "";
    public string? Granja { get; set; }
    public string? Galpon { get; set; }
    public DateTime? FechaInicio { get; set; }
    public int? AvesEncasetadas { get; set; }
    /// <summary>Raza/línea genética con la que se crea el lote (del origen o el override global).</summary>
    public string? Raza { get; set; }
    public int Seguimientos { get; set; }
    public int Reproductoras { get; set; }
    public int SeguimientosReproductora { get; set; }
    /// <summary>Lesiones de reproductora nuevas que trae/traería el lote.</summary>
    public int Lesiones { get; set; }
    /// <summary>Nuevo | YaExiste | Pendiente | Error</summary>
    public string Estado { get; set; } = "";
    public string? Mensaje { get; set; }
}

/// <summary>Contadores + detalle + mensajes del resultado de una corrida (dry-run o real).</summary>
public sealed class ResultadoSincronizacionDto
{
    public bool DryRun { get; set; }
    public int? Anio { get; set; }
    public int CompanyId { get; set; }

    // Guía genética
    public bool GuiaGeneticaImportada { get; set; }
    public int GuiaGeneticaFilas { get; set; }
    public string? GuiaGeneticaRazaAnio { get; set; }
    /// <summary>True si se creó (o se crearía, en dry-run) al menos una guía de PRUEBA (FAKE). Cargá la guía real: los indicadores contra guía no son válidos hasta entonces.</summary>
    public bool GuiaGeneticaFakeCreada { get; set; }

    public int GranjasVistas { get; set; }
    public int GranjasNuevas { get; set; }

    public int GalponesVistos { get; set; }
    public int GalponesNuevos { get; set; }

    public int LotesEnAnio { get; set; }
    public int LotesNuevos { get; set; }
    public int LotesOmitidos { get; set; }
    public int LotesConError { get; set; }
    /// <summary>Lotes que quedan pendientes por no tener guía genética (no se crean; se listan para cargar luego).</summary>
    public int LotesPendientes { get; set; }

    public int SeguimientosNuevos { get; set; }
    public int SeguimientosOmitidos { get; set; }

    public int ReproductorasNuevas { get; set; }
    public int ReproductorasOmitidas { get; set; }

    public int SeguimientosReproNuevos { get; set; }
    public int SeguimientosReproOmitidos { get; set; }

    public int LesionesNuevas { get; set; }
    public int LesionesOmitidas { get; set; }

    public long DuracionMs { get; set; }
    public string Estado { get; set; } = "Ok";   // Ok | ConAdvertencias | Fallido

    /// <summary>Id del registro de auditoría (migracion_masiva) de esta corrida — "corrida #id" en el front.</summary>
    public int? AuditoriaId { get; set; }

    /// <summary>Detalle por lote (para la vista informativa del front).</summary>
    public List<LotePreviewDto> Lotes { get; set; } = new();

    /// <summary>Advertencias/errores legibles.</summary>
    public List<string> Mensajes { get; set; } = new();
}

/// <summary>Fila del historial de corridas del puente (metadatos + contadores; sin el detalle jsonb pesado).</summary>
public sealed class SincronizacionHistorialItemDto
{
    public int Id { get; set; }
    public DateTime FechaProceso { get; set; }
    public bool FueDryRun { get; set; }
    public string Estado { get; set; } = "";
    public long? DuracionMs { get; set; }
    /// <summary>Descriptor de la corrida ("ZooPanamaPollo · año X").</summary>
    public string NombreArchivo { get; set; } = "";
    public int LotesTotales { get; set; }
    public int LotesNuevos { get; set; }
    public int LotesOmitidos { get; set; }
    public int LotesConError { get; set; }
    /// <summary>Derivado: totales − nuevos − omitidos − error (los pendientes no tienen columna propia en la auditoría).</summary>
    public int LotesPendientes { get; set; }
    /// <summary>True si la corrida tiene el detalle completo persistido (corridas posteriores a la mejora de historial).</summary>
    public bool TieneDetalle { get; set; }
}

public sealed class SincronizacionHistorialPagedDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public List<SincronizacionHistorialItemDto> Items { get; set; } = new();
}

/// <summary>Detalle de una corrida del historial: metadatos + el ResultadoSincronizacionDto persistido (null en corridas viejas sin detalle).</summary>
public sealed class SincronizacionHistorialDetalleDto
{
    public int Id { get; set; }
    public DateTime FechaProceso { get; set; }
    public bool FueDryRun { get; set; }
    public string Estado { get; set; } = "";
    public long? DuracionMs { get; set; }
    public string NombreArchivo { get; set; } = "";
    public ResultadoSincronizacionDto? Resultado { get; set; }
}
