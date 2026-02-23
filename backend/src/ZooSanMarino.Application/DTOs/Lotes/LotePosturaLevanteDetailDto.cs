namespace ZooSanMarino.Application.DTOs.Lotes;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

/// <summary>
/// DTO para listado/detalle de lote_postura_levante.
/// </summary>
public sealed record LotePosturaLevanteDetailDto(
    int       LotePosturaLevanteId,
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
    double?   UnifH,
    double?   UnifM,
    int?      MortCajaH,
    int?      MortCajaM,
    string?   Raza,
    int?      AnoTablaGenetica,
    string?   Linea,
    string?   TipoLinea,
    string?   CodigoGuiaGenetica,
    int?      LineaGeneticaId,
    string?   Tecnico,
    int?      Mixtas,
    double?   PesoMixto,
    int?      AvesEncasetadas,
    int?      EdadInicial,
    string?   LoteErp,
    string?   EstadoTraslado,
    int?      PaisId,
    string?   PaisNombre,
    string?   EmpresaNombre,
    int       CompanyId,
    DateTime  CreatedAt,
    // Campos específicos postura levante
    int?      LoteId,
    int?      LotePadreId,
    int?      LotePosturaLevantePadreId,
    int?      AvesHInicial,
    int?      AvesMInicial,
    int?      AvesHActual,
    int?      AvesMActual,
    int?      EmpresaId,
    int?      UsuarioId,
    string?   Estado,
    string?   Etapa,
    int?      Edad,
    string?   EstadoCierre, // Abierto | Cerrado (semana 26+)
    // Relaciones
    FarmLiteDto    Farm,
    NucleoLiteDto? Nucleo,
    GalponLiteDto? Galpon
);
