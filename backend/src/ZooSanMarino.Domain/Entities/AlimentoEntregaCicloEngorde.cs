using System;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// La ATRIBUCIÓN de un movimiento de alimento marcado «para el próximo ciclo», persistida como
/// <b>hecho</b>: quién entrega, quién recibe, cuántos kg y en qué fecha. Tabla
/// <c>alimento_entrega_ciclo_engorde</c>.
/// <para>
/// <b>Por qué existe.</b> Hasta la v16 la atribución era un veredicto que
/// <c>fn_seguimiento_diario_engorde</c> <b>recalculaba en cada lectura</b> sobre estado mutable. La
/// liquidación congela <b>un solo</b> extremo del handoff, así que al re-leer, el otro extremo cambiaba
/// de opinión y el handoff se partía: liquidar el cedente escondía 3.000 kg reales (la apertura del
/// destino caía a 0 y su cuadre a −3.000) y liquidar el destino los duplicaba (+3.000 kg creados, con
/// <c>descuadre_kg = 0,00</c> en los dos estados ⇒ el detector ciego). Fue el NO-GO del gate.
/// </para>
/// <para>
/// <b>El cambio de modelo.</b> La atribución se decide y se ESCRIBE una vez —al marcar o al
/// materializar— y la fn la <b>lee</b>. Congelar un extremo deja de poder cambiar lo que ve el otro,
/// porque no queda nada que recalcular. La entrega es <b>una fila leída por DOS lotes</b>: el cedente
/// la ve como salida en <see cref="FechaEntrega"/> y el destino como crédito de apertura, por los
/// mismos <see cref="KgEntregados"/>. Ninguno puede dejar de honrarla unilateralmente ⇒ los dos
/// bloqueantes del NO-GO son inconstruibles.
/// </para>
/// <para>
/// La <b>intención</b> sigue viviendo en <c>lote_registro_historico_unificado.para_proximo_ciclo</c>
/// (y su espejo en <c>inventario_gestion_movimiento</c>): eso es lo que pidió la persona. Esta tabla es
/// lo que el sistema <b>resolvió</b> a partir de esa intención. Son cosas distintas y por eso viven
/// separadas.
/// </para>
/// Plan: fase_de_desarrollo/v16_engorde_atribucion_persistida_plan.md (FASE B, §3.2)
/// </summary>
public class AlimentoEntregaCicloEngorde
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    public int FarmId { get; set; }

    /// <summary>Núcleo del galpón. Cadena vacía —no NULL— cuando la ubicación no lo usa, para que la
    /// clave natural compare igual que en la fn (<c>COALESCE(TRIM(...), '')</c>).</summary>
    public string NucleoId { get; set; } = string.Empty;

    /// <summary>Galpón. Obligatorio: sin galpón no hay ciclo al que atribuir el alimento.</summary>
    public string GalponId { get; set; } = null!;

    // ─── El movimiento del que nace ──────────────────────────────────────────────────────────────

    /// <summary>Tabla origen del movimiento. Junto con <see cref="OrigenId"/> es la clave REAL, la
    /// misma que usa <c>uq_lote_hist_origen</c> en el histórico unificado.</summary>
    public string OrigenTabla { get; set; } = null!;

    public long OrigenId { get; set; }

    /// <summary>Id de la fila del histórico unificado, cuando se pudo resolver. Es una comodidad de
    /// lectura: la clave sigue siendo (<see cref="OrigenTabla"/>, <see cref="OrigenId"/>).</summary>
    public long? HistId { get; set; }

    public DateTime FechaMovimiento { get; set; }

    public decimal KgMovimiento { get; set; }

    public string? NumeroDocumento { get; set; }

    // ─── El HECHO ────────────────────────────────────────────────────────────────────────────────

    /// <summary>Ciclo en posesión del galpón cuando llegó el alimento. Es quien lo entrega.</summary>
    public int? LoteCedenteId { get; set; }

    /// <summary>Ciclo que recibe. NULL mientras el estado es <c>PENDIENTE</c>.</summary>
    public int? LoteDestinoId { get; set; }

    /// <summary>Último día visible del cedente: donde se escribe la salida sintética. Es también el
    /// único día donde puede escribirse, porque no queda ninguna fila posterior que pudiera quedar
    /// negativa.</summary>
    public DateTime? FechaEntrega { get; set; }

    /// <summary>Kg efectivamente entregados, ya <b>topados</b> al saldo real del cedente a
    /// <see cref="FechaEntrega"/> y congelados acá. 0 si el estado no es <c>VIGENTE</c>.</summary>
    public decimal KgEntregados { get; set; }

    /// <summary>Residuo que el cedente no tenía para entregar. No se compensa ni se esconde: es la
    /// anomalía de R2 y se SEÑALA (la lee <c>GET /liquidados-con-alimento</c>).</summary>
    public decimal KgNoDiferible { get; set; }

    /// <summary>
    /// <c>PENDIENTE</c> (hay intención, todavía no hay destino operativo · la fn no hace nada) ·
    /// <c>VIGENTE</c> (handoff escrito · salida en el cedente + crédito en el destino) ·
    /// <c>INERTE</c> (la marca no aplica: convivencia, ya visible en el destino, salida, extremo
    /// congelado… · la fn no hace nada) · <c>ANULADA</c> (el hecho se deshizo · la fila QUEDA).
    /// Ver <c>EstadoEntregaAlimentoCiclo</c>.
    /// </summary>
    public string Estado { get; set; } = null!;

    /// <summary>Texto legible que explica el estado. Es lo que la operación lee en la bandeja para
    /// entender por qué su marca hizo o no hizo algo.</summary>
    public string? Motivo { get; set; }

    /// <summary>
    /// Una entrega cuyo cedente <b>o</b> destino tiene liquidación congelada vigente queda
    /// <b>inmutable</b>: no se anula, no cambia de kg, no se re-materializa. Sin esta regla, tocar el
    /// hecho después de congelar un extremo reabre exactamente el agujero del NO-GO.
    /// </summary>
    public bool Sellada { get; set; }

    // ─── Auditoría ───────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; set; }

    public string? CreatedByUserId { get; set; }

    public DateTimeOffset? AnuladaAt { get; set; }

    public string? AnuladaPorUserId { get; set; }

    public string? AnuladaMotivo { get; set; }
}

/// <summary>Los 4 estados persistidos de <see cref="AlimentoEntregaCicloEngorde"/>. Colapsan los 17
/// veredictos que el intento anterior recalculaba en lectura: lo que aquéllos distinguían vive ahora
/// en <see cref="AlimentoEntregaCicloEngorde.Motivo"/>, que es texto para la UI y no lógica.</summary>
public static class EstadoEntregaAlimentoCiclo
{
    public const string Pendiente = "PENDIENTE";
    public const string Vigente = "VIGENTE";
    public const string Inerte = "INERTE";
    public const string Anulada = "ANULADA";
}
