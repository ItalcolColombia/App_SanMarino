// Semáforo de retraso y bloqueo del alta por registros vencidos.
// Partial de ValidacionSeguimientoService (namespace plano).
using ZooSanMarino.Application.Calculos;
using ZooSanMarino.Application.DTOs;

namespace ZooSanMarino.Infrastructure.Services;

public partial class ValidacionSeguimientoService
{
    /// <summary>
    /// Situación de validación del lote: cuántos registros están sin validar, cuántos vencieron el
    /// plazo y si eso bloquea el alta de un día nuevo. Es lo que alimenta el modal rojo que aparece al
    /// entrar al lote.
    ///
    /// <para>
    /// Con el flag apagado responde igual pero con <c>RequiereValidacion = false</c> y sin bloqueo: en
    /// esas empresas el backfill dejó todo validado y el modal nunca tiene nada que mostrar.
    /// </para>
    /// </summary>
    public async Task<PendientesValidacionDto> ObtenerPendientesAsync(
        string modulo, int loteId, CancellationToken ct = default)
    {
        var requiere = await RequiereValidacionAsync(ct);

        if (!ModuloSeguimiento.EsValido(modulo))
            return new PendientesValidacionDto(modulo, loteId, requiere, 0, 0, false, string.Empty,
                Array.Empty<RegistroValidacionDto>());

        var pendientes = await LeerPendientesDelLoteAsync(modulo, loteId, ct);
        var hoy = Hoy;

        var registros = pendientes
            .OrderBy(p => p.Fecha)
            .Select(p => new RegistroValidacionDto(
                Modulo: modulo,
                SeguimientoId: p.Id,
                Fecha: p.Fecha,
                Validado: false,
                Estado: ValidacionSeguimientoCalculos.EtiquetaEstado(false, p.Fecha, hoy),
                FechaLimite: ValidacionSeguimientoCalculos.FechaLimiteValidacion(p.Fecha)))
            .ToList();

        var vencidos = registros.Count(r => r.Estado == "EN_RETRASO");
        var bloquea = ValidacionSeguimientoCalculos.BloqueaAltaPorVencidos(requiere, vencidos);

        // Con el flag apagado no se arma mensaje: no hay nada que avisar y un texto vacío es lo que
        // el front usa para no abrir el modal.
        var mensaje = requiere
            ? ValidacionSeguimientoCalculos.MensajeAlertaPendientes(vencidos, registros.Count)
            : string.Empty;

        return new PendientesValidacionDto(
            modulo, loteId, requiere, registros.Count, vencidos, bloquea, mensaje, registros);
    }

    /// <summary>
    /// Corta el alta de un día nuevo cuando el lote tiene registros vencidos sin validar (decisión del
    /// usuario: el retraso además de avisar, bloquea).
    ///
    /// <para>
    /// El mensaje nombra las fechas concretas: un «tiene pendientes» sin decir cuáles obliga a
    /// buscarlos a mano en la tabla. No hace absolutamente nada con el flag apagado.
    /// </para>
    /// </summary>
    public async Task AsegurarPuedeRegistrarDiaAsync(string modulo, int loteId, CancellationToken ct = default)
    {
        if (!await RequiereValidacionAsync(ct)) return;
        if (!ModuloSeguimiento.EsValido(modulo)) return;

        var pendientes = await LeerPendientesDelLoteAsync(modulo, loteId, ct);
        if (pendientes.Count == 0) return;

        var hoy = Hoy;
        var vencidas = pendientes
            .Where(p => ValidacionSeguimientoCalculos.EstaEnRetraso(false, p.Fecha, hoy))
            .Select(p => p.Fecha)
            .ToList();

        if (!ValidacionSeguimientoCalculos.BloqueaAltaPorVencidos(true, vencidas.Count)) return;

        throw new InvalidOperationException(
            ValidacionSeguimientoCalculos.MensajeBloqueoPorVencidos(vencidas));
    }
}
