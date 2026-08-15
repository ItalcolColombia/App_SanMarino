// src/ZooSanMarino.Domain/Entities/SeguimientoReservaAves.cs
// Separación de aves de un seguimiento diario pendiente de validar.

namespace ZooSanMarino.Domain.Entities;

/// <summary>
/// Aves <b>separadas</b> por un seguimiento diario que todavía no fue validado: bajas del día
/// (mortalidad + selección + error de sexaje) que ya no están disponibles pero tampoco se
/// descontaron del maestro del lote. Tabla: <c>seguimiento_reserva_aves</c>.
///
/// <para>
/// <b>Qué evita.</b> Que un traslado, un despacho o una venta dispongan de aves que un registro
/// pendiente ya dio de baja. El saldo del lote no cambia hasta validar, así que sin esta reserva el
/// disponible seguiría mostrándolas vivas.
/// </para>
///
/// <para>
/// <b>Por qué tres columnas y no dos.</b> En un lote con sexos el saldo es hembras/machos; en uno
/// mixto es uno solo y sin sexar (Panamá y los lotes mixtos de engorde). Restar por sexo sobre un
/// saldo mixto no tendría contra qué aplicarse, así que la separación viaja en el mismo pool contra
/// el que se va a descontar.
/// </para>
/// </summary>
public class SeguimientoReservaAves
{
    public long Id { get; set; }

    public int CompanyId { get; set; }

    /// <summary>Módulo que originó la separación — ver <c>ModuloSeguimiento</c>.</summary>
    public string OrigenModulo { get; set; } = default!;

    /// <summary>Id del registro de seguimiento en la tabla de su módulo.</summary>
    public long OrigenSeguimientoId { get; set; }

    /// <summary>
    /// Clave numérica del lote en el módulo que corresponda (<c>lote_id</c>,
    /// <c>lote_ave_engorde_id</c>, <c>lote_reproductora_ave_engorde_id</c>…). No es una FK: cada
    /// módulo apunta a una tabla distinta.
    /// </summary>
    public int LoteRefInt { get; set; }

    /// <summary>Lote legible, solo para trazabilidad y mensajes.</summary>
    public string? LoteRef { get; set; }

    public DateOnly FechaSeguimiento { get; set; }

    /// <summary>Bajas separadas de hembras (lote con sexos).</summary>
    public int Hembras { get; set; }

    /// <summary>Bajas separadas de machos (lote con sexos).</summary>
    public int Machos { get; set; }

    /// <summary>Bajas separadas en un lote mixto, donde el saldo no está sexado.</summary>
    public int Mixtas { get; set; }

    /// <summary>ACTIVA | APLICADA | LIBERADA — ver <see cref="EstadoReservaSeguimiento"/>.</summary>
    public string Estado { get; set; } = EstadoReservaSeguimiento.Activa;

    public DateTimeOffset CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public DateTimeOffset? AplicadaAt { get; set; }
    public DateTimeOffset? LiberadaAt { get; set; }

    public Company Company { get; set; } = null!;
}
