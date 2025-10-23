// src/ZooSanMarino.Application/DTOs/LoteDto.cs
namespace ZooSanMarino.Application.DTOs;

public record LoteDto(
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

    // 👇 Campos agregados
    int?      Mixtas,
    double?   PesoMixto,
    int?      AvesEncasetadas,
    int?      EdadInicial,
    string?   LoteErp,  // ← NUEVO: Código ERP del lote

    string?   Tecnico
);
