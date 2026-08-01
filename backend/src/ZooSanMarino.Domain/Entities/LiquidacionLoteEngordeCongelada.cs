using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// CABECERA de la copia congelada de la liquidación de un lote de pollo engorde.
/// Tabla: <c>liquidacion_lote_engorde_congelada</c>. El detalle (las 47 columnas de la tabla
/// diaria × N días) vive en <c>liquidacion_lote_engorde_congelada_fila</c>, que NO se mapea en EF:
/// su única lectura es dentro de <c>fn_seguimiento_diario_engorde</c> (v13) y mapearla duplicaría
/// el esquema en un tercer lugar.
///
/// <para>
/// <b>Por qué existe.</b> La tabla diaria de engorde no es una foto: la fn la recalcula en cada
/// request. Cuando el 28-jul-2026 cambió la fórmula (v9 → v12), corridas CERRADAS hacía meses
/// cambiaron solas sin que nadie tocara un dato — Costos ya las había dado por cuadradas. Al
/// liquidar, esta copia guarda exactamente lo que se aprobó; la fn la devuelve desde entonces, y
/// los 4 consumidores que entran por LATERAL (Reporte de Costos, Informe Semanal Panamá, Cuadre de
/// alimento y el saldo persistido) quedan congelados con el mismo cambio.
/// </para>
///
/// <para>
/// <b>La señal de "liquidado" sigue siendo <c>estado_operativo_lote='Cerrado'</c></b> (los 9 gates
/// existentes ya comparan ese literal y la propia fn ramifica su aritmética por él). La copia es un
/// DERIVADO garantizado por la BD: índice único parcial (<c>WHERE anulada_at IS NULL</c>) ⇒ a lo
/// sumo una copia vigente, y el trigger <c>trg_lote_ave_engorde_anula_congelada</c> anula la copia
/// si el estado deja de ser Cerrado por cualquier camino (incluido un UPDATE crudo en BD).
/// </para>
///
/// <para>
/// <b>Ciclo de vida:</b> se crea al liquidar (misma transacción que el cambio de estado), se ANULA
/// —no se borra— al reabrir (queda el rastro de qué se había liquidado y con qué números) y
/// re-liquidar crea una copia nueva. <c>fn_recongelar_liquidacion_engorde</c> permite regenerarla
/// sin reabrir cuando se corrige la fórmula después de congelar.
/// </para>
///
/// <para>
/// ⚠️ <c>liquidado_at</c> del LOTE no es señal de nada: <c>AbrirLoteAsync</c> no la limpia y hay
/// lotes 'Abierto' que la conservan. Acá es solo la marca de tiempo copiada al congelar.
/// </para>
/// </summary>
public class LiquidacionLoteEngordeCongelada
{
    public long Id { get; set; }

    public int LoteAveEngordeId { get; set; }

    public int CompanyId { get; set; }

    /// <summary>Granja del lote al congelar — permite filtrar/auditar sin join.</summary>
    public int GranjaId { get; set; }

    /// <summary>Momento de la liquidación (copiado del lote; es la fecha que eligió el usuario).</summary>
    public DateTime LiquidadoAt { get; set; }

    public string LiquidadoPorUserId { get; set; } = null!;

    public DateTime CongeladaAt { get; set; }

    /// <summary>'cierre' | 'backfill' | 'recongelado' | 'correccion'.</summary>
    public string Origen { get; set; } = null!;

    /// <summary>Versión de la fórmula que produjo la foto (ej. "v13"). Trazabilidad.</summary>
    public string FnVersion { get; set; } = null!;

    /// <summary>Cantidad de días copiados. Permite verificar la copia sin leer el detalle.</summary>
    public int Filas { get; set; }

    /// <summary>md5 del detalle ordenado — atajo para el diff copia vs vivo.</summary>
    public string Checksum { get; set; } = null!;

    // ── Resumen de liquidación aprobado (campos del LiquidacionLoteEngordeResumenDto, tipados).
    //    En copias de backfill quedan en NULL (TotalAvesInicio IS NULL ⇒ el resumen cae a vivo):
    //    replicar la aritmética de LiquidacionEngordeCalculos en SQL sería una segunda
    //    implementación del mismo cálculo, la deuda que este repo ya pagó cara.
    public string LoteNombre { get; set; } = null!;
    public string EstadoOperativoLote { get; set; } = null!;
    public int? HembrasInicio { get; set; }
    public int? MachosInicio { get; set; }
    public int? MixtasInicio { get; set; }
    public int? TotalAvesInicio { get; set; }
    public int? VentasTotalHembras { get; set; }
    public int? VentasTotalMachos { get; set; }
    public int? VentasTotalMixtas { get; set; }
    public int? AvesVivasActuales { get; set; }
    public int? MovimientosVentaCount { get; set; }
    public decimal? SaldoAlimentoKg { get; set; }
    public int? MermaUnidades { get; set; }
    public decimal? MermaKilos { get; set; }

    /// <summary>Contexto variable: raza, año de tabla genética, guía, ventana de alimento previo…</summary>
    public JsonDocument? Metadata { get; set; }

    // ── Anulación (reapertura). La fn filtra `anulada_at IS NULL`: anulada = vuelve el cálculo vivo.
    public DateTime? AnuladaAt { get; set; }
    public string? AnuladaPorUserId { get; set; }
    public string? AnuladaMotivo { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }

    public LoteAveEngorde? LoteAveEngorde { get; set; }
}
