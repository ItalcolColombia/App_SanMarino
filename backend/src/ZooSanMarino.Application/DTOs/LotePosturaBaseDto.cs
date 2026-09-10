namespace ZooSanMarino.Application.DTOs;

public record LotePosturaBaseDto(
    int       LotePosturaBaseId,
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    // Datos del lote
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    // Empresa
    int       CompanyId,
    string?   CompanyNombre,
    // Usuario creador
    int       CreatedByUserId,
    // País
    int?      PaisId,
    string?   PaisNombre,
    // Granja
    int?      FarmId,
    string?   FarmNombre,
    // ERP
    DateTime? ErpCreate,
    // Auditoría
    DateTime  CreatedAt,
    // ─── Señales para el filtro Abiertos/Cerrados/Todos de Lote Management ───
    /// <summary>
    /// Cantidad de lotes (tabla <c>lotes</c>) que se derivaron de esta base — sin contar el lote
    /// "hijo de producción" (mismo criterio que <c>LoteService.GetAllAsync</c>, que ya lo excluye
    /// para no duplicar en pantalla el padre y el registro creado para seguimiento diario).
    /// </summary>
    int       TotalLotes = 0,
    /// <summary>
    /// Existe al menos un lote de esta base sin <c>FaseLoteCalculos.EstaLoteCerradoCompleto</c>.
    /// Default <c>true</c> a propósito: <c>CreateAsync</c>/<c>UpdateAsync</c> mapean la base sin
    /// correr la subquery, y una base recién creada o editada nunca tiene lotes cerrados — sigue
    /// siendo la respuesta correcta, no solo un fallback seguro.
    /// </summary>
    bool      TieneLoteAbierto = true
);

public record CreateLotePosturaBaseDto(
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    int?      FarmId,
    DateTime? ErpCreate
);

public record UpdateLotePosturaBaseDto(
    string    LoteNombre,
    string?   CodigoErp,
    string?   DescripcionErp,
    int       CantidadHembras,
    int       CantidadMachos,
    int       CantidadMixtas,
    string?   Raza,
    string?   TipoLinea,
    DateTime? FechaEncaset,
    int?      FarmId,
    DateTime? ErpCreate
);
