// src/ZooSanMarino.Domain/Enums/InventoryMovementType.cs
namespace ZooSanMarino.Domain.Enums;

public enum InventoryMovementType
{
    Entry,
    Exit,
    TransferOut,
    TransferIn,
    Adjust,

    // Fase 2 — consumo/devolución automáticos desde seguimientos (Colombia, modelo A).
    // Persisten como string vía HasConversion(ToString/Parse). "DevolucionSeguimiento"=21 chars
    // NO cabe en el varchar(20) original → la migración 20260703140000 amplía movement_type a
    // varchar(30) (DDL no destructivo; sin CHECK en BD). "ConsumoSeguimiento"=18.
    // EXCLUIDOS de los 4 buckets del ReporteContable (filtra por Entry/TransferIn/
    // TransferOut/Exit literales) → no distorsionan las cifras del contable.
    // Signo en el kardex: ConsumoSeguimiento → -1 ; DevolucionSeguimiento → +1.
    ConsumoSeguimiento,
    DevolucionSeguimiento
}
