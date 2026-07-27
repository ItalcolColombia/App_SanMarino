namespace ZooSanMarino.Application.DTOs.Lotes;

/// <summary>Datos mostrados en el modal «Cerrar lote» (seguimiento diario levante).</summary>
public sealed record CierreLoteLevanteResumenDto(
    int LotePosturaLevanteId,
    string LoteNombre,
    int AvesHembrasDisponibles,
    int AvesMachosDisponibles,
    bool YaExisteLoteProduccion,
    /// <summary>
    /// Huevos capturados en el seguimiento diario de LEVANTE (semana 14+) que se arrastrarán al
    /// primer registro de producción al cerrar. 0 si la empresa no captura huevos en levante.
    /// </summary>
    int HuevosLevanteTotales = 0,
    int HuevosLevanteIncubables = 0
);

public sealed record CerrarLoteLevanteRequest(
    int HuevosIniciales,
    string ClosedByUserId,
    /// <summary>
    /// Fecha de inicio de producción. Si no se envía, se usa la fecha/hora actual del servidor.
    /// </summary>
    DateTime? FechaInicioProduccion = null,
    /// <summary>
    /// Aves iniciales que pasarán a producción (opcional). Si no se envía, se usan las aves actuales del levante.
    /// </summary>
    int? AvesHInicialProd = null,
    int? AvesMInicialProd = null,
    /// <summary>Motivo del ajuste de aves (solo referencia/auditoría; puede persistirse en el futuro).</summary>
    string? MotivoAjusteAves = null
);

public sealed record AbrirLoteLevanteRequest(
    string Motivo,
    string OpenedByUserId
);

/// <summary>
/// Datos mostrados en el modal «Abrir lote» (seguimiento diario levante) ANTES de confirmar.
/// <para>
/// Reabrir un levante elimina el lote de producción que generó el cierre, así que el usuario tiene
/// que saber de antemano si eso es posible y qué se va a perder. El backend revalida lo mismo al
/// confirmar: este resumen es para la UI, no la autoridad.
/// </para>
/// </summary>
/// <param name="PuedeReabrir">
/// false si hay seguimiento diario capturado por el usuario en producción, o si el lote de
/// producción está cerrado.
/// </param>
/// <param name="MotivoBloqueo">Explicación de por qué no se puede reabrir; null si se puede.</param>
/// <param name="Aviso">Qué va a pasar al reabrir (se muestra cuando sí se puede).</param>
/// <param name="RegistrosProduccionUsuario">Registros capturados por el usuario (los que bloquean).</param>
/// <param name="RegistrosProduccionSistema">
/// Registros que generó el propio cierre (arrastre de huevos y traslado de aves). Se eliminan con el
/// lote de producción y se regeneran en el siguiente cierre.
/// </param>
public sealed record ReaperturaLoteLevanteResumenDto(
    int LotePosturaLevanteId,
    string LoteNombre,
    bool EstaCerrado,
    bool PuedeReabrir,
    string? MotivoBloqueo,
    string Aviso,
    int? LotePosturaProduccionId,
    string? LoteProduccionNombre,
    bool LoteProduccionCerrado,
    int RegistrosProduccionUsuario,
    int RegistrosProduccionSistema,
    DateTime? PrimerRegistroUsuario = null,
    DateTime? UltimoRegistroUsuario = null
);
