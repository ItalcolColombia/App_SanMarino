// src/ZooSanMarino.Domain/Entities/InventarioGestionMovimiento.cs
// Movimientos (ingresos y traslados) del módulo Gestión de Inventario (Panama/Ecuador).

namespace ZooSanMarino.Domain.Entities;

public class InventarioGestionMovimiento
{
    public int Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    public int FarmId { get; set; }
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }
    public int ItemInventarioEcuadorId { get; set; }

    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "kg";
    /// <summary>Ingreso, TrasladoEntrada, TrasladoSalida, Consumo</summary>
    public string MovementType { get; set; } = "Ingreso";

    /// <summary>Estado mostrado en histórico: Entrada planta, Entrada granja, Transferencia a granja, Transferencia a planta, Consumo.</summary>
    public string? Estado { get; set; }

    /// <summary>Origen del traslado (granja). Null si es Ingreso.</summary>
    public int? FromFarmId { get; set; }
    public string? FromNucleoId { get; set; }
    public string? FromGalponId { get; set; }

    public string? Reference { get; set; }
    public string? Reason { get; set; }
    public Guid? TransferGroupId { get; set; }

    /// <summary>
    /// Fecha del movimiento. La tipea el usuario y se materializa a mediodía UTC; si no la indica,
    /// queda la hora del servidor. <b>No es auditoría</b>: para el instante real de captura está
    /// <see cref="RegistradoAt"/>.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }

    /// <summary>
    /// Marca explícita «este alimento es para el PRÓXIMO encasetamiento de este galpón».
    /// <para>
    /// La fecha sola no alcanza para atribuir el alimento a un ciclo cuando los galpones se encadenan:
    /// en Ecuador 28 de 75 ciclos encadenados arrancan a menos de 10 días del cierre del anterior, así
    /// que una llegada real 2-7 días antes del encaset cae DENTRO del ciclo viejo y el corte por fecha
    /// la descarta. Con la marca, la atribución es del usuario y no depende de la ventana.
    /// </para>
    /// <para>
    /// <c>false</c> (default) = comportamiento previo intacto. Se copia al espejo
    /// <c>lote_registro_historico_unificado</c> por el trigger.
    /// </para>
    /// </summary>
    public bool ParaProximoCiclo { get; set; }

    /// <summary>
    /// Instante REAL en que se digitó el movimiento. <see cref="CreatedAt"/> deja de servir de
    /// auditoría en cuanto el usuario tipea una fecha —esa fecha lo pisa—, así que el dato de
    /// «cuándo se cargó esto» no tenía dónde vivir.
    /// <para><c>null</c> = fila anterior a la columna. NUNCA se pisa con la fecha del movimiento.</para>
    /// </summary>
    public DateTimeOffset? RegistradoAt { get; set; }

    public Farm Farm { get; set; } = null!;
    public ItemInventario ItemInventario { get; set; } = null!;
    public Company Company { get; set; } = null!;
    public Pais Pais { get; set; } = null!;
}
