// src/ZooSanMarino.Application/DTOs/Migracion/TipoMigracion.cs
namespace ZooSanMarino.Application.DTOs.Migracion;

/// <summary>Tipos de migración masiva soportados (Postura + línea Engorde).</summary>
public enum TipoMigracion
{
    Granjas,
    Nucleos,
    Galpones,
    SeguimientoLevante,
    SeguimientoProduccion,
    Ventas,
    MovimientoAves,
    MovimientoHuevos,
    // ── Línea Engorde ──
    LotesPolloEngorde,
    SeguimientoPolloEngorde,
    VentaPolloEngorde
}

/// <summary>Metadatos de un tipo de migración para el catálogo del módulo (endpoint /tipos).</summary>
/// <param name="Codigo">Nombre del enum (identificador estable usado por el front y las rutas).</param>
/// <param name="Nombre">Etiqueta amigable.</param>
/// <param name="Descripcion">Descripción corta.</param>
/// <param name="RequiereLote">Si necesita bajar el filtro jerárquico hasta un lote elegible.</param>
/// <param name="Fase">Fase de implementación ("1"=estructura, "2"=seguimientos, "3"=ventas/movimientos).</param>
/// <param name="Disponible">Si la carga ya está implementada y habilitada.</param>
public record TipoMigracionInfoDto(
    string Codigo,
    string Nombre,
    string Descripcion,
    bool RequiereLote,
    string Fase,
    bool Disponible
);

/// <summary>Catálogo estático de los tipos de migración.</summary>
public static class TipoMigracionCatalogo
{
    public static IReadOnlyList<TipoMigracionInfoDto> Todos { get; } = new List<TipoMigracionInfoDto>
    {
        new(nameof(TipoMigracion.Granjas),  "Granjas",  "Creación masiva de granjas.",                       false, "1", true),
        new(nameof(TipoMigracion.Nucleos),  "Núcleos",  "Creación masiva de núcleos por granja.",            false, "1", true),
        new(nameof(TipoMigracion.Galpones), "Galpones", "Creación masiva de galpones por núcleo.",           false, "1", true),
        new(nameof(TipoMigracion.SeguimientoLevante),    "Seguimiento Levante",    "Carga histórica de seguimiento diario de levante.",    true, "2", true),
        new(nameof(TipoMigracion.SeguimientoProduccion), "Seguimiento Producción", "Carga histórica de seguimiento diario de producción.", true, "2", true),
        new(nameof(TipoMigracion.Ventas),           "Ventas",             "Carga de ventas de aves (descarte) y de huevos.",   true, "3", false),
        new(nameof(TipoMigracion.MovimientoAves),   "Movimiento de Aves", "Carga de traslados/retiros/ajustes de aves (no-venta).", true, "3", false),
        new(nameof(TipoMigracion.MovimientoHuevos), "Movimiento de Huevos", "Carga de traslados/ajustes de huevos (no-venta).",  true, "3", false),
        // ── Línea Engorde ──
        new(nameof(TipoMigracion.LotesPolloEngorde),       "Lotes Engorde",       "Creación masiva de lotes de pollo de engorde.",          false, "4", true),
        new(nameof(TipoMigracion.SeguimientoPolloEngorde), "Seguimiento Engorde", "Carga histórica de seguimiento diario de engorde.",      true,  "4", true),
        new(nameof(TipoMigracion.VentaPolloEngorde),       "Venta Engorde",       "Carga histórica de ventas de pollo de engorde.",         true,  "4", true),
    };
}
