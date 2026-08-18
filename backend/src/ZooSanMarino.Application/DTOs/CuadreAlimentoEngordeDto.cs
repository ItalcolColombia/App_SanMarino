// src/ZooSanMarino.Application/DTOs/CuadreAlimentoEngordeDto.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Una fila del cuadre de alimento: un galpón, su ciclo vigente y si la tabla diaria sigue contando lo
/// mismo que el inventario. Proyección de <c>fn_cuadre_alimento_engorde</c>.
/// </summary>
public sealed record CuadreAlimentoEngordeFilaDto(
    int      CompanyId,
    string   Empresa,
    int      GranjaId,
    string   Granja,
    string   NucleoId,
    string   GalponId,
    int      LoteAveEngordeId,
    string   LoteNombre,
    string   EstadoOperativoLote,
    DateTime UltimoSeguimiento,
    /// <summary>Saldo de la tabla diaria en el último día con seguimiento.</summary>
    decimal  SaldoTablaKg,
    /// <summary>Alimento movido DESPUÉS del último seguimiento: no cabe en la tabla diaria.</summary>
    decimal  MovPostKg,
    decimal  StockKg,
    /// <summary><c>StockKg − MovPostKg</c>: lo que la tabla diaria debería estar mostrando.</summary>
    decimal  EsperadoKg,
    /// <summary><c>SaldoTablaKg − EsperadoKg</c>. Distinto de 0 = la tabla y el inventario se separaron.</summary>
    decimal  DescuadreKg,
    /// <summary>Días del ciclo que cierran en negativo.</summary>
    int      FilasNegativas,
    EstadoCuadreAlimento Estado,
    string   Detalle,
    /// <summary>
    /// Kilos que se corrigieron a mano en el stock (<c>AjusteStock</c> / <c>EliminacionStock</c>)
    /// DENTRO del ciclo activo. La tabla diaria no los ve —se espejan como <c>INV_OTRO</c>, que la fn
    /// no lee—, así que cuando el galpón no cuadra suelen ser la causa. Informativo: no se restan del
    /// descuadre.
    /// </summary>
    decimal  AjustesManualesKg = 0m,
    int      AjustesManualesCount = 0
);

/// <summary>Resultado del cuadre: el resumen que se mira primero y el detalle por galpón.</summary>
public sealed record CuadreAlimentoEngordeDto(
    int TotalGalpones,
    int Cuadran,
    int Descuadrados,
    int ConSaldoNegativo,
    decimal KgErrorAbsoluto,
    IReadOnlyList<CuadreAlimentoEngordeFilaDto> Galpones
);
