namespace ZooSanMarino.Application.DTOs.Lotes;

using FarmLiteDto = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

/// <summary>
/// DTO para listado/detalle de lote_postura_produccion.
/// </summary>
public sealed record LotePosturaProduccionDetailDto(
    int       LotePosturaProduccionId,
    string    LoteNombre,
    int       GranjaId,
    string?   NucleoId,
    string?   GalponId,
    string?   Regional,
    DateTime? FechaEncaset,
    int?      HembrasL,
    int?      MachosL,
    double?   PesoInicialH,
    double?   PesoInicialM,
    string?   Raza,
    string?   Linea,
    string?   Tecnico,
    int?      AvesEncasetadas,
    int?      EdadInicial,
    string?   LoteErp,
    int?      PaisId,
    string?   PaisNombre,
    string?   EmpresaNombre,
    int       CompanyId,
    DateTime  CreatedAt,
    // Campos producción
    DateTime? FechaInicioProduccion,
    int?      HembrasInicialesProd,
    int?      MachosInicialesProd,
    int?      LotePosturaLevanteId,
    int?      AvesHInicial,
    int?      AvesMInicial,
    int?      AvesHActual,
    int?      AvesMActual,
    string?   Estado,
    string?   Etapa,
    int?      Edad,
    string?   EstadoCierre,
    // Relaciones
    FarmLiteDto    Farm,
    NucleoLiteDto? Nucleo,
    GalponLiteDto? Galpon,
    // Feature 14 — referencia al Lote base (para resumen-mortalidad)
    int?      LoteId = null,
    // Acumulados de traslado en FASE PRODUCCIÓN
    int       ProduccionTrasladoIngresoHembras = 0,
    int       ProduccionTrasladoIngresoMachos  = 0,
    int       ProduccionTrasladoSalidaHembras  = 0,
    int       ProduccionTrasladoSalidaMachos   = 0
);
