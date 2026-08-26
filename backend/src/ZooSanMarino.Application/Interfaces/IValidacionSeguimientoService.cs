using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Interfaces;

/// <summary>
/// Todo lo que un seguimiento diario necesita saber para separar en vez de descontar.
/// </summary>
/// <param name="Modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
/// <param name="SeguimientoId">Registro que origina la separación.</param>
/// <param name="PaisId">País del LOTE (no del usuario): decide el modelo de inventario al aplicar.</param>
/// <param name="FarmId">Granja del lote.</param>
/// <param name="NucleoId">Núcleo, cuando el stock se lleva a ese nivel.</param>
/// <param name="GalponId">Galpón, cuando el stock se lleva a ese nivel.</param>
/// <param name="LoteRefInt">Clave numérica del lote en su módulo. En producción es el
/// <c>lote_postura_produccion_id</c>, que es contra el que se descuenta el saldo.</param>
/// <param name="LoteRef">Lote legible, para mensajes y trazabilidad.</param>
/// <param name="FechaSeguimiento">Día del registro.</param>
/// <param name="ConsumoPorItem">Consumo parseado de la metadata, con el origen del id conservado.</param>
/// <param name="Aves">Bajas del día ya repartidas según el lote sea mixto o por sexos.</param>
public sealed record SeparacionSeguimientoContexto(
    string Modulo,
    long SeguimientoId,
    int PaisId,
    int FarmId,
    string? NucleoId,
    string? GalponId,
    int LoteRefInt,
    string? LoteRef,
    DateOnly FechaSeguimiento,
    IReadOnlyDictionary<ItemConsumoKey, decimal> ConsumoPorItem,
    ReservaAvesLineas Aves
);

/// <summary>
/// Doble validación de los seguimientos diarios: separación (reserva) al guardar y descuento real al
/// validar.
///
/// <para>
/// Los cinco services de seguimiento consultan <see cref="RequiereValidacionAsync"/> y, si está
/// encendido, llaman a <see cref="SepararAsync"/> en vez de descontar. El descuento lo aplica después
/// <see cref="ValidarAsync"/> leyendo las reservas: la reserva es la cola de efectos pendientes, así
/// que el número que se descuenta es exactamente el que se separó.
/// </para>
/// </summary>
public interface IValidacionSeguimientoService
{
    /// <summary>¿La empresa activa opera con doble validación? Fail-closed ante duda.</summary>
    Task<bool> RequiereValidacionAsync(CancellationToken ct = default);

    /// <summary>
    /// Suspende la doble validación mientras dure el alcance devuelto: dentro, los seguimientos
    /// descuentan al guardar y nacen validados, como en las empresas que no la usan.
    ///
    /// <para>
    /// <b>Para qué.</b> La doble validación modela la captura del día a día: se separa, y alguien
    /// valida dentro del plazo. Una <b>carga histórica</b> —el Excel de migración, el puente de
    /// Panamá— no es eso: son días que ya pasaron y cuyo alimento ya se consumió de verdad. Tratarlos
    /// como pendientes es incorrecto de fondo y además rompía el import: la primera fila insertada
    /// queda vencida en el acto (el plazo es de un día) y <see cref="AsegurarPuedeRegistrarDiaAsync"/>
    /// rechazaba la segunda. Un lote de 40 días entraba con una sola fila.
    /// </para>
    ///
    /// <para>
    /// Se devuelve un <see cref="IDisposable"/> y no un setter para que el modo se apague solo, también
    /// si el import se cae a la mitad: un flag que queda encendido convertiría al resto de la request
    /// en una empresa sin doble validación. El servicio es <c>Scoped</c>, así que el modo nunca cruza
    /// de una request a otra.
    /// </para>
    /// </summary>
    IDisposable ModoCargaHistorica();

    /// <summary>
    /// Separa alimento y aves del registro. <b>Idempotente por registro</b>: libera lo que ese
    /// seguimiento tuviera separado y escribe lo nuevo, que es lo que hace que editar no necesite
    /// ningún cálculo de retorno.
    /// </summary>
    Task SepararAsync(SeparacionSeguimientoContexto contexto, CancellationToken ct = default);

    /// <summary>Libera lo separado por un registro (edición que quita consumo, o borrado).</summary>
    Task LiberarAsync(string modulo, long seguimientoId, CancellationToken ct = default);

    /// <summary>
    /// Aplica el efecto real: descuenta el alimento del inventario y las aves del maestro del lote,
    /// marca las reservas como aplicadas y el registro como validado. Todo en una transacción.
    /// </summary>
    Task<ResultadoValidacionDto> ValidarAsync(string modulo, long seguimientoId, CancellationToken ct = default);

    /// <summary>
    /// Valida en bloque todos los pendientes de un lote, en <b>orden cronológico</b> y cortando en la
    /// primera falla. Cada registro va en su propia transacción, así que el éxito es parcial y
    /// reintentar retoma donde paró.
    ///
    /// <para>
    /// Existe porque validar de a uno no escala: ItalcolPanama llegó a cargar 34 días en una sesión, y
    /// con el plazo contado desde la creación esos días entran completos y vencen todos juntos.
    /// </para>
    /// </summary>
    Task<ResultadoValidacionEnBloqueDto> ValidarPendientesDelLoteAsync(string modulo, int loteId, CancellationToken ct = default);

    /// <summary>
    /// Deshace la validación: devuelve el alimento y las aves y vuelve a dejar el registro separado y
    /// editable. Requiere permiso propio — es la única vía para corregir un registro ya validado.
    /// </summary>
    Task<ResultadoValidacionDto> DesvalidarAsync(string modulo, long seguimientoId, CancellationToken ct = default);

    /// <summary>Situación de validación del lote: pendientes, vencidos y si bloquea el alta.</summary>
    Task<PendientesValidacionDto> ObtenerPendientesAsync(string modulo, int loteId, CancellationToken ct = default);

    /// <summary>
    /// Lanza si el lote no puede recibir un día nuevo por tener registros vencidos sin validar.
    /// No hace nada con el flag apagado.
    /// </summary>
    Task AsegurarPuedeRegistrarDiaAsync(string modulo, int loteId, CancellationToken ct = default);

    // El disponible de ALIMENTO no se pide por acá: lo resuelve `InventarioGestionService.GetStockAsync`
    // inline, agrupando las reservas activas con el SILO en la clave. Hubo un `ReservadoPorItemAsync`
    // en esta interfaz que hacía la misma cuenta SIN el silo y nunca tuvo un llamador; se eliminó el
    // 17-ago-2026 porque no era sólo redundante —en Santa Reyes, donde el silo es la ubicación del
    // alimento, habría devuelto un número distinto para el mismo ítem—. Una sola fórmula por número.

    /// <summary>Aves separadas y activas de un lote, para descontarlas del disponible.</summary>
    Task<ReservaAvesLineas> ReservadoDeAvesAsync(string modulo, int loteId, CancellationToken ct = default);
}
