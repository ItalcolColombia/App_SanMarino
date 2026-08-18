// src/ZooSanMarino.Application/DTOs/AnomaliaAlimentoLiquidadoDto.cs
using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Un lote de engorde ya liquidado que congeló su liquidación con saldo de alimento en el galpón.
/// El saldo sale de la COPIA CONGELADA (lo que se aprobó al liquidar), no de un recálculo.
/// </summary>
public sealed record AnomaliaAlimentoLiquidadoFilaDto(
    int      CompanyId,
    int      GranjaId,
    string   Granja,
    string   NucleoId,
    string   GalponId,
    int      LoteAveEngordeId,
    string   LoteNombre,
    DateTime LiquidadoAt,
    /// <summary>Último día con seguimiento del lote: el corte de la foto congelada.</summary>
    DateTime? UltimoSeguimiento,
    /// <summary>Saldo de alimento aprobado al liquidar (foto congelada).</summary>
    decimal  SaldoCongeladoKg,
    /// <summary>Traslados de SALIDA del galpón posteriores al último seguimiento.</summary>
    decimal  SalidasPostKg,
    /// <summary>Stock de alimento que hoy tiene el galpón.</summary>
    decimal  StockGalponKg,
    /// <summary><c>SaldoCongeladoKg − SalidasPostKg</c>, con piso en 0.</summary>
    decimal  KgSinTrasladar,
    /// <summary>De lo anterior, lo que el stock del galpón ya no respalda.</summary>
    decimal  KgSinRespaldo,
    EstadoAlimentoLiquidado Estado,
    string   Detalle,
    /// <summary>Ciclo que ocupó el galpón después de esta liquidación, si ya existe.</summary>
    int?     LoteSiguienteId,
    string?  LoteSiguienteNombre,
    DateTime? LoteSiguienteEncaset
);

/// <summary>
/// Resultado del señalamiento: primero el resumen —cuántas liquidaciones dejaron alimento y cuántos
/// kilos— y después el detalle por lote.
/// </summary>
public sealed record AnomaliaAlimentoLiquidadoDto(
    /// <summary>Liquidaciones congeladas vigentes de la empresa (el denominador).</summary>
    int TotalLiquidados,
    /// <summary>…de esas, las que congelaron con saldo de alimento &gt; 0.</summary>
    int ConSaldo,
    /// <summary>
    /// Copias de backfill sin saldo congelado (NULL). No se les inventa un número: se cuentan aparte
    /// para que «28 de 90» no se lea como si las 90 tuvieran dato.
    /// </summary>
    int SinDatoCongelado,
    int PendientesEnGalpon,
    int SinRespaldoFisico,
    decimal KgSinTrasladar,
    decimal KgSinRespaldo,
    IReadOnlyList<AnomaliaAlimentoLiquidadoFilaDto> Lotes
);
