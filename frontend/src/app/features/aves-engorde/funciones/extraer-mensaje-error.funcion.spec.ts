import { extraerMensajeError } from './extraer-mensaje-error.funcion';

/**
 * `HttpErrorResponse.message` es siempre el genérico de Angular — nunca el motivo real. Este helper
 * debe encontrar el mensaje real en las 3 formas que devuelve el backend, y solo caer al default
 * cuando ninguna aplica (antes: el modal de liquidación Panamá mostraba "Http failure response for
 * .../ReporteIndicadorPanama/liquidar: 400 OK" sin decir nada, 26-ago-2026).
 */
describe('extraerMensajeError', () => {
  const DEFAULT = 'No se pudo guardar.';

  it('{ error } — convención de las excepciones de negocio del controller', () => {
    const err = { error: { error: 'El lote está liquidado. Reabra el lote para modificarlo.' } };
    expect(extraerMensajeError(err, DEFAULT)).toBe('El lote está liquidado. Reabra el lote para modificarlo.');
  });

  it('{ message } — convención del UseExceptionHandler global', () => {
    const err = { error: { message: 'Sesión inválida o expirada.' } };
    expect(extraerMensajeError(err, DEFAULT)).toBe('Sesión inválida o expirada.');
  });

  it('🔑 ValidationProblemDetails ({ errors }) del [ApiController] — el caso real del bug', () => {
    const err = {
      error: { title: 'One or more validation errors occurred.', errors: { avesFinalGranja: ["The JSON value could not be converted to System.Int32."] } }
    };
    const msg = extraerMensajeError(err, DEFAULT);
    expect(msg).toContain('avesFinalGranja');
    expect(msg).toContain('System.Int32');
  });

  it('errors con múltiples campos ⇒ todos se listan', () => {
    const err = { error: { errors: { campoA: ['msgA'], campoB: ['msgB1', 'msgB2'] } } };
    const msg = extraerMensajeError(err, DEFAULT);
    expect(msg).toContain('campoA: msgA');
    expect(msg).toContain('campoB: msgB1 msgB2');
  });

  it('solo title, sin errors ⇒ usa title', () => {
    expect(extraerMensajeError({ error: { title: 'Solicitud inválida.' } }, DEFAULT)).toBe('Solicitud inválida.');
  });

  it('error.error es un string plano (algún proxy/handler no-JSON) ⇒ se usa tal cual', () => {
    expect(extraerMensajeError({ error: 'Bad Gateway' }, DEFAULT)).toBe('Bad Gateway');
  });

  it('nada utilizable (el "Http failure response" genérico de Angular) ⇒ default del caller', () => {
    const err = { error: null, message: 'Http failure response for url: 400 OK' };
    expect(extraerMensajeError(err, DEFAULT)).toBe(DEFAULT);
  });

  it('err undefined/null no rompe', () => {
    expect(extraerMensajeError(undefined, DEFAULT)).toBe(DEFAULT);
    expect(extraerMensajeError(null, DEFAULT)).toBe(DEFAULT);
  });
});
