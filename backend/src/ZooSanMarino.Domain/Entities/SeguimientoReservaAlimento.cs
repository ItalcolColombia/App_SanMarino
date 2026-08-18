// src/ZooSanMarino.Domain/Entities/SeguimientoReservaAlimento.cs
// Separación de alimento de un seguimiento diario pendiente de validar.

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Alimento <b>separado</b> (reservado) por un seguimiento diario que todavía no fue validado.
/// Tabla: <c>seguimiento_reserva_alimento</c>.
///
/// <para>
/// <b>Una reserva NO es un movimiento.</b> No toca <c>inventario_gestion_stock</c> ni genera fila en
/// <c>inventario_gestion_movimiento</c>, y por lo tanto tampoco llega al espejo
/// <c>lote_registro_historico_unificado</c>. Lo único que hace es comprometer cantidad: el disponible
/// que ve el formulario pasa a ser <c>stock − Σ reservas activas</c>. El movimiento real recién se
/// escribe al validar, por el camino de siempre.
/// </para>
///
/// <para>
/// <b>Por qué existe.</b> El mismo galpón alimenta a dos lotes. Sin reserva, los dos ven el stock
/// completo y los dos creen tenerlo. Y como el registro se puede editar y borrar antes de validar,
/// descontar de una obligaría a devoluciones y recálculos manuales en cada corrección; separando,
/// editar es reescribir la reserva y borrar es liberarla.
/// </para>
/// </summary>
public class SeguimientoReservaAlimento
{
    public long Id { get; set; }

    public int CompanyId { get; set; }
    public int PaisId { get; set; }
    public int FarmId { get; set; }

    /// <summary>Ubicación, con el mismo criterio que <see cref="InventarioGestionStock"/>.</summary>
    public string? NucleoId { get; set; }
    public string? GalponId { get; set; }

    /// <summary>
    /// Silo donde está el alimento (empresas con <c>maneja_inventario_por_silo</c>). Cuando tiene
    /// valor, núcleo y galpón van en null: la ubicación del alimento es el silo.
    /// </summary>
    public int? SiloId { get; set; }

    public int ItemInventarioEcuadorId { get; set; }

    /// <summary>
    /// <c>true</c> = el id es de <c>item_inventario_ecuador</c> (camino 2); <c>false</c> = es de
    /// <c>catalogo_items</c> (camino 1, Colombia).
    /// <para>
    /// Hay que persistirlo: en Colombia los dos rangos de id COLISIONAN, así que al validar no se
    /// puede adivinar contra cuál resolver. Ecuador y Panamá mandan el mismo id por los dos campos,
    /// donde esto queda siempre en <c>true</c> y no cambia nada.
    /// </para>
    /// </summary>
    public bool EsItemInventario { get; set; } = true;

    /// <summary>
    /// Módulo que originó la separación — ver <c>ModuloSeguimiento</c>. Se guarda el string porque los
    /// cinco seguimientos viven en tablas distintas y no hay una FK única que los alcance.
    /// </summary>
    public string OrigenModulo { get; set; } = default!;

    /// <summary>Id del registro de seguimiento en la tabla de su módulo.</summary>
    public long OrigenSeguimientoId { get; set; }

    /// <summary>Lote legible, solo para trazabilidad y mensajes (no se usa para resolver nada).</summary>
    public string? LoteRef { get; set; }

    /// <summary>Día del seguimiento (no el de la carga): es el que ordena el kardex al aplicar.</summary>
    public DateOnly FechaSeguimiento { get; set; }

    public decimal CantidadKg { get; set; }
    public string Unit { get; set; } = "kg";

    /// <summary>ACTIVA | APLICADA | LIBERADA — ver <see cref="EstadoReservaSeguimiento"/>.</summary>
    public string Estado { get; set; } = EstadoReservaSeguimiento.Activa;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? AplicadaAt { get; set; }
    public DateTimeOffset? LiberadaAt { get; set; }

    public Company Company { get; set; } = null!;
    public Farm Farm { get; set; } = null!;

    // Sin navegación al ítem A PROPÓSITO: `item_inventario_ecuador_id` es POLIMÓRFICA —la tabla la
    // decide `es_item_inventario`—, así que no hay una sola entidad a la que apuntar. La FK que
    // había rechazaba con violación de clave foránea cualquier separación de Colombia cuyo
    // catalogo_items.id no existiera también como item_inventario_ecuador.id —208 de 435 ítems—,
    // así que guardar un seguimiento de levante o producción con el flag encendido daba 500.
}

/// <summary>
/// Estados de una separación. Solo <see cref="Activa"/> compromete disponible; los otros dos son
/// historia y se conservan para poder auditar qué pasó con cada reserva.
/// </summary>
public static class EstadoReservaSeguimiento
{
    /// <summary>Comprometida: descuenta del disponible pero no del stock.</summary>
    public const string Activa = "ACTIVA";

    /// <summary>El registro se validó y la reserva se convirtió en consumo real.</summary>
    public const string Aplicada = "APLICADA";

    /// <summary>El registro se editó o se eliminó antes de validar: la reserva dejó de comprometer.</summary>
    public const string Liberada = "LIBERADA";

    public static readonly IReadOnlySet<string> Todos =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Activa, Aplicada, Liberada };

    public static bool EsValido(string? estado) =>
        !string.IsNullOrWhiteSpace(estado) && Todos.Contains(estado);
}
