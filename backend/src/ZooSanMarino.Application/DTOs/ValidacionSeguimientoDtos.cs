using ZooSanMarino.Application.Calculos;

namespace ZooSanMarino.Application.DTOs;

/// <summary>
/// Un registro de seguimiento visto desde la doble validación. Es lo mínimo que necesita la tabla
/// diaria para pintar el estado y decidir si muestra el botón de validar.
/// </summary>
/// <param name="Modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
/// <param name="SeguimientoId">Id del registro en la tabla de su módulo.</param>
/// <param name="Fecha">Día del seguimiento.</param>
/// <param name="Validado">True si ya se aplicó el consumo y el descuento de aves.</param>
/// <param name="Estado">VALIDADO | PENDIENTE | EN_RETRASO — derivado, no persistido.</param>
/// <param name="FechaLimite">Último día para validar sin quedar en retraso.</param>
/// <param name="ValidadoAt">Cuándo se validó.</param>
/// <param name="ValidadoPor">Quién lo validó.</param>
public record RegistroValidacionDto(
    string Modulo,
    long SeguimientoId,
    DateOnly Fecha,
    bool Validado,
    string Estado,
    DateOnly FechaLimite,
    DateTime? ValidadoAt = null,
    string? ValidadoPor = null
);

/// <summary>
/// Situación de validación de un lote. La consume el modal de alerta que aparece al entrar y el
/// gate que bloquea el alta de días nuevos.
/// </summary>
/// <param name="Modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
/// <param name="LoteId">Clave numérica del lote en ese módulo.</param>
/// <param name="RequiereValidacion">Flag de la empresa. En <c>false</c> el resto es informativo:
/// nada bloquea ni se pinta, porque la empresa no adoptó la doble validación.</param>
/// <param name="Pendientes">Registros sin validar (incluye los vencidos).</param>
/// <param name="Vencidos">Registros sin validar que pasaron el plazo de un día.</param>
/// <param name="BloqueaAlta">True si el lote no acepta un día nuevo hasta ponerse al día.</param>
/// <param name="Mensaje">Texto del modal de alerta, vacío si no hay nada pendiente.</param>
/// <param name="Registros">Detalle de los pendientes, para listar las fechas.</param>
public record PendientesValidacionDto(
    string Modulo,
    int LoteId,
    bool RequiereValidacion,
    int Pendientes,
    int Vencidos,
    bool BloqueaAlta,
    string Mensaje,
    IReadOnlyList<RegistroValidacionDto> Registros
);

/// <summary>
/// Resultado de validar un registro: qué se aplicó realmente. Se devuelve para que la UI pueda
/// confirmar el efecto —«se descontaron 850 kg y 12 aves»— en vez de un «listo» sin contenido.
/// </summary>
/// <param name="Modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
/// <param name="SeguimientoId">Registro validado.</param>
/// <param name="ItemsAplicados">Cuántas líneas de alimento pasaron de separadas a consumidas.</param>
/// <param name="KgAplicados">Kilos totales descontados del inventario.</param>
/// <param name="AvesDescontadas">Aves descontadas del maestro del lote.</param>
/// <param name="YaEstabaValidado">
/// True cuando el registro ya venía validado y no se hizo nada. Existe por la validación en bloque:
/// sin este dato, «lo validé yo ahora y no aplicó nada» (un día sin consumo ni bajas) y «otra pestaña
/// ya lo había validado» son el mismo <c>(0, 0, 0)</c>, y el conteo del bloque mentiría.
/// </param>
public record ResultadoValidacionDto(
    string Modulo,
    long SeguimientoId,
    int ItemsAplicados,
    decimal KgAplicados,
    int AvesDescontadas,
    bool YaEstabaValidado = false
);

/// <summary>
/// Una línea del reporte de la validación en bloque: qué pasó con cada registro.
/// </summary>
/// <param name="SeguimientoId">Registro de la tabla de su módulo.</param>
/// <param name="Fecha">Día del seguimiento.</param>
/// <param name="Resultado">Uno de <see cref="DesenlaceValidacionEnBloque"/>.</param>
/// <param name="ItemsAplicados">Líneas de alimento que pasaron de separadas a consumidas.</param>
/// <param name="KgAplicados">Kilos descontados por este registro.</param>
/// <param name="AvesDescontadas">Aves descontadas por este registro.</param>
/// <param name="Motivo">Por qué falló. Sólo viene en el registro que cortó el bloque.</param>
public record ResultadoValidacionEnBloqueItemDto(
    long SeguimientoId,
    DateOnly Fecha,
    string Resultado,
    int ItemsAplicados,
    decimal KgAplicados,
    int AvesDescontadas,
    string? Motivo
);

/// <summary>
/// Resultado de validar en bloque los pendientes de un lote.
///
/// <para>
/// <b><c>NoIntentados</c> no son fallas.</b> El bloque corta en la primera y deja el resto sin
/// intentar; separarlos es lo que permite decirle al operario «corregí ese registro y volvé a
/// validar» en vez de un «fallaron 15» que no señala nada.
/// </para>
/// </summary>
/// <param name="Modulo">Uno de <see cref="ModuloSeguimiento"/>.</param>
/// <param name="LoteId">Clave numérica del lote en ese módulo.</param>
/// <param name="Solicitados">Cuántos pendientes tenía el lote. Es la suma de los cuatro conteos.</param>
/// <param name="Validados">Validados en esta corrida (con o sin efecto).</param>
/// <param name="YaValidados">Ya venían validados; no se tocaron.</param>
/// <param name="Fallidos">0 o 1: el bloque corta en el primero.</param>
/// <param name="NoIntentados">Quedaron después del corte, o fuera del tope.</param>
/// <param name="KgAplicados">Kilos totales descontados por el bloque.</param>
/// <param name="AvesDescontadas">Aves totales descontadas por el bloque.</param>
/// <param name="SeguimientoCorte">Registro que cortó, si hubo.</param>
/// <param name="FechaCorte">Día del registro que cortó.</param>
/// <param name="MotivoCorte">Por qué cortó, en texto legible.</param>
/// <param name="Mensaje">Texto listo para mostrar; la UI no concatena nada.</param>
/// <param name="Detalle">Una línea por registro, en el orden en que se procesaron.</param>
public record ResultadoValidacionEnBloqueDto(
    string Modulo,
    int LoteId,
    int Solicitados,
    int Validados,
    int YaValidados,
    int Fallidos,
    int NoIntentados,
    decimal KgAplicados,
    int AvesDescontadas,
    long? SeguimientoCorte,
    DateOnly? FechaCorte,
    string? MotivoCorte,
    string Mensaje,
    IReadOnlyList<ResultadoValidacionEnBloqueItemDto> Detalle
);
