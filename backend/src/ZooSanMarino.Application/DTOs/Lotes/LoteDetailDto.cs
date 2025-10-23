namespace ZooSanMarino.Application.DTOs.Lotes;

using FarmLiteDto   = ZooSanMarino.Application.DTOs.Farms.FarmLiteDto;
using NucleoLiteDto = ZooSanMarino.Application.DTOs.Shared.NucleoLiteDto;
using GalponLiteDto = ZooSanMarino.Application.DTOs.Shared.GalponLiteDto;

public sealed record LoteDetailDto(
    int       LoteId,  // Cambiado a int para secuencia numérica
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
    int?      LineaGeneticaId,  // ← NUEVO: ID de la línea genética
    string?   Tecnico,
    int?      Mixtas,
    double?   PesoMixto,
    int?      AvesEncasetadas,
    int?      EdadInicial,
    string?   LoteErp,  // ← NUEVO: Código ERP del lote
    // Auditoría
    int       CompanyId,
    int       CreatedByUserId,
    DateTime  CreatedAt,
    int?      UpdatedByUserId,
    DateTime? UpdatedAt,
    // Relaciones (tomadas de Shared)
    FarmLiteDto    Farm,
    NucleoLiteDto? Nucleo,
    GalponLiteDto? Galpon
);
