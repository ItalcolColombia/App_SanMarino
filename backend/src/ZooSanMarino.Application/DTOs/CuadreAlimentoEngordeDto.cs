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
    int      AjustesManualesCount = 0,
    /// <summary>
    /// Kilos que la doble validación tiene <b>separados y todavía sin aplicar</b> en esta ubicación.
    ///
    /// <para>
    /// 🔴 Es la razón por la que <see cref="DescuadreKg"/> <b>no</b> es
    /// <c>SaldoTablaKg − (StockKg − MovPostKg)</c>: el descuadre que se publica ya viene corregido
    /// por este número (<c>CuadreAlimentoEngordeCalculos.DescuadreAjustadoPorReservas</c>), porque
    /// el consumo pendiente ya está dentro del saldo pero todavía no salió del inventario. Quien
    /// recalcule el invariante a mano —la pantalla, un ajuste de cuadre— tiene que restarlo del
    /// stock o le va a dar distinto que la fila que está mirando.
    /// </para>
    ///
    /// <para>
    /// Con el flag de doble validación apagado es siempre 0. Medido el 25-ago-2026: ItalcolEcuador 0,
    /// ItalcolPanama 12.609,7 kg en 3 reservas activas.
    /// </para>
    /// </summary>
    decimal  ReservadoActivoKg = 0m
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

/// <summary>
/// Pedido de «Cuadrar galpón»: el operador declara <b>cuántos kilos de alimento hay realmente</b> y
/// el sistema deriva qué escribir de cada lado.
///
/// <para>
/// No se piden los deltas, se pide el dato físico. Quien está parado frente al galpón sabe cuánto
/// alimento hay; no tiene por qué saber si el error está en el inventario o en la tabla diaria.
/// </para>
/// </summary>
/// <param name="LoteAveEngordeId">Ciclo activo del galpón, tal como lo devuelve el cuadre.</param>
/// <param name="ItemInventarioEcuadorId">
/// Ítem de alimento sobre el que se registra la corrección. El front propone el de mayor stock en el
/// galpón; se pide explícito porque un galpón puede tener varios alimentos y adivinar cuál corregir
/// es exactamente el tipo de decisión que no debe tomar el backend.
/// </param>
/// <param name="KilosRealesKg">Kilos que hay físicamente en el galpón. Cero es válido.</param>
/// <param name="Motivo">Obligatorio: es lo único que le explica al próximo por qué estos kilos cambiaron.</param>
public sealed record CuadrarGalponAlimentoRequest(
    int     LoteAveEngordeId,
    int     ItemInventarioEcuadorId,
    decimal KilosRealesKg,
    string  Motivo
);

/// <summary>Lo que el cuadre escribió, para que la pantalla muestre exactamente lo que pasó.</summary>
public sealed record CuadrarGalponAlimentoResultDto(
    string  Granja,
    string  NucleoId,
    string  GalponId,
    string  LoteNombre,
    decimal SaldoTablaAntesKg,
    decimal StockAntesKg,
    decimal MovPostKg,
    decimal KilosRealesKg,
    /// <summary>Kilos escritos en el inventario (<c>AjusteStock</c>). 0 = el inventario ya estaba bien.</summary>
    decimal DeltaStockKg,
    /// <summary>Kilos escritos en la tabla diaria (<c>AjusteCuadreTabla*</c>). 0 = la tabla ya estaba bien.</summary>
    decimal DeltaTablaKg,
    decimal DescuadreAntesKg,
    decimal DescuadreDespuesKg,
    string  Resumen
);
