// src/ZooSanMarino.Application/Calculos/ApagadoDobleValidacionCalculos.cs
// Reglas PURAS de lo que pasa cuando una empresa APAGA la doble validación. Sin EF, sin estado, sin I/O:
// el service resuelve el flag y la empresa activa, y delega acá la decisión y los textos.
using System.Globalization;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Application.Calculos;

/// <summary>
/// Apagar <c>requiere_validacion_seguimiento_diario</c> con registros pendientes dejaba un estado que el
/// sistema no sabe cuidar: reservas ACTIVAS de alimento y de aves que ya nadie puede validar (con el flag
/// apagado la pantalla oculta el botón), y un borrado que devolvía stock que nunca había salido. Se vio
/// en Santa Reyes el 18-sep-2026: 6 registros y 7.011 kg separados y nunca aplicados, una reserva
/// huérfana y un Ingreso sobre el ítem equivocado.
///
/// <para>
/// <b>La regla (decisión del usuario):</b> si a una empresa que tenía la doble validación ENCENDIDA se le
/// quita, primero se validan todos sus pendientes y recién entonces se apaga. Si algo no se puede
/// validar —típicamente falta stock—, no se apaga y no se guarda ningún cambio de la empresa. Así el estado
/// «flag apagado con pendientes» deja de poder producirse.
/// </para>
/// </summary>
public static class ApagadoDobleValidacionCalculos
{
    /// <summary>Tope de líneas de detalle en el mensaje; el resto se resume en «y N lotes más».</summary>
    public const int MaxLineasDeFallos = 8;

    /// <summary>
    /// ¿Esta actualización APAGA el flag? Solo la transición encendido → apagado: con el flag ya apagado,
    /// o un <c>PUT</c> que no lo manda (<c>null</c> = «conservar»), o que lo enciende, no hay nada que
    /// validar y el comportamiento es el de siempre.
    /// </summary>
    /// <param name="flagActual">Valor guardado hoy en <c>companies</c>.</param>
    /// <param name="flagSolicitado">Valor que trae el DTO; <c>null</c> = el cliente no lo mandó.</param>
    public static bool EsApagado(bool flagActual, bool? flagSolicitado) =>
        flagActual && flagSolicitado == false;

    /// <summary>
    /// ¿Se puede aplicar la validación de los pendientes de <paramref name="companyIdObjetivo"/> desde la
    /// sesión que está activa en <paramref name="companyIdActiva"/>?
    ///
    /// <para>
    /// Validar descuenta inventario, aves y —en reproductora— dispara el cruce, y todo eso se resuelve
    /// contra la empresa ACTIVA (<c>ValidarAsync</c> exige <c>EsDeLaEmpresaActiva</c>; el descuento de
    /// Ecuador y Panamá rechaza una granja ajena). Aplicarlo desde otra empresa sería escribir en el
    /// inventario de una empresa que la sesión no tiene abierta. Solo se pregunta cuando HAY pendientes:
    /// sin nada que validar, apagar el flag no exige nada.
    /// </para>
    /// </summary>
    public static bool PuedeValidarDesdeEmpresaActiva(int companyIdObjetivo, int companyIdActiva) =>
        companyIdObjetivo > 0 && companyIdObjetivo == companyIdActiva;

    /// <summary>Texto del rechazo cuando hay pendientes pero la empresa activa es otra.</summary>
    public static string MensajeEmpresaActivaDistinta(int pendientes) =>
        "Para apagar la doble validación primero hay que validar " +
        $"{Registros(pendientes)} pendiente{(pendientes == 1 ? string.Empty : "s")} de la empresa, y eso descuenta su " +
        "inventario y sus aves, que solo se pueden tocar desde esa misma empresa. Cambie a esa empresa en el " +
        "selector de empresa activa y vuelva a apagarla. No se cambió nada.";

    /// <summary>Nombre legible del módulo para los mensajes.</summary>
    public static string EtiquetaModulo(string modulo) => modulo switch
    {
        ModuloSeguimiento.Levante => "Levante",
        ModuloSeguimiento.Produccion => "Producción",
        ModuloSeguimiento.Engorde or ModuloSeguimiento.EngordeEcuador => "Engorde",
        ModuloSeguimiento.Reproductora => "Reproductora",
        _ => modulo
    };

    /// <summary>
    /// Texto del rechazo cuando la validación previa al apagado no llegó a validar todo. Nombra el módulo,
    /// el lote, el registro, su fecha y el motivo de cada corte (hasta <see cref="MaxLineasDeFallos"/>
    /// líneas): un «no se pudo» sin decir cuál obliga a buscarlo a mano.
    /// </summary>
    public static string MensajeNoSePudoApagar(ResultadoValidacionEmpresaDto resultado)
    {
        var sinValidar = resultado.Fallidos + resultado.NoIntentados;

        var partes = new List<string>
        {
            $"No se puede apagar la doble validación: {sinValidar} de {resultado.Pendientes} " +
            $"registro{(resultado.Pendientes == 1 ? string.Empty : "s")} pendiente{(resultado.Pendientes == 1 ? string.Empty : "s")} " +
            "no se pudo validar. No se guardó ningún cambio de la empresa y el flag sigue encendido."
        };

        foreach (var f in resultado.Fallos.Take(MaxLineasDeFallos))
        {
            partes.Add(
                $"• {EtiquetaModulo(f.Modulo)}, lote {f.LoteId}: #{f.SeguimientoId} " +
                $"({f.Fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}): {f.Motivo}");
        }

        var restantes = resultado.Fallos.Count - MaxLineasDeFallos;
        if (restantes > 0)
            partes.Add($"…y {restantes} lote{(restantes == 1 ? string.Empty : "s")} más.");

        partes.Add(
            "Corrija lo indicado (por ejemplo, registre el ingreso de alimento que falta) y vuelva a apagarla: " +
            "lo que ya se validó se conserva y se retoma desde ahí.");

        return string.Join("\n", partes);
    }

    private static string Registros(int n) => $"{n} registro{(n == 1 ? string.Empty : "s")}";
}
