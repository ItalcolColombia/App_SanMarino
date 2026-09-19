// src/ZooSanMarino.Domain/Entities/SeguimientoDiarioAvesEngorde.cs
using System.Text.Json;

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Seguimiento diario por lote aves de engorde. Tabla: seguimiento_diario_aves_engorde.
/// Un registro por lote_ave_engorde_id por fecha.
/// </summary>
public class SeguimientoDiarioAvesEngorde
{
    public long Id { get; set; }
    public int LoteAveEngordeId { get; set; }
    public DateTime Fecha { get; set; }

    public int? MortalidadHembras { get; set; }
    public int? MortalidadMachos { get; set; }
    public int? SelH { get; set; }
    public int? SelM { get; set; }
    public int? ErrorSexajeHembras { get; set; }
    public int? ErrorSexajeMachos { get; set; }
    public decimal? ConsumoKgHembras { get; set; }
    public decimal? ConsumoKgMachos { get; set; }
    public string? TipoAlimento { get; set; }
    public string? Observaciones { get; set; }
    public string? Ciclo { get; set; }

    public double? PesoPromHembras { get; set; }
    public double? PesoPromMachos { get; set; }
    public double? UniformidadHembras { get; set; }
    public double? UniformidadMachos { get; set; }
    public double? CvHembras { get; set; }
    public double? CvMachos { get; set; }

    public double? ConsumoAguaDiario { get; set; }
    public double? ConsumoAguaPh { get; set; }
    public double? ConsumoAguaOrp { get; set; }
    public double? ConsumoAguaTemperatura { get; set; }

    public JsonDocument? Metadata { get; set; }
    public JsonDocument? ItemsAdicionales { get; set; }

    public double? KcalAlH { get; set; }
    public double? ProtAlH { get; set; }
    public double? KcalAveH { get; set; }
    public double? ProtAveH { get; set; }

    public string? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Saldo de alimento (kg) al cierre del día en bodega del lote, calculado desde
    /// lote_registro_historico_unificado (ingresos + traslados entrada − traslados salida − consumos).
    /// </summary>
    public decimal? SaldoAlimentoKg { get; set; }

    /// <summary>
    /// Histórico de consumo por ítem de alimento registrado en el día.
    /// Estructura: [{ nombre_alimento, saldo_inicial, consumo, saldo_final, unidad_medida }]
    /// </summary>
    public JsonDocument? HistoricoConsumoAlimento { get; set; }

    // Panamá: cantidad de alimento por categoría (quintales) — visible solo en formularios de Panamá
    public decimal? QqMixtas { get; set; }
    public decimal? QqHembras { get; set; }
    public decimal? QqMachos { get; set; }

    /// <summary>
    /// True cuando el registro fue generado automáticamente por el cruce de los lotes reproductora
    /// (primeros 7 días). Estos registros son de solo lectura en UI y los regenera el trigger de BD.
    /// </summary>
    public bool OrigenCruce { get; set; }

    // ── Doble validación ───────────────────────────────────────────
    /// <summary>
    /// Segunda confirmación del registro. Mientras está en <c>false</c> el seguimiento se puede
    /// editar y eliminar, y el alimento y las aves quedan SEPARADOS (ver
    /// <see cref="SeguimientoReservaAlimento"/>) en vez de descontados; al validar se aplica el
    /// consumo real y el descuento de aves.
    /// <para>
    /// Solo tiene efecto en empresas con <c>requiere_validacion_seguimiento_diario</c>. En las demás
    /// nace en <c>true</c> y nada lo lee.
    /// </para>
    /// <para>
    /// Los registros con <see cref="OrigenCruce"/> nacen validados: los escribe el trigger de BD
    /// desde reproductora, ya confirmados en su origen, y nadie los edita a mano.
    /// </para>
    /// <para>
    /// <b>Nace validado</b> (valor inicial <c>true</c>): un registro solo queda sin validar mientras
    /// tiene alimento o aves SEPARADOS esperando su aplicación, y eso lo dicen de forma explícita los
    /// caminos que separan (<c>Validado = !separa</c>). Con el valor inicial en <c>false</c> esto
    /// falló cuatro veces (los Crud, los traslados, el cruce de reproductora y la rama Colombia de
    /// Producción): cada creador tenía que acordarse, y el que no lo hacía dejaba filas «pendientes»
    /// que a las 24 h bloqueaban el alta de días nuevos del lote sin tener nada que validar.
    /// </para>
    /// <para>
    /// 🔴 El default de la COLUMNA en la BD sigue en <c>false</c> y la configuración de EF lo declara
    /// con <c>HasDefaultValue(false)</c>: NO cambiarlos a <c>true</c>. EF omite del INSERT el
    /// <c>false</c> de un <c>bool</c> con default y deja que lo ponga la BD, así que con default
    /// <c>true</c> los registros que SÍ separan nacerían validados (y en un deploy rodante, las tareas
    /// viejas lo harían con la doble validación encendida). El <c>true</c> de acá sí viaja explícito.
    /// </para>
    /// </summary>
    public bool Validado { get; set; } = true;
    public DateTime? ValidadoAt { get; set; }
    public string? ValidadoPor { get; set; }

    public virtual LoteAveEngorde? LoteAveEngorde { get; set; }
}
