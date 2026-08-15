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
public record ResultadoValidacionDto(
    string Modulo,
    long SeguimientoId,
    int ItemsAplicados,
    decimal KgAplicados,
    int AvesDescontadas
);
