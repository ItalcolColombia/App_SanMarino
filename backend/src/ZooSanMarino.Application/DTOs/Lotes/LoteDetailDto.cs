namespace ZooSanMarino.Application.DTOs.Lotes;

using ZooSanMarino.Application.Calculos;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

public sealed record LoteDetailDto(
    int       LoteId,  // Cambiado a int para secuencia numérica
    string    LoteNombre,
    int?      LotePosturaBaseId,
    int       GranjaId,
    string?   NucleoId,
    string?   GalponId,
    string?   Regional,
    DateTime? FechaEncaset,
    int?      HembrasL,
    int?      MachosL,
    double?   PesoInicialH,
    double?   PesoInicialM,
    double?   UnifH,
    double?   UnifM,
    int?      MortCajaH,
    int?      MortCajaM,
    string?   Raza,
    int?      AnoTablaGenetica,
    string?   Linea,
    string?   TipoLinea,
    string?   CodigoGuiaGenetica,
    int?      LineaGeneticaId,  // ← NUEVO: ID de la línea genética
    string?   Tecnico,
    int?      Mixtas,
    double?   PesoMixto,
    int?      AvesEncasetadas,
    int?      EdadInicial,
    string?   LoteErp,  // ← NUEVO: Código ERP del lote
    string?   EstadoTraslado,  // ← Estados: null/"normal", "trasladado", "en_transferencia"
    int?      LotePadreId,
    int?      PaisId,        // País en sesión al crear
    string?   PaisNombre,
    string?   EmpresaNombre,
    // Auditoría
    int       CompanyId,
    int       CreatedByUserId,
    DateTime  CreatedAt,
    int?      UpdatedByUserId,
    DateTime? UpdatedAt,
    // Relaciones (tomadas de Shared)
    FarmLiteDto    Farm,
    NucleoLiteDto? Nucleo,
    GalponLiteDto? Galpon,
    // Códigos ERP avícolas (empresas con manejaCodigosErpAvicola = true)
    string?   CodigoCentroCosto      = null,
    string?   DescripcionCentroCosto = null,
    // ─── Señales que deciden la fase REAL del lote ───
    // La pantalla derivaba la fase de las semanas desde el encasetamiento, así que todo lote
    // cargado con historia aparecía en «Producción» sin haber pasado nunca a producción. Estas dos
    // señales vienen de la BD y `FaseActual` las traduce con la fórmula única.
    /// <summary>El levante del lote está cerrado (<c>lote_postura_levante.estado_cierre</c>).</summary>
    bool      LevanteCerrado  = false,
    /// <summary>Existe el lote de producción: una fila viva en <c>lote_postura_produccion</c>.</summary>
    bool      TieneProduccion = false
)
{
    /// <summary>
    /// Fase real del lote — <c>Levante</c> o <c>Produccion</c> — para mostrar en pantalla.
    ///
    /// <para>
    /// Es una propiedad DERIVADA y no un parámetro del constructor a propósito: la proyección a SQL
    /// trae las dos señales y la fase se resuelve acá con <see cref="FaseLoteCalculos.ResolverFaseVisible"/>.
    /// Escribir el ternario dentro del <c>Select</c> habría duplicado la regla —EF Core no traduce
    /// una llamada a método propio dentro del árbol de expresión— y este repositorio ya pagó caro
    /// tener el mismo número calculado en dos lugares.
    /// </para>
    /// </summary>
    public string FaseActual => FaseLoteCalculos.ResolverFaseVisible(LevanteCerrado, TieneProduccion);
}
